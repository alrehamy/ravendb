﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastTests;
using FastTests.Server.Basic.Entities;
using Raven.Client.Documents.Subscriptions;
using Raven.Tests.Core.Utils.Entities;
using SlowTests.Server.Documents.Notifications;
using Xunit;

namespace SlowTests.Core.Subscriptions
{
    public class RavenDB_3193 : RavenTestBase
    {
        [Fact]
        public async Task ShouldRespectCollectionCriteria()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    for (int i = 0; i < 100; i++)
                    {
                        await session.StoreAsync(new Company());
                        await session.StoreAsync(new User());
                        await session.StoreAsync(new Address());
                    }

                    await session.SaveChangesAsync();
                }

                var subscriptionCreationParams = new SubscriptionCreationOptions
                {
                    Criteria = new SubscriptionCriteria("Users")
                };
                var id = await store.Subscriptions.CreateAsync(subscriptionCreationParams);

                using (var subscription = store.Subscriptions.Open(
                    new SubscriptionConnectionOptions(id){
                        MaxDocsPerBatch = 31
                    }))
                {
                    var docs = new List<dynamic>();

                    GC.KeepAlive(subscription.Run(batch =>
                    {
                        foreach (var item in batch.Items)
                        {
                            docs.Add(item);
                        }
                    }));
                    
                    SpinWait.SpinUntil(() => docs.Count >= 100, TimeSpan.FromSeconds(60));
                    Assert.Equal(100, docs.Count);
                    foreach (var doc in docs)
                    {
                        Assert.True(doc.Id.StartsWith("users/"));
                    }
                }
            }
        }
    }
}
