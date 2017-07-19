namespace SyncGraphDbApp
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json.Linq;

    class SyncCommandFactory
    {
        readonly DocumentClient documentClient;
        readonly DocumentCollection graphCollection;

        public SyncCommandFactory(DocumentClient documentClient, DocumentCollection graphCollection)
        {
            this.documentClient = documentClient;
            this.graphCollection = graphCollection;
        }

        public SyncCommandBase CreateDeviceIdentitySyncCommand(string hubName, string twinId, JToken jTwin)
        {
            return new AddTwinSyncCommand(this.documentClient, this.graphCollection, hubName, twinId, jTwin);
        }

        public SyncCommandBase DeleteDeviceIdentitySyncCommand(string hubName, string twinId, JToken jTwin)
        {
            return new DeleteTwinSyncCommand(this.documentClient, this.graphCollection, hubName, twinId, jTwin);
        }

        public SyncCommandBase UpdateTwinSyncCommand(string hubName, string twinId, JToken jTwin)
        {
            return new UpdateTwinSyncCommand(this.documentClient, this.graphCollection, hubName, twinId, jTwin);
        }

        public SyncCommandBase ReplaceTwinSyncCommand(string hubName, string twinId, JToken jTwin)
        {
            return new ReplaceTwinSyncCommand(this.documentClient, this.graphCollection, hubName, twinId, jTwin);
        }
    }
}
