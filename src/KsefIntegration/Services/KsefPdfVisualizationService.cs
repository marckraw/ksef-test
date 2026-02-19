using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KsefIntegration.Abstractions;
using KsefIntegration.Infrastructure;
using KsefIntegration.Models;

namespace KsefIntegration.Services
{
    public sealed class KsefPdfVisualizationService : IKsefPdfVisualizationService
    {
        private readonly PdfGeneratorSettings _settings;

        public KsefPdfVisualizationService(PdfGeneratorSettings settings)
        {
            KsefArgumentValidator.ValidatePdfSettings(settings);
            _settings = settings;
        }

        public async Task<string> GeneratePdfAsync(
            string invoiceXml,
            string outputPdfPath,
            string ksefNumber,
            PdfRenderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(invoiceXml))
            {
                throw new ArgumentException("Invoice XML cannot be empty.", nameof(invoiceXml));
            }

            if (string.IsNullOrWhiteSpace(outputPdfPath))
            {
                throw new ArgumentException("Output PDF path cannot be empty.", nameof(outputPdfPath));
            }

            if (string.IsNullOrWhiteSpace(ksefNumber))
            {
                throw new ArgumentException("KSeF number cannot be empty.", nameof(ksefNumber));
            }

            options = options ?? new PdfRenderOptions();

            var outputDirectory = Path.GetDirectoryName(outputPdfPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var tempXmlPath = Path.Combine(Path.GetTempPath(), $"ksef-{Guid.NewGuid():N}.xml");

            try
            {
                await Task.Run(
                    () => File.WriteAllText(tempXmlPath, invoiceXml),
                    cancellationToken).ConfigureAwait(false);

                var arguments = BuildArguments(tempXmlPath, outputPdfPath, ksefNumber, options);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _settings.CommandPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();

                    var timeoutMs = Math.Max(1, _settings.TimeoutSeconds) * 1000;
                    bool exited;
                    try
                    {
                        exited = await Task.Run(() => process.WaitForExit(timeoutMs), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        TryKill(process);
                        throw;
                    }

                    if (!exited)
                    {
                        TryKill(process);
                        throw new TimeoutException("PDF generator process timed out.");
                    }

                    var stdout = await stdoutTask.ConfigureAwait(false);
                    var stderr = await stderrTask.ConfigureAwait(false);

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException(
                            $"PDF generator failed with exit code {process.ExitCode}. stderr: {stderr}. stdout: {stdout}");
                    }
                }

                if (!File.Exists(outputPdfPath))
                {
                    throw new FileNotFoundException(
                        "PDF generator finished successfully but output file was not created.",
                        outputPdfPath);
                }

                return outputPdfPath;
            }
            finally
            {
                if (File.Exists(tempXmlPath))
                {
                    File.Delete(tempXmlPath);
                }
            }
        }

        private string BuildArguments(string inputXmlPath, string outputPdfPath, string ksefNumber, PdfRenderOptions options)
        {
            var template = _settings.ArgumentsTemplate;

            return template
                .Replace("{script}", QuoteIfNeeded(_settings.ScriptPath))
                .Replace("{input}", QuoteIfNeeded(inputXmlPath))
                .Replace("{output}", QuoteIfNeeded(outputPdfPath))
                .Replace("{ksefNumber}", QuoteIfNeeded(ksefNumber))
                .Replace("{includeQrCode}", options.IncludeQrCode ? "true" : "false")
                .Replace("{includeKsefMetadata}", options.IncludeKsefMetadata ? "true" : "false")
                .Replace("{extra}", options.AdditionalArguments ?? string.Empty);
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.Contains(" "))
            {
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            return value;
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}
