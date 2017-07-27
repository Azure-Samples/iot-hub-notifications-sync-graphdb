namespace SyncGraphDbApp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Graphs.Elements;
    using Newtonsoft.Json.Linq;

    class CreateOrUpdateTwinSyncCommand : SyncCommandBase
    {
        readonly string hubName;
        readonly string twinId;
        readonly JToken jTwin;

        public CreateOrUpdateTwinSyncCommand(
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
            Vertex thermostat = await this.GetVertexByIdAsync(graphTwinId);
            if (thermostat == null)
            {
                Console.WriteLine("Add new thermostat to graph ...");
                await new CreateDeviceIdentitySyncCommand(this.DocumentClient, this.GraphCollection, this.hubName, this.twinId, this.jTwin).RunAsync();
            }
            else
            {
                Console.WriteLine("Update existing thermostat ...");
                await new UpdateTwinSyncCommand(this.DocumentClient, this.GraphCollection, this.hubName, this.twinId, this.jTwin).RunAsync();
            }
        }
    }
}
