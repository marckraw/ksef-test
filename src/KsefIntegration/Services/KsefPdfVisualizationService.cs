using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            var tempXmlPath = Path.Combine(Path.GetTempPath(), $"ksef-{Guid.NewGuid():N}.xml");

            try
            {
                await Task.Run(
                    () => File.WriteAllText(tempXmlPath, invoiceXml),
                    cancellationToken).ConfigureAwait(false);

                return await GeneratePdfFromXmlFileAsync(
                    tempXmlPath,
                    outputPdfPath,
                    ksefNumber,
                    options,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tempXmlPath))
                {
                    File.Delete(tempXmlPath);
                }
            }
        }

        public async Task<string> GeneratePdfFromXmlFileAsync(
            string inputXmlPath,
            string outputPdfPath,
            string ksefNumber,
            PdfRenderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(inputXmlPath))
            {
                throw new ArgumentException("Input XML path cannot be empty.", nameof(inputXmlPath));
            }

            if (!File.Exists(inputXmlPath))
            {
                throw new FileNotFoundException("Input XML file does not exist.", inputXmlPath);
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

            var operationStartedAtUtc = DateTime.UtcNow;
            var arguments = BuildArguments(inputXmlPath, outputPdfPath, ksefNumber, options);

            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.CommandPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            string lastStdout = string.Empty;
            string lastStderr = string.Empty;

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
                lastStdout = stdout ?? string.Empty;
                lastStderr = stderr ?? string.Empty;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        BuildProcessFailureMessage(
                            process.ExitCode,
                            startInfo.FileName,
                            startInfo.Arguments,
                            lastStderr,
                            lastStdout));
                }

                if (!File.Exists(outputPdfPath))
                {
                    var recoveredPath = TryRecoverOutputPdfPath(outputPdfPath, operationStartedAtUtc, lastStdout, lastStderr);
                    if (!string.IsNullOrWhiteSpace(recoveredPath) && File.Exists(recoveredPath))
                    {
                        if (!Path.GetFullPath(recoveredPath).Equals(Path.GetFullPath(outputPdfPath), StringComparison.OrdinalIgnoreCase))
                        {
                            var outputDir = Path.GetDirectoryName(outputPdfPath);
                            if (!string.IsNullOrWhiteSpace(outputDir))
                            {
                                Directory.CreateDirectory(outputDir);
                            }

                            if (File.Exists(outputPdfPath))
                            {
                                File.Delete(outputPdfPath);
                            }

                            File.Move(recoveredPath, outputPdfPath);
                        }
                    }
                }
            }

            if (!File.Exists(outputPdfPath))
            {
                throw new FileNotFoundException(
                    "PDF generator finished successfully but output file was not created. "
                    + "Command: "
                    + startInfo.FileName
                    + " "
                    + startInfo.Arguments
                    + ". stderr: "
                    + Truncate(lastStderr, 1500)
                    + ". stdout: "
                    + Truncate(lastStdout, 1500),
                    outputPdfPath);
            }

            return outputPdfPath;
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

        private static string BuildProcessFailureMessage(
            int exitCode,
            string? command,
            string? arguments,
            string? stderr,
            string? stdout)
        {
            return "PDF generator failed with exit code "
                + exitCode
                + ". Command: "
                + (command ?? "<null>")
                + " "
                + (arguments ?? string.Empty)
                + ". stderr: "
                + (stderr ?? string.Empty)
                + ". stdout: "
                + (stdout ?? string.Empty);
        }

        private static string? TryRecoverOutputPdfPath(
            string desiredOutputPath,
            DateTime operationStartedAtUtc,
            string stdout,
            string stderr)
        {
            var fromStdout = ExtractPdfPathFromText(stdout);
            if (!string.IsNullOrWhiteSpace(fromStdout) && File.Exists(fromStdout))
            {
                return fromStdout;
            }

            var fromStderr = ExtractPdfPathFromText(stderr);
            if (!string.IsNullOrWhiteSpace(fromStderr) && File.Exists(fromStderr))
            {
                return fromStderr;
            }

            var desiredDir = Path.GetDirectoryName(desiredOutputPath);
            if (string.IsNullOrWhiteSpace(desiredDir) || !Directory.Exists(desiredDir))
            {
                return null;
            }

            var candidates = new List<FileInfo>();
            foreach (var filePath in Directory.GetFiles(desiredDir, "*.pdf"))
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.LastWriteTimeUtc >= operationStartedAtUtc.AddSeconds(-2))
                {
                    candidates.Add(fileInfo);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates
                .OrderByDescending(c => c.LastWriteTimeUtc)
                .First()
                .FullName;
        }

        private static string? ExtractPdfPathFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var parts = text.Split(new[] { '\r', '\n', '\t', ' ', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && Path.IsPathRooted(part))
                {
                    return part;
                }
            }

            return null;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }
    }
}
