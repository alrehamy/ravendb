using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web.Http;
using Raven.Abstractions.Data;
using Raven.Abstractions.Logging;
using Raven.Abstractions.MEF;
using Raven.Database.Extensions;
using Raven.Database.FileSystem.Plugins;
using Raven.Database.Plugins;
using Raven.Database.Server.WebApi.Attributes;
using Raven.Imports.Newtonsoft.Json;
using Raven.Json.Linq;

namespace Raven.Database.FileSystem.Controllers
{
    public class FilesStreamsController : BaseFileSystemApiController
    {
        private static readonly ILog log = LogManager.GetCurrentClassLogger();


        [HttpGet]
        [RavenRoute("fs/{fileSystemName}/streams/files")]
        public HttpResponseMessage StreamFilesGet()
        {
            var etag = GetEtagFromQueryString();
            var pageSize = GetPageSize(int.MaxValue);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new PushStreamContent((stream, content, transportContext) => StreamToClient(stream, pageSize, etag, FileSystem.ReadTriggers))
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" } }
                }
            };
        }

        private void StreamToClient(Stream stream, int pageSize, Etag etag, OrderedPartCollection<AbstractFileReadTrigger> readTriggers)
        {
            using (var cts = new CancellationTokenSource())
            using (var timeout = cts.TimeoutAfter(FileSystemsLandlord.SystemConfiguration.Core.DatabaseOperationTimeout.AsTimeSpan))
            using (var writer = new JsonTextWriter(new StreamWriter(stream)))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Results");
                writer.WriteStartArray();

                Storage.Batch(accessor =>
                {
                    var returnedCount = 0;

                    while (true)
                    {
                        var files = accessor.GetFilesAfter(etag, pageSize);

                        var fileCount = 0;

                        foreach (var file in files)
                        {
                            fileCount++;

                            cts.Token.ThrowIfCancellationRequested();

                            etag = file.Etag;

                            if (readTriggers.CanReadFile(file.FullPath, file.Metadata, ReadOperation.Load) == false)
                                continue;

                            timeout.Delay();

                            var doc = RavenJObject.FromObject(file);
                            doc.WriteTo(writer);
                            writer.WriteRaw(Environment.NewLine);

                            returnedCount++;
                        }

                        if (fileCount == 0)
                            break;

                        if (returnedCount == pageSize)
                            break;
                    }
                });

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();
            }
        }


        [HttpGet]
        [RavenRoute("fs/{fileSystemName}/streams/query")]
        public HttpResponseMessage StreamQueryGet(string query, [FromUri] string[] sort)
        {
            var start = Paging.Start;
            var pageSize = GetPageSize(int.MaxValue);

            int _;
            long __;

            var files = Search.Query(query, sort, start, pageSize, out _, out __);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new PushStreamContent((stream, content, transportContext) => StreamQueryResultsToClient(stream, files, FileSystem.ReadTriggers))
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" } }
                }
            };
        }

        private void StreamQueryResultsToClient(Stream stream, string[] files, OrderedPartCollection<AbstractFileReadTrigger> readTriggers)
        {
            using (var cts = new CancellationTokenSource())
            using (var timeout = cts.TimeoutAfter(FileSystemsLandlord.SystemConfiguration.Core.DatabaseOperationTimeout.AsTimeSpan))
            using (var writer = new JsonTextWriter(new StreamWriter(stream)))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Results");
                writer.WriteStartArray();

                Storage.Batch(accessor =>
                {
                    foreach (var filename in files)
                    {
                        var fileHeader = accessor.ReadFile(filename);

                        if (fileHeader == null)
                            continue;

                        if (readTriggers.CanReadFile(fileHeader.FullPath, fileHeader.Metadata, ReadOperation.Load) == false)
                            continue;

                        timeout.Delay();
                        var doc = RavenJObject.FromObject(fileHeader);
                        doc.WriteTo(writer);

                        writer.WriteRaw(Environment.NewLine);
                    }
                });

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();
            }
        }
    }
}
