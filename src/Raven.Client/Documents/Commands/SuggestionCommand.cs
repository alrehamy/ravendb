﻿using System;
using System.Net.Http;
using System.Text;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Queries.Suggestion;
using Raven.Client.Extensions;
using Raven.Client.Http;
using Raven.Client.Json;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Commands
{
    public class SuggestionCommand : RavenCommand<SuggestionQueryResult>
    {
        private readonly DocumentConventions _conventions;
        private readonly JsonOperationContext _context;
        private readonly SuggestionQuery _query;

        public SuggestionCommand(DocumentConventions conventions, JsonOperationContext context, SuggestionQuery query)
        {
            _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
        {
            var path = new StringBuilder(node.Url)
                .Append("/databases/")
                .Append(node.Database)
                .Append("/queries?op=suggest&query-hash=")
                .Append(_query.GetQueryHash(_context));

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content = new BlittableJsonContent(stream =>
                    {
                        using (var writer = new BlittableJsonTextWriter(_context, stream))
                        {
                            writer.WriteSuggestionQuery(_conventions, _context, _query);
                        }
                    }
                )
            };

            url = path.ToString();
            return request;
        }

        public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
        {
            if (response == null)
            {
                Result = null;
                return;
            }

            Result = JsonDeserializationClient.SuggestQueryResult(response);
        }

        public override bool IsReadRequest => true;
    }
}
