using System.Threading;
using System.Threading.Tasks;
using Dotmim.Sync.Messages;

namespace Dotmim.Sync
{
    public interface IEfficientProvider : IProvider
    {
        Task<MessageEfficientSyncResponse> ProcessEfficientSyncAsync(
            SyncContext context,
            MessageEfficientSync message);
    }
}