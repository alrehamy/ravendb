using System;
using System.Collections.Generic;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Transformers;
using Sparrow.Json;
using Sparrow.Logging;

namespace Raven.Client.Documents.Session.Operations
{
    internal class LoadStartingWithOperation
    {
        private static readonly Logger Logger = LoggingSource.Instance.GetLogger<LoadStartingWithOperation>("Raven.NewClient.Client");
        private readonly InMemoryDocumentSessionOperations _session;

        private string _startWith;
        private string _matches;
        private int _start;
        private int _pageSize;
        private string _exclude;
        private string _startAfter;

        private string _transformer;
        private Dictionary<string, object> _transformerParameters;

        private readonly List<string> _returnedIds = new List<string>();

        public LoadStartingWithOperation(InMemoryDocumentSessionOperations session)
        {
            _session = session;
        }

        public GetDocumentCommand CreateRequest()
        {
            _session.IncrementRequestCount();
            if (Logger.IsInfoEnabled)
                Logger.Info($"Requesting documents with ids starting with '{_startWith}' from {_session.StoreIdentifier}");

            return new GetDocumentCommand(_startWith, _startAfter, _matches, _exclude, _transformer, _transformerParameters, _start, _pageSize);
        }

        public void WithStartWith(string idPrefix, string matches = null, int start = 0, int pageSize = 25,
            string exclude = null, Action<ILoadConfiguration> configure = null, string startAfter = null)
        {
            _startWith = idPrefix;
            _matches = matches;
            _start = start;
            _pageSize = pageSize;
            _exclude = exclude;
            _startAfter = startAfter;
        }

        public void WithTransformer(string transformer, Dictionary<string, object> transformerParameters)
        {
            _transformer = transformer;
            _transformerParameters = transformerParameters;
        }

        public void SetResult(GetDocumentResult result)
        {
            // We don't want to track transformed entities.
            if (_transformer != null)
                return;

            foreach (BlittableJsonReaderObject document in result.Results)
            {
                var newDocumentInfo = DocumentInfo.GetNewDocumentInfo(document);
                _session.DocumentsById.Add(newDocumentInfo);
                _returnedIds.Add(newDocumentInfo.Id);
            }
        }

        public T[] GetDocuments<T>()
        {
            var i = 0;
            var finalResults = new T[_returnedIds.Count];
            foreach (var id in _returnedIds)
            {
                finalResults[i++] = GetDocument<T>(id);
            }
            return finalResults;
        }

        private T GetDocument<T>(string id)
        {
            if (id == null)
                return default(T);

            if (_session.IsDeleted(id))
                return default(T);

            DocumentInfo doc;
            if (_session.DocumentsById.TryGetValue(id, out doc))
                return _session.TrackEntity<T>(doc);

            return default(T);
        }

        public Dictionary<string, T> GetTransformedDocuments<T>(GetDocumentResult result)
        {
            if (result == null)
                return null;

            return TransformerHelper.ParseResultsForLoadOperation<T>(_session, result);
        }
    }
}
