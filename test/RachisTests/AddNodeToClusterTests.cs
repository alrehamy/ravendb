﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FastTests.Server.Replication;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Http;
using Raven.Client.ServerWide;
using Raven.Server.Rachis;
using Raven.Server.Utils;
using Raven.Tests.Core.Utils.Entities;
using Sparrow.Utils;
using Tests.Infrastructure;
using Xunit;

namespace RachisTests
{
    public class AddNodeToClusterTests : ReplicationTestsBase
    {
        [Fact]
        public async Task FailOnAddingNonPassiveNode()
        {
            var raft1 = await CreateRaftClusterAndGetLeader(1);
            var raft2 = await CreateRaftClusterAndGetLeader(1);
            
            var url = raft2.WebUrls[0];
            await raft1.ServerStore.AddNodeToClusterAsync(url);
            Assert.True(await WaitForValueAsync(() => raft1.ServerStore.GetClusterErrors().Count > 0,true));
        }


        [Fact]
        public async Task RemoveNodeWithDb()
        {
            DebuggerAttachedTimeout.DisableLongTimespan = true;
            var fromSeconds = TimeSpan.FromSeconds(5);

            var leader = await CreateRaftClusterAndGetLeader(5);
            var db = await CreateDatabaseInCluster("MainDB", 5, leader.WebUrls[0]);
            var watcherDb = await CreateDatabaseInCluster("WatcherDB", 1, leader.WebUrls[0]);
            
            var leaderStore = new DocumentStore
            {
                Database = "MainDB",
                Urls = leader.WebUrls
            }.Initialize();
            leaderStore.Conventions.DisableTopologyUpdates = true;

            var watcherStore = new DocumentStore
            {
                Database = "WatcherDB",
                Urls = watcherDb.Item2.Single().WebUrls
            }.Initialize();
            watcherStore.Conventions.DisableTopologyUpdates = true;

            var watcher = new ExternalReplication
            {
                Database = "WatcherDB",
                Url = watcherDb.Item2.Single().WebUrls[0]
            };

            var watcherRes = await AddWatcherToReplicationTopology((DocumentStore)leaderStore, watcher);
            var tasks = new List<Task>();
            foreach (var ravenServer in Servers)
            {
                tasks.Add(ravenServer.ServerStore.Cluster.WaitForIndexNotification(watcherRes.RaftCommandIndex));
            }
            Assert.True(await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)));

            var responsibleServer = Servers.Single(s => s.ServerStore.NodeTag == watcherRes.ResponsibleNode);

            var responsibleStore = new DocumentStore
            {
                Database = "MainDB",
                Urls = responsibleServer.WebUrls
            }.Initialize();
            responsibleStore.Conventions.DisableTopologyUpdates = true;

            var serverNodes = db.Item2.Select(s => new ServerNode
            {
                ClusterTag = s.ServerStore.NodeTag,
                Database = "MainDB",
                Url = s.WebUrls[0]
            }).ToList();

            // check that replication works.
            using (var session = leaderStore.OpenSession())
            {
                session.Store(new User
                {
                    Name = "Karmel"
                }, "users/1");
                session.SaveChanges();
            }

            Assert.True(await WaitForDocumentInClusterAsync<User>(serverNodes, "users/1", u => u.Name == "Karmel", fromSeconds));
            Assert.True(WaitForDocument<User>(watcherStore, "users/1", u => u.Name == "Karmel", 30_000));

            // remove the node from the cluster that is responsible for the external replication
            Assert.True(await leader.ServerStore.RemoveFromClusterAsync(watcherRes.ResponsibleNode).WaitAsync(fromSeconds));
            Assert.True(await responsibleServer.ServerStore.WaitForState(RachisConsensus.State.Passive).WaitAsync(fromSeconds));

            // replication from the removed node should be suspended
            using (var session = responsibleStore.OpenAsyncSession())
            {
                await session.StoreAsync(new User
                {
                    Name = "Karmel2"
                }, "users/2");
                await session.SaveChangesAsync();
            }
            var nodeInCluster = serverNodes.First(s => s.ClusterTag != responsibleServer.ServerStore.NodeTag);
            var nodeInClusterStore = new DocumentStore
            {
                Database = "MainDB",
                Urls = new []{nodeInCluster.Url }
            }.Initialize();
            nodeInClusterStore.Conventions.DisableTopologyUpdates = true;

            Assert.False(WaitForDocument<User>(nodeInClusterStore, "users/2", u => u.Name == "Karmel2"));
            Assert.False(WaitForDocument<User>(watcherStore, "users/2", u => u.Name == "Karmel2"));

            // the task should be reassinged within to another node
            using (var session = nodeInClusterStore.OpenSession())
            {
                session.Store(new User
                {
                    Name = "Karmel2"
                }, "users/3");
                session.SaveChanges();
            }
            Assert.True(WaitForDocument<User>(watcherStore, "users/3", u => u.Name == "Karmel2", 30_000));

            // rejoin the node
            var newLeader = Servers.Single(s => s.ServerStore.IsLeader());
            Assert.True(await newLeader.ServerStore.AddNodeToClusterAsync(responsibleServer.WebUrls[0], watcherRes.ResponsibleNode).WaitAsync(fromSeconds));
            Assert.True(await responsibleServer.ServerStore.WaitForState(RachisConsensus.State.Follower).WaitAsync(fromSeconds));

            using (var session = responsibleStore.OpenSession())
            {
                session.Store(new User
                {
                    Name = "Karmel4"
                }, "users/4");
                session.SaveChanges();
                Assert.True(await WaitForDocumentInClusterAsync<User>(serverNodes, "users/4", u => u.Name == "Karmel4", fromSeconds));
            }

            Assert.True(WaitForDocument<User>(watcherStore, "users/4", u => u.Name == "Karmel4", 30_000));

            nodeInClusterStore.Dispose();
            leaderStore.Dispose();
            watcherStore.Dispose();
            responsibleStore.Dispose();
        }
    }
}
