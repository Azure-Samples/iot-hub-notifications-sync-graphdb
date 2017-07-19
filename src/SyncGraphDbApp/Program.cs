namespace SyncGraphDbApp
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Graphs;
    using Microsoft.ServiceBus.Messaging;
    using Newtonsoft.Json;

    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        static async Task MainAsync()
        {
            try
            {
                string graphDbEndpoint = "https://ailn-graph.documents.azure.com:443/";
                string graphDbAuthKey = "AOxCOJiLCjsCtCTGQ5il4N72dPHVGCVMclOOQj34WkSAkLqvfcnBaOI19Xm66QIbxOwg2cCcwIC2jfKpTa8HEA==";

                string serviceBussConnectionString = "Endpoint=sb://ailn-sample.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=5SJDI1QBXpJUd5C4Z+wJP1Y4eQP7ZDT9vgkRdNnoHT0=";
                string eventHubName = "ailn-sample-twin-notifications";
                string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=ailnsample;AccountKey=8bqrvviAQvXKw2EAMK1DflqhwGSKky+nmNp6gk1Hy5kelRoejKez+NC8GMCF8B8ozoyzw9gZdyTVEJikXzZQpA==;EndpointSuffix=core.windows.net";

                using (var eventProcessorHost = new EventProcessorHost(
                    Guid.NewGuid().ToString("N"),
                    eventHubName,
                    EventHubConsumerGroup.DefaultGroupName,
                    serviceBussConnectionString,
                    storageConnectionString))
                {

                    using (var documentClient = new DocumentClient(
                        new Uri(graphDbEndpoint),
                        graphDbAuthKey,
                        new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp }))
                    {
                        await new Program().RunSampleAsync(eventProcessorHost, documentClient);

                        Console.WriteLine();
                        Console.WriteLine("*************************************");
                        Console.WriteLine("* Press any key to exit at any time *");
                        Console.WriteLine("*************************************");
                        Console.WriteLine();
                        Console.ReadKey();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async Task RunSampleAsync(EventProcessorHost eventProcessorHost, DocumentClient documentClient)
        {
            string databaseName = "graphdb";
            string collectionName = "Devices";

            Console.WriteLine($"Create database '{databaseName}' if not exists ...");
            Database database = await documentClient.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });

            Console.WriteLine($"Create document collection '{collectionName}' if not exists ...");
            DocumentCollection graph = await documentClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(databaseName),
                new DocumentCollection { Id = collectionName },
                new RequestOptions { OfferThroughput = 1000 });

            // create topology graph
            await this.CreateTopologyGraphAsync(documentClient, graph);

            var eventProcessorOptions = new EventProcessorOptions();
            var eventProcessorFactory = new TwinChangeEventProcessorFactory(documentClient, graph);
            await eventProcessorHost.RegisterEventProcessorFactoryAsync(eventProcessorFactory, eventProcessorOptions);
        }

        async Task CreateTopologyGraphAsync(DocumentClient documentClient, DocumentCollection graph)
        {
            Console.WriteLine();
            Console.WriteLine("======== Create topology graph if not exists ... ========");

            Dictionary<string, string> gremlinQueries = new Dictionary<string, string>
            {
                // building 43
                { "Building 43",                    "g.addV('building').property('id', 'B-43').property('address', '43')" },

                { "Building 43: Floor 1",           "g.addV('floor').property('id', 'Floor-1').property('name', '1').addE('located').to(g.V('B-43'))"},
                { "Building 43: Floor 1: Room 1A",  "g.addV('room').property('id', '1R').property('name', '1R').addE('located').to(g.V('Floor-1'))"},
                { "Building 43: Floor 1: Room 1B",  "g.addV('room').property('id', '1S').property('name', '1S').addE('located').to(g.V('Floor-1'))"},

                { "Building 43: Floor 2",           "g.addV('floor').property('id', 'Floor-2').property('name', '2').addE('located').to(g.V('B-43'))"},
                { "Building 43: Floor 2: Room 2A",  "g.addV('room').property('id', '2R').property('name', '2R').addE('located').to(g.V('Floor-2'))"},
                { "Building 43: Floor 2: Room 2B",  "g.addV('room').property('id', '2S').property('name', '2S').addE('located').to(g.V('Floor-2'))"},

                // building 44
                { "Building 44",                    "g.addV('building').property('id', 'B-44').property('address', '44')" },

                { "Building 44: Floor A",           "g.addV('floor').property('id', 'Floor-A').property('name', 'A').addE('located').to(g.V('B-44'))"},
                { "Building 44: Floor A: Room A1",  "g.addV('room').property('id', 'A1').property('name', 'A1').addE('located').to(g.V('Floor-A'))"},
                { "Building 44: Floor A: Room A2",  "g.addV('room').property('id', 'A2').property('name', 'A2').addE('located').to(g.V('Floor-A'))"},

                { "Building 44: Floor B",           "g.addV('floor').property('id', 'Floor-B').property('name', 'B').addE('located').to(g.V('B-44'))"},
                { "Building 44: Floor B: Room B1",  "g.addV('room').property('id', 'B1').property('name', 'B1').addE('located').to(g.V('Floor-B'))"},
                { "Building 44: Floor B: Room B2",  "g.addV('room').property('id', 'B2').property('name', 'B2').addE('located').to(g.V('Floor-B'))"},
            };

            foreach (KeyValuePair<string, string> gremlinQuery in gremlinQueries)
            {
                Console.WriteLine($"Running {gremlinQuery.Key}: {gremlinQuery.Value}");

                IDocumentQuery<dynamic> query = documentClient.CreateGremlinQuery<dynamic>(graph, gremlinQuery.Value);
                while (query.HasMoreResults)
                {
                    try
                    {
                        FeedResponse<dynamic> results = await query.ExecuteNextAsync();
                        foreach (dynamic result in results)
                        {
                            Console.WriteLine($"\t {JsonConvert.SerializeObject(result)}");
                        }
                    }
                    catch (DocumentClientException ex) when (ex.Error.Code == "Conflict")
                    {
                        Console.WriteLine("================= Graph already exists. =================");
                        return;
                    }
                }

                Console.WriteLine("===================== Grap created. =====================");
            }
        }
    }
}
