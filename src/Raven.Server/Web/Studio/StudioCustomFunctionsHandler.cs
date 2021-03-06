﻿using System.Threading.Tasks;
using Raven.Server.Documents;
using Raven.Server.Routing;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Web.Studio
{
    public class StudioCustomFunctionsHandler : DatabaseRequestHandler
    {
        [RavenAction("/databases/*/studio/custom-functions", "GET", AuthorizationStatus.ValidUser)]
        public Task GetCustomFunctions()
        {
            using (Database.ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
            using (context.OpenReadTransaction())
            {
                var dbrecord = Database.ServerStore.Cluster.ReadDatabase(context, Database.Name);

                var customFunctions = dbrecord?.CustomFunctions;

                /*
                 * Respond with null custom functions, instead of 404
                 * as this function is used by Studio
                 * Each 404 response goes to console as error, 
                 * so we don't want to scare user.
                 */

                using (var writer = new BlittableJsonTextWriter(context, ResponseBodyStream()))
                {
                    context.Write(writer, new DynamicJsonValue
                    {
                        [nameof(CustomFunctions.Functions)] = customFunctions
                    });
                }
            }

            return Task.CompletedTask;
        }

    }
}