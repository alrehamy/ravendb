using System;
using SlowTests.Bugs;
using SlowTests.Issues;
using FastTests.Voron.Storage;
using SlowTests.Cluster;
using Raven.Server.Documents.Replication;
using Raven.Client.Documents;

namespace Tryouts
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using (var store = new DocumentStore
            {
                Urls = new string[] { "http://127.0.0.1:8080" },
                Database = "test"
            }.Initialize())
            {
                var sub = store.Subscriptions.Open(new Raven.Client.Documents.Subscriptions.SubscriptionConnectionOptions(11));
                sub.Run(batch =>
                {
                    foreach (var item in batch.Items)
                    {
                        Console.WriteLine(item.Id);
                    }
                }).Wait();
            }
        }

        private static void RunTest()
        {
            for (int i = 0; i < 100; i++)
            {
                Console.Clear();
                Console.WriteLine(i);
                using (var test = new FastTests.Client.Query())
                {
                    try
                    {
                        test.Query_By_Index();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        Console.Beep();
                        return;
                    }
                }
            }
        }
    }
}
