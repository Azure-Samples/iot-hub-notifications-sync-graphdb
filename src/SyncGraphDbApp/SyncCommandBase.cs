namespace SyncGraphDbApp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Graphs;
    using Microsoft.Azure.Graphs.Elements;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    abstract class SyncCommandBase
    {
        protected readonly DocumentClient DocumentClient;
        protected readonly DocumentCollection GraphCollection;

        protected SyncCommandBase(DocumentClient documentClient, DocumentCollection graphCollection)
        {
            this.DocumentClient = documentClient;
            this.GraphCollection = graphCollection;
        }

        public async Task RunAsync()
        {
            string commandName = this.GetType().Name;

            Console.WriteLine($"\t\t{commandName} {{");
            Console.WriteLine();

            await RunInternalAsync();

            Console.WriteLine($"\t\t}} {commandName}");
        }

        protected abstract Task RunInternalAsync();

        protected async Task ReplaceLocationAsync(Vertex vTwin, Location newLocation)
        {
            Console.WriteLine("Get current location if any ...");
            Edge veLocated = await this.ExecuteVertexEdgeCommandAsync($"g.V('{vTwin.Id}').outE('located')"); // assuming there can be only one location at a time
            if (veLocated != null)
            {
                // remove current location: remove room edge
                Console.WriteLine();
                Console.WriteLine("Remove current location ...");
                await this.DropVertexEdgeAsync(veLocated);
            }

            // verify new location exists: find the room on the floor in the building
            Console.WriteLine();
            Console.WriteLine("Check if new location is valid ...");
            Vertex vRoom = await this.FindRoomAsync(newLocation);
            if (vRoom == null)
            {
                Console.WriteLine($"WARNING! Provided location {newLocation} is not found in the graph. The thermostat will be located nowhere");
            }
            else
            {
                // add new location: add edge from device vertex to room vertex
                Console.WriteLine("Link thermostat to the location ...");
                await this.AddVertexEdgeAsync(vTwin, vRoom, "located");
            }
        }

        protected async Task UpdateLocationAsync(Vertex vTwin, Location location)
        {
            // get current located edge
            Console.WriteLine("Get current location (room, floor, building) ...");
            Location updatedLocation = await this.GetCurrentLocationAsync(vTwin);
            Console.WriteLine($"Current location: {updatedLocation}");

            if (!string.IsNullOrWhiteSpace(location.Building))
            {
                updatedLocation.Building = location.Building;
            }

            if (!string.IsNullOrWhiteSpace(location.Floor))
            {
                updatedLocation.Floor = location.Floor;
            }

            if (!string.IsNullOrWhiteSpace(location.Room))
            {
                updatedLocation.Room = location.Room;
            }

            Console.WriteLine("Now replace current location with updated location ...");
            Console.WriteLine();
            await this.ReplaceLocationAsync(vTwin, updatedLocation);
        }

        protected Task AddTwinAsync(string hubName, string twinId, JToken jTwin)
        {
            return new CreateDeviceIdentitySyncCommand(this.DocumentClient, this.GraphCollection, hubName, twinId, jTwin).RunAsync();
        }

        protected async Task<Vertex> AddVertexAsync(string label, string id, IDictionary<string, string> properties)
        {
            var sb = new StringBuilder($"g.addV('{label}').property('id', '{id}')");
            if (properties != null)
            {
                foreach (KeyValuePair<string, string> kvp in properties)
                {
                    sb.Append($".property('{kvp.Key}', '{kvp.Value}')");
                }
            }

            string command = sb.ToString();
            return await ExecuteVertexCommandAsync(command);
        }

        protected async Task<Vertex> UpdateVertexAsync(string id, IDictionary<string, string> properties)
        {
            var sb = new StringBuilder($"g.V('{id}')");
            if (properties != null)
            {
                foreach (KeyValuePair<string, string> kvp in properties)
                {
                    sb.Append($".property('{kvp.Key}', '{kvp.Value}')");
                }
            }

            string command = sb.ToString();
            return await ExecuteVertexCommandAsync(command);
        }

        protected async Task<Vertex> ExecuteVertexCommandAsync(string command)
        {
            Console.WriteLine($"Executing: {command} ...");
            IDocumentQuery<Vertex> query = this.DocumentClient.CreateGremlinQuery<Vertex>(this.GraphCollection, command);
            while (query.HasMoreResults)
            {
                Console.Write("Results:");
                FeedResponse<Vertex> results = await query.ExecuteNextAsync<Vertex>();
                foreach (Vertex vertex in results)
                {
                    Console.WriteLine($"\t {JsonConvert.SerializeObject(vertex)}");
                }
                Console.WriteLine();

                return results.FirstOrDefault();
            }

            return null;
        }

        protected async Task<Edge> ExecuteVertexEdgeCommandAsync(string command)
        {
            Console.WriteLine($"Executing: {command} ...");
            IDocumentQuery<Edge> query = this.DocumentClient.CreateGremlinQuery<Edge>(this.GraphCollection, command);
            while (query.HasMoreResults)
            {
                Console.Write("Results:");
                FeedResponse<Edge> results = await query.ExecuteNextAsync<Edge>();
                foreach (Edge edge in results)
                {
                    Console.WriteLine($"\t {JsonConvert.SerializeObject(edge)}");
                }
                Console.WriteLine();

                return results.FirstOrDefault();
            }

            return null;
        }

        protected Task<Vertex> GetVertexByIdAsync(string id) => ExecuteVertexCommandAsync($"g.V('{id}')");

        protected Task DropVertexEdgeAsync(Edge vertexEdge) => ExecuteVertexEdgeCommandAsync($"g.E('{vertexEdge.Id}').drop()");

        protected string ParseReportedTemperature(JToken jTwin)
        {
            string temperaturePath = "properties.reported.temperature";
            JValue jTemperature = jTwin.SelectToken(temperaturePath) as JValue;
            if (jTemperature != null)
            {
                if (jTemperature.Type != JTokenType.Integer && jTemperature.Type != JTokenType.Float && jTemperature.Type != JTokenType.String)
                {
                    Console.WriteLine($"WARNING! Invalid temperature format. Path: {temperaturePath}");
                }

                return jTemperature.Value.ToString();
            }

            return null;
        }

        protected Location? ParseTaggedLocation(JToken jTwin)
        {
            string locationPath = "tags.location";
            JToken jLocation = jTwin.SelectToken(locationPath);
            if (jLocation == null)
            {
                return null;
            }

            Location location = new Location();
            if (jLocation.Type == JTokenType.Object)
            {
                location = jLocation.ToObject<Location>();
            }
            else
            {
                Console.WriteLine($"WARNING! Invalid location. Path: {locationPath}");
            }

            return location;
        }

        protected static string MapGraphTwinId(string hubName, string twinId)
        {
            // use hubName as twinId prefix. In real world example it could be used to identify device owner.
            return $"{hubName}-{twinId}";
        }

        Task<Edge> AddVertexEdgeAsync(Vertex vTwin, Vertex vRoom, string label) => this.ExecuteVertexEdgeCommandAsync($"g.V('{vTwin.Id}').addE('{label}').to(g.V('{vRoom.Id}'))");

        Task<Vertex> FindRoomAsync(Location newLocation)
        {
            string command = $@"g.V().hasLabel('building').has('address', '{newLocation.Building}').inE('located').outV().hasLabel('floor').has('name', '{newLocation.Floor}').inE('located').outV().hasLabel('room').has('name', '{newLocation.Room}')";

            return this.ExecuteVertexCommandAsync(command);
        }

        async Task<Location> GetCurrentLocationAsync(Vertex vTwin)
        {
            Location location = new Location();

            Vertex vRoom = await this.ExecuteVertexCommandAsync($"g.V('{vTwin.Id}').outE('located').inV().hasLabel('room').has('name')");
            if (vRoom == null)
            {
                return location;
            }
            location.Room = vRoom.GetVertexProperties("name").First().Value?.ToString(); // assuming there can be only one 'name' property

            Vertex vFloor = await this.ExecuteVertexCommandAsync($"g.V('{vRoom.Id}').outE('located').inV().hasLabel('floor').has('name')");
            if (vFloor == null)
            {
                return location;
            }
            location.Floor = vFloor.GetVertexProperties("name").First().Value?.ToString(); // assuming there can be only one 'name' property

            Vertex vBuilding = await this.ExecuteVertexCommandAsync($"g.V('{vFloor.Id}').outE('located').inV().hasLabel('building').has('address')");
            if (vBuilding != null)
            {
                location.Building = vBuilding.GetVertexProperties("address").First().Value?.ToString(); // assuming there can be only one 'address' property
            }

            return location;
        }
    }
}
