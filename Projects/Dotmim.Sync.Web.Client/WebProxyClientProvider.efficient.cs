using System.Threading;
using System.Threading.Tasks;
using Dotmim.Sync.Enumerations;
using Dotmim.Sync.Messages;
using Newtonsoft.Json.Linq;

namespace Dotmim.Sync.Web.Client
{
    public partial class WebProxyClientProvider : IEfficientProvider
    {
        /// <summary>
        /// Processes an efficient sync request
        /// </summary>
        public async Task<MessageEfficientSyncResponse> ProcessEfficientSyncAsync(
            SyncContext context,
            MessageEfficientSync message)
        {
            var httpMessage = new HttpMessage
            {
                Step = HttpStep.EfficientSync,
                SyncContext = context,
                Content = message
            };

            // Use the existing HttpRequestHandler to process the request
            var response = await this.httpRequestHandler.ProcessRequest(httpMessage, message.SerializationFormat,
                this.cancellationToken);

            if (response?.Content is MessageEfficientSyncResponse syncResponse)
                return syncResponse;
            else if (response?.Content is JObject jObject)
                return jObject.ToObject<MessageEfficientSyncResponse>();

            throw new SyncException("Invalid response from server", SyncStage.None, this.GetType().Name);
        }
    }
}