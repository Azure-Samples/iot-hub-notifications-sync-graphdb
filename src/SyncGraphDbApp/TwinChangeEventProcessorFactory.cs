namespace SyncGraphDbApp
{
    using System.Threading;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.ServiceBus.Messaging;

    class TwinChangeEventProcessorFactory : IEventProcessorFactory
    {
        static int count = 0;

        readonly DocumentClient documentClient;
        readonly DocumentCollection graphCollection;

        public TwinChangeEventProcessorFactory(DocumentClient documentClient, DocumentCollection graphCollection)
        {
            this.documentClient = documentClient;
            this.graphCollection = graphCollection;
        }

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            Interlocked.Increment(ref count);
            return new TwinChangesEventProcessor(new SyncCommandFactory(this.documentClient, this.graphCollection));
        }
    }
}
