using System.Threading;
using System.Threading.Tasks;

namespace KsefIntegration.Abstractions
{
    public interface IKsefSessionService
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

        void Invalidate();
    }
}
