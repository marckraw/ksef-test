using System.Threading;
using System.Threading.Tasks;
using KsefIntegration.Models;

namespace KsefIntegration.Abstractions
{
    public interface IKsefFacade
    {
        Task<string> DownloadInvoiceVisualizationAsync(
            string ksefNumber,
            string outputPdfPath,
            PdfRenderOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}
