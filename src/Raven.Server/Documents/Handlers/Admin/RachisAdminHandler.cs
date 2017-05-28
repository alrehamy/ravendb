﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Session;
using Raven.Client.Http;
using Raven.Server.Extensions;
using Raven.Server.Rachis;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.Handlers.Admin
{
    public class RachisAdminHandler : AdminRequestHandler
    {
        [RavenAction("/rachis/send", "POST", "/rachis/send")]
        public async Task ApplyCommand()
        {
            TransactionOperationContext context;
            using (ServerStore.ContextPool.AllocateOperationContext(out context))
            {
                var command = await context.ReadForMemoryAsync(RequestBodyStream(), "ExternalRachisCommand").ThrowOnTimeout();

                if (command.TryGet("Type", out string _) == false)
                {
                    // TODO: maybe add further validation?
                    throw new ArgumentException("Received command must contain a Type field");
                }

                var (etag, result) = await ServerStore.PutCommandAsync(command).ThrowOnTimeout();
                HttpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    context.Write(writer, new DynamicJsonValue
                    {
                        ["ETag"] = etag,
                        ["Data"] = result
                    });
                    writer.Flush();
                }
            }
        }

        [RavenAction("/admin/cluster/topology", "GET", "/admin/cluster/topology")]
        public Task GetClusterTopology()
        {
            TransactionOperationContext context;
            using (ServerStore.ContextPool.AllocateOperationContext(out context))
            using(context.OpenReadTransaction())
            {
                
                var topology = ServerStore.GetClusterTopology(context);
                var nodeTag = ServerStore.NodeTag;

                if (topology.Members.Count == 0)
                {
                    var serverUrl = GetStringQueryString("url");
                    topology = new ClusterTopology(
                        Guid.NewGuid().ToString(),
                        null,
                        new Dictionary<string, string>
                        {
                            ["A"] = serverUrl
                        },
                        new Dictionary<string, string>(),
                        new Dictionary<string, string>(),
                        "A"
                    );
                    nodeTag = "A";
                }
                HttpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                
                var blit = EntityToBlittable.ConvertEntityToBlittable(topology, DocumentConventions.Default, context);
                
                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    var json = new DynamicJsonValue
                    {
                        ["Topology"] = blit,
                        ["Leader"] = ServerStore.LeaderTag,
                        ["CurrentState"] = ServerStore.CurrentState,
                        ["NodeTag"] = nodeTag
                    };
                    var clusterErrors = ServerStore.GetClusterErrors();
                    if (clusterErrors.Count > 0)
                        json["Errors"] = clusterErrors;

                    context.Write(writer, json);
                    writer.Flush();
                }
            }
            return Task.CompletedTask;
        }

        [RavenAction("/cluster/maintenance-stats", "Get", "/cluster/maintenance-stats")]
        public Task ClusterMaintenanceStats()
        {
            if (ServerStore.LeaderTag == null)
            {
                return Task.CompletedTask;
            }
            using (ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
            {
                if (ServerStore.IsLeader())
                {
                    context.Write(writer, DynamicJsonValue.Convert(ServerStore.ClusterMaintenanceSupervisor?.GetStats()));
                    writer.Flush();
                    return Task.CompletedTask;
                }
                // redirect to leader
                ClusterTopology topology;
                using (context.OpenReadTransaction())
                {
                    topology = ServerStore.GetClusterTopology(context);
                }
                var url = topology.GetUrlFromTag(ServerStore.LeaderTag);
                HttpContext.Response.Redirect(Path.Combine(url,"cluster/maintenance-stats"));
            }
            return Task.CompletedTask;
        }

        [RavenAction("/admin/cluster/add-node", "POST", "/admin/cluster/add-node?url={nodeUrl:string}")]
        public async Task AddNode()
        {
            var serverUrl = GetStringQueryString("url");
            ServerStore.EnsureNotPassive();
            await ServerStore.AddNodeToClusterAsync(serverUrl).ThrowOnTimeout();

            NoContentStatus();
        }

        [RavenAction("/admin/cluster/remove-node", "DELETE", "/admin/cluster/remove-node?nodeTag={nodeTag:string}")]
        public async Task DeleteNode()
        {
            var serverUrl = GetStringQueryString("nodeTag");
            ServerStore.EnsureNotPassive();
            await ServerStore.RemoveFromClusterAsync(serverUrl).ThrowOnTimeout();

            NoContentStatus();
        }
    }
}