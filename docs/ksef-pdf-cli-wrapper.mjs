#!/usr/bin/env node
import fs from "node:fs/promises";
import path from "node:path";
import { Blob, File } from "node:buffer";
import { generateInvoice } from "./dist/ksef-fe-invoice-converter.mjs";

function printUsage() {
  console.error(
    "Usage: node ksef-pdf-cli-wrapper.mjs faktura <inputXmlPath> <outputPdfPath> [additionalDataJson]"
  );
}

function fail(message) {
  console.error(message);
  process.exitCode = 1;
}

async function main() {
  const [, , documentType, inputXmlPath, outputPdfPath, additionalDataRaw] = process.argv;

  if (!documentType || !inputXmlPath || !outputPdfPath) {
    printUsage();
    return fail("Missing required arguments.");
  }

  if (documentType.toLowerCase() !== "faktura") {
    return fail("Only 'faktura' documentType is supported by this wrapper.");
  }

  let additionalData = undefined;
  if (additionalDataRaw && additionalDataRaw.trim().length > 0) {
    try {
      additionalData = JSON.parse(additionalDataRaw);
    } catch (error) {
      return fail("Invalid additionalDataJson. Expected JSON object string.");
    }
  }

  const xmlBytes = await fs.readFile(inputXmlPath);
  const fileName = path.basename(inputXmlPath);
  const xmlFile = new File(
    [new Blob([xmlBytes], { type: "application/xml" })],
    fileName,
    { type: "application/xml", lastModified: Date.now() }
  );

  const pdfBytes = await generateInvoice(xmlFile, additionalData);

  const outputDirectory = path.dirname(outputPdfPath);
  await fs.mkdir(outputDirectory, { recursive: true });
  await fs.writeFile(outputPdfPath, Buffer.from(pdfBytes));
  console.log("PDF written:", outputPdfPath);
}

main().catch((error) => {
  fail(error?.stack || String(error));
});
