namespace SyncGraphDbApp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json.Linq;

    class DeleteDeviceIdentitySyncCommand : SyncCommandBase
    {
        readonly string hubName;
        readonly string twinId;
        readonly JToken jTwin;

        public DeleteDeviceIdentitySyncCommand(
            DocumentClient documentClient,
            DocumentCollection graphCollection,
            string hubName,
            string twinId,
            JToken jTwin)
            : base(documentClient, graphCollection)
        {
            this.hubName = hubName;
            this.twinId = twinId;
            this.jTwin = jTwin;
        }

        protected override async Task RunInternalAsync()
        {
            string graphTwinId = MapGraphTwinId(this.hubName, this.twinId);
            Console.WriteLine($"Try remove twin {graphTwinId} from graph ...");
            await this.ExecuteVertexCommandAsync($"g.V('{graphTwinId}').drop()");
        }
    }
}
