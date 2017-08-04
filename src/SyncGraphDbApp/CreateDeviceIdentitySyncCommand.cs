namespace SyncGraphDbApp
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Graphs.Elements;
    using Newtonsoft.Json.Linq;

    class CreateDeviceIdentitySyncCommand : SyncCommandBase
    {
        readonly string hubName;
        readonly string twinId;
        readonly JToken jTwin;

        public CreateDeviceIdentitySyncCommand(
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
            Dictionary<string, string> properties = new Dictionary<string, string>
            {
                { "version", ((long)this.jTwin["version"]).ToString() }
            };

            string reportedTemperature = this.ParseReportedTemperature(this.jTwin);
            if (!string.IsNullOrWhiteSpace(reportedTemperature))
            {
                properties.Add("temperature", reportedTemperature);
            }

            try
            {
                Console.WriteLine("Add new thermostat vertex ...");
                vTwin = await this.AddVertexAsync("thermostat", graphTwinId, properties);
            }
            catch (DocumentClientException ex) when (ex.Error.Code == "Conflict")
            {
                Console.WriteLine($"Thermostat vertex {graphTwinId} already exists in the graph.");
                return;
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
