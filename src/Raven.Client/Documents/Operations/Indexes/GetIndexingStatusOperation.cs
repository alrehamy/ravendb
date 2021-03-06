﻿using System.Net.Http;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Indexes;
using Raven.Client.Http;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Operations.Indexes
{
    public class GetIndexingStatusOperation : IAdminOperation<IndexingStatus>
    {
        public RavenCommand<IndexingStatus> GetCommand(DocumentConventions conventions, JsonOperationContext context)
        {
            return new GetIndexingStatusCommand();
        }

        private class GetIndexingStatusCommand : RavenCommand<IndexingStatus>
        {
            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/indexes/status";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                Result = JsonDeserializationClient.IndexingStatus(response);
            }

            public override bool IsReadRequest => true;
        }
    }
}