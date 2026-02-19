using System;
using System.Threading;
using System.Threading.Tasks;
using KsefIntegration.Abstractions;
using KsefIntegration.Models;

namespace KsefIntegration.Services
{
    public sealed class KsefFacade : IKsefFacade
    {
        private readonly IKsefInvoiceService _invoiceService;
        private readonly IKsefPdfVisualizationService _pdfVisualizationService;

        public KsefFacade(
            IKsefInvoiceService invoiceService,
            IKsefPdfVisualizationService pdfVisualizationService)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _pdfVisualizationService = pdfVisualizationService
                ?? throw new ArgumentNullException(nameof(pdfVisualizationService));
        }

        public async Task<string> DownloadInvoiceVisualizationAsync(
            string ksefNumber,
            string outputPdfPath,
            PdfRenderOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var invoiceXml = await _invoiceService
                .GetInvoiceXmlAsync(ksefNumber, cancellationToken)
                .ConfigureAwait(false);

            return await _pdfVisualizationService
                .GeneratePdfAsync(invoiceXml, outputPdfPath, ksefNumber, options, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
