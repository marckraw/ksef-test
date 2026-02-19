using System.Threading;
using System.Threading.Tasks;
using KsefIntegration.Models;

namespace KsefIntegration.Abstractions
{
    public interface IKsefPdfVisualizationService
    {
        Task<string> GeneratePdfAsync(
            string invoiceXml,
            string outputPdfPath,
            string ksefNumber,
            PdfRenderOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
