using System.Threading;
using System.Threading.Tasks;

namespace KsefIntegration.Abstractions
{
    public interface IKsefInvoiceService
    {
        Task<string> GetInvoiceXmlAsync(string ksefNumber, CancellationToken cancellationToken = default);
    }
}
