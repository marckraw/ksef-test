namespace KsefIntegration.Models
{
    public sealed class PdfGeneratorSettings
    {
        public string CommandPath { get; set; } = "node";

        public string ScriptPath { get; set; } = string.Empty;

        public string ArgumentsTemplate { get; set; } = "{script} faktura {input} {output} {extra}";

        public int TimeoutSeconds { get; set; } = 60;
    }
}
