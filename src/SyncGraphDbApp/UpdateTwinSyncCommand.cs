namespace SyncGraphDbApp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Graphs.Elements;
    using Newtonsoft.Json.Linq;

    class UpdateTwinSyncCommand : SyncCommandBase
    {
        readonly string hubName;
        readonly string twinId;
        readonly JToken jTwin;

        public UpdateTwinSyncCommand(
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

            Console.WriteLine("Get thermostat vertex ...");
            Vertex vTwin = await this.GetVertexByIdAsync(graphTwinId);
            if (vTwin == null)
            {
                Console.WriteLine("Thermostat does not exist in the graph.");
                return;
            }

            VertexProperty versionProperty = vTwin.GetVertexProperties("version").FirstOrDefault();
            long currentVersion = Convert.ToInt64(versionProperty?.Value ?? 0L);
            long newVersion = (long)this.jTwin["version"];

            if (currentVersion >= newVersion)
            {
                Console.WriteLine($"Current state has newer version '{currentVersion}' than update version '{newVersion}'. No update required.");
                return;
            }

            Dictionary<string, string> properties = new Dictionary<string, string>
            {
                {"version", newVersion.ToString() }
            };

            string reportedTemperature = this.ParseReportedTemperature(this.jTwin);
            if (!string.IsNullOrWhiteSpace(reportedTemperature))
            {
                properties.Add("temperature", reportedTemperature);
            }
            vTwin = await this.UpdateVertexAsync(graphTwinId, properties);

            Location? location = this.ParseTaggedLocation(this.jTwin);
            if (location != null)
            {
                await this.UpdateLocationAsync(vTwin, location.Value);
            }
        }
    }
}
