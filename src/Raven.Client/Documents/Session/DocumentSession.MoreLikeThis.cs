﻿//-----------------------------------------------------------------------
// <copyright file="DocumentSession.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Queries.MoreLikeThis;
using Raven.Client.Documents.Session.Operations;
using Raven.Client.Documents.Session.Tokens;
using Raven.Client.Documents.Transformers;

namespace Raven.Client.Documents.Session
{
    /// <summary>
    /// Implements Unit of Work for accessing the RavenDB server
    /// </summary>
    public partial class DocumentSession
    {
        public List<T> MoreLikeThis<T, TIndexCreator>(string documentId) where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (documentId == null)
                throw new ArgumentNullException(nameof(documentId));

            var index = new TIndexCreator();
            return MoreLikeThis<T>(new MoreLikeThisQuery { Query = CreateQuery(index.IndexName), DocumentId = documentId });
        }

        public List<T> MoreLikeThis<TTransformer, T, TIndexCreator>(string documentId, Parameters transformerParameters = null) where TTransformer : AbstractTransformerCreationTask, new() where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (documentId == null)
                throw new ArgumentNullException(nameof(documentId));

            var index = new TIndexCreator();
            var transformer = new TTransformer();

            return MoreLikeThis<T>(new MoreLikeThisQuery
            {
                Query = CreateQuery(index.IndexName),
                Transformer = transformer.TransformerName,
                TransformerParameters = transformerParameters
            });
        }

        public List<T> MoreLikeThis<TTransformer, T, TIndexCreator>(MoreLikeThisQuery query) where TTransformer : AbstractTransformerCreationTask, new() where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var index = new TIndexCreator();
            var transformer = new TTransformer();

            query.Query = CreateQuery(index.IndexName);
            query.Transformer = transformer.TransformerName;

            return MoreLikeThis<T>(query);
        }

        public List<T> MoreLikeThis<T>(string index, string documentId, string transformer = null, Parameters transformerParameters = null)
        {
            return MoreLikeThis<T>(new MoreLikeThisQuery
            {
                Query = CreateQuery(index),
                DocumentId = documentId,
                Transformer = transformer,
                TransformerParameters = transformerParameters
            });
        }

        public List<T> MoreLikeThis<T>(MoreLikeThisQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var operation = new MoreLikeThisOperation(this, query);

            var command = operation.CreateRequest();
            RequestExecutor.Execute(command, Context, sessionId: _clientSessionId);

            var result = command.Result;
            operation.SetResult(result);

            return operation.Complete<T>();
        }

        private static string CreateQuery(string indexName)
        {
            var fromToken = FromToken.Create(indexName, null);

            var sb = new StringBuilder();
            fromToken.WriteTo(sb);

            return sb.ToString();
        }
    }
}