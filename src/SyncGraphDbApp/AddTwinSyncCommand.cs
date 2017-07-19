namespace SyncGraphDbApp
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Graphs.Elements;
    using Newtonsoft.Json.Linq;

    class AddTwinSyncCommand : SyncCommandBase
    {
        readonly string hubName;
        readonly string twinId;
        readonly JToken jTwin;

        public AddTwinSyncCommand(
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
            Vertex vTwin = null;
            do
            {
                try
                {
                    Console.WriteLine("Add new thermostat vertex ...");
                    vTwin = await this.AddVertexAsync("thermostat", graphTwinId, null);
                }
                catch (DocumentClientException ex) when (ex.Error.Code == "Conflict")
                {
                    Console.WriteLine($"Thermostat vertex {graphTwinId} already exists in the graph. Get the vertex ...");
                    vTwin = await this.GetVertexByIdAsync(graphTwinId);
                }
            } while (vTwin == null);

            // update temperature
            Dictionary<string, string> properties = null;
            string reportedTemperature = this.ParseReportedTemperature(this.jTwin);
            if (!string.IsNullOrWhiteSpace(reportedTemperature))
            {
                properties = new Dictionary<string, string>
                {
                    { "temperature", reportedTemperature }
                };

                Console.WriteLine("Update vertex temperature property ...");
                vTwin = await this.UpdateVertexAsync(graphTwinId, properties);
            }

            // replace location
            Location? location = this.ParseTaggedLocation(this.jTwin);
            if (location != null)
            {
                await this.ReplaceLocationAsync(vTwin, location.Value);
            }
        }
    }
}
