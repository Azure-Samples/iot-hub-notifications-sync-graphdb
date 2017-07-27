namespace SyncGraphDbApp
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Microsoft.Azure.Graphs;
    using Microsoft.ServiceBus.Messaging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                string iotHubConnectionString = ConfigurationManager.AppSettings["IotHubConnectionString"];

                string graphDbEndpoint = ConfigurationManager.AppSettings["GraphDbEndpoint"];
                string graphDbAuthKey = ConfigurationManager.AppSettings["GraphDbAuthKey"];
                string databaseName = ConfigurationManager.AppSettings["GraphDbName"];
                string collectionName = ConfigurationManager.AppSettings["GraphDbCollectionName"];

                string serviceBussConnectionString = ConfigurationManager.AppSettings["ServiceBussConnectionString"];
                string eventHubName = ConfigurationManager.AppSettings["EventHubName"];
                string storageConnectionString = ConfigurationManager.AppSettings["StorageConnectionString"];

                using (var documentClient = new DocumentClient(
                        new Uri(graphDbEndpoint),
                        graphDbAuthKey,
                        new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp }))
                {
                    string action;
                    if (!ParseArguments(args, out action))
                    {
                        return;
                    }

                    Console.WriteLine($"Create database '{databaseName}' if not exists ...");
                    Database database = await documentClient.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });

                    Console.WriteLine($"Create document collection '{collectionName}' if not exists ...");
                    DocumentCollection graph = await documentClient.CreateDocumentCollectionIfNotExistsAsync(
                        UriFactory.CreateDatabaseUri(databaseName),
                        new DocumentCollection { Id = collectionName },
                        new RequestOptions { OfferThroughput = 1000 });

                    // create topology graph
                    await CreateTopologyGraphAsync(documentClient, graph);

                    if (action == "notifications")
                    {
                        Console.WriteLine("Sync graph using notifications ...");
                        Console.WriteLine();

                        using (var eventProcessorHost = new EventProcessorHost(
                            Guid.NewGuid().ToString("N"),
                            eventHubName,
                            EventHubConsumerGroup.DefaultGroupName,
                            serviceBussConnectionString,
                            storageConnectionString))
                        {
                            await new Program().RunNotificationsSampleAsync(eventProcessorHost, documentClient, graph);

                            WaitAnyKeyPressedToExit();
                        }
                    }
                    else if (action == "sync")
                    {
                        Console.WriteLine("Query all devices from IotHub and sync to graph db ...");
                        Console.WriteLine();

                        IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
                        string iotHubName = csb.HostName.Substring(0, csb.HostName.IndexOf(".azure-devices.net"));

                        using (var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString))
                        {
                            await new Program().RunSyncSampleAsync(iotHubName, registryManager, documentClient, graph);

                            WaitAnyKeyPressedToExit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void WaitAnyKeyPressedToExit()
        {
            Console.WriteLine();
            Console.WriteLine("*************************************");
            Console.WriteLine("* Press any key to exit at any time *");
            Console.WriteLine("*************************************");
            Console.WriteLine();
            Console.ReadKey();
        }

        // usage: <deviceId> <temperature>
        static bool ParseArguments(string[] args, out string action)
        {
            action = null;
            if (args.Length > 1)
            {
                PrintHelp("Wrong number of arguments.");
                return false;
            }

            action = "notifications";
            if (args.Length == 1)
            {
                action = args[0];
            }

            switch (action)
            {
                case "sync":
                case "notifications":
                    return true;
                default:
                    PrintHelp($"Unrecognized action '{action}'.");
                    return false;
            }
        }

        static void PrintHelp(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("usage: SyncGraphDbApp [notifications]  - will consume twin notifications to sync changes to graph db");
            Console.WriteLine("       SyncGraphDbApp sync             - will query all the devices and create or update corresponding entities in graph db");
        }

        async Task RunNotificationsSampleAsync(EventProcessorHost eventProcessorHost, DocumentClient documentClient, DocumentCollection graph)
        {
            var eventProcessorOptions = new EventProcessorOptions();
            var eventProcessorFactory = new TwinChangeEventProcessorFactory(documentClient, graph);
            await eventProcessorHost.RegisterEventProcessorFactoryAsync(eventProcessorFactory, eventProcessorOptions);
        }

        async Task RunSyncSampleAsync(string iotHubName, RegistryManager registryManager, DocumentClient documentClient, DocumentCollection graph)
        {
            int pageSize = 100;
            IQuery query = registryManager.CreateQuery("select * from devices", pageSize);
            while (query.HasMoreResults)
            {
                IEnumerable<Twin> twins = await query.GetNextAsTwinAsync();
                foreach (Twin twin in twins)
                {
                    Console.WriteLine("----------------------------------------");
                    Console.WriteLine($"Synchronize thermostat {twin.DeviceId}:");

                    string jTwin = twin.ToJson();
                    await new CreateOrUpdateTwinSyncCommand(documentClient, graph, iotHubName, twin.DeviceId, JToken.Parse(jTwin)).RunAsync();

                    Console.WriteLine();
                }
            }
        }

        static async Task CreateTopologyGraphAsync(DocumentClient documentClient, DocumentCollection graph)
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

                Console.WriteLine("===================== Graph created. =====================");
            }
        }
    }
}
