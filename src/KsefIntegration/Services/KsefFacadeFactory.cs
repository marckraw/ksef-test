using System;
using System.Net.Http;
using KsefIntegration.Abstractions;
using KsefIntegration.Models;

namespace KsefIntegration.Services
{
    public static class KsefFacadeFactory
    {
        public static IKsefFacade Create(
            KsefSettings ksefSettings,
            PdfGeneratorSettings pdfSettings,
            HttpClient httpClient)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            var sessionService = new KsefSessionService(httpClient, ksefSettings);
            var invoiceService = new KsefInvoiceService(httpClient, ksefSettings, sessionService);
            var pdfService = new KsefPdfVisualizationService(pdfSettings);

            return new KsefFacade(invoiceService, pdfService);
        }
    }
}
