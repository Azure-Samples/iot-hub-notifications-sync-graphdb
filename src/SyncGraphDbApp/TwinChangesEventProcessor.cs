namespace SyncGraphDbApp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus.Messaging;
    using Newtonsoft.Json.Linq;

    class TwinChangesEventProcessor : IEventProcessor
    {
        readonly SyncCommandFactory syncCommandFactory;

        public TwinChangesEventProcessor(SyncCommandFactory syncCommandFactory)
        {
            this.syncCommandFactory = syncCommandFactory;
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Console.WriteLine("Processor Shutting Down. Partition '{0}', Reason: '{1}'.", context.Lease.PartitionId, reason);

            return Task.CompletedTask;
        }

        public Task OpenAsync(PartitionContext context)
        {
            Console.WriteLine("TwinChangesEventProcessor initialized.  Partition: '{0}', Offset: '{1}'", context.Lease.PartitionId, context.Lease.Offset);

            return Task.CompletedTask;
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            List<EventData> messagesList = messages.ToList();
            int lastSuccessfulIndex = -1;
            while (messagesList.Count > 0)
            {
                lastSuccessfulIndex = await this.SyncDataAsync(context, messagesList);

                await context.CheckpointAsync(messagesList[lastSuccessfulIndex]);

                // remove all succeeded messages from the list
                for (int i = 0; i < lastSuccessfulIndex + 1; i++)
                {
                    messagesList.RemoveAt(0);
                }
            }
        }

        async Task<int> SyncDataAsync(PartitionContext context, List<EventData> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                EventData eventData = messages[i];
                SyncCommandBase syncCommand = null;
                string hubName = (string)eventData.Properties["hubName"];
                string deviceId = (string)eventData.Properties["deviceId"];
                string messageSource = (string)eventData.SystemProperties["iothub-message-source"];
                string operationType = (string)eventData.Properties["opType"];
                JToken jPayload = JToken.Parse(Encoding.UTF8.GetString(eventData.GetBytes()));

                Console.WriteLine();
                Console.WriteLine("=========================================================================================================================================");
                Console.WriteLine("PROCESS EVENT");
                Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine($"Partition: {context.Lease.PartitionId}, Offset: {eventData.Offset}, HubName: {hubName}, DeviceId: {deviceId}, MessageSource: {messageSource} ...");
                Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine($"Operation type '{operationType}'");
                Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("Body:");
                Console.WriteLine(jPayload.ToString(Newtonsoft.Json.Formatting.Indented));
                Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------------------");

                switch (messageSource)
                {
                    case "deviceLifecycleEvents":
                        syncCommand = this.GetDeviceLifecycleSyncCommand(eventData, operationType, jPayload, hubName, deviceId);
                        break;
                    case "twinChangeEvents":
                        syncCommand = this.GetTwinChangeSyncCommand(eventData, operationType, jPayload, hubName, deviceId);
                        break;
                    default:
                        Console.WriteLine($"Message source '{messageSource}' not supported");
                        break;
                }

                if (syncCommand != null)
                {
                    try
                    {
                        await syncCommand.RunAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"WARNING! Failed to sync message with offset {eventData.Offset}");
                        Console.WriteLine(ex.ToString());

                        return i - 1;
                    }
                }

                Console.WriteLine("=========================================================================================================================================");
            }

            return messages.Count - 1;
        }

        SyncCommandBase GetTwinChangeSyncCommand(EventData eventData, string operationType, JToken jPayload, string hubName, string deviceId)
        {
            SyncCommandBase syncCommand = null;
            switch (operationType)
            {
                case "updateTwin": // this is a patch
                    syncCommand = this.syncCommandFactory.UpdateTwinSyncCommand(hubName, deviceId, jPayload);
                    break;
                case "replaceTwin": // this contains full twin state
                    syncCommand = this.syncCommandFactory.ReplaceTwinSyncCommand(hubName, deviceId, jPayload);
                    break;
                default:
                    Console.WriteLine($"Operation type '{operationType}' not supported");
                    break;
            }

            return syncCommand;
        }

        SyncCommandBase GetDeviceLifecycleSyncCommand(EventData eventData, string operationType, JToken jPayload, string hubName, string deviceId)
        {
            SyncCommandBase syncCommand = null;
            switch (operationType)
            {
                case "createDeviceIdentity":
                    syncCommand = this.syncCommandFactory.CreateDeviceIdentitySyncCommand(hubName, deviceId, jPayload);
                    break;
                case "deleteDeviceIdentity":
                    syncCommand = this.syncCommandFactory.DeleteDeviceIdentitySyncCommand(hubName, deviceId, jPayload);
                    break;
                default:
                    Console.WriteLine($"Operation type '{operationType}' not supported");
                    break;
            }

            return syncCommand;
        }
    }
}
