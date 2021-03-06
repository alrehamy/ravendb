// -----------------------------------------------------------------------
//  <copyright file="StorageActionsBase.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Raven.Abstractions.Extensions;
using Raven.Abstractions.Util.Streams;
using Raven.Database.FileSystem.Storage.Voron.Impl;
using Raven.Database.FileSystem.Util;

using System;

using Raven.Database.Util.Streams;
using Raven.Json.Linq;

using Voron;
using Voron.Impl;

namespace Raven.Database.FileSystem.Storage.Voron
{
    internal abstract class StorageActionsBase
    {
        private readonly Reference<SnapshotReader> snapshotReference;

        private readonly IBufferPool bufferPool;

        protected SnapshotReader Snapshot
        {
            get
            {
                return snapshotReference.Value;
            }
        }

        protected IdGenerator IdGenerator { get; private set; }

        protected StorageActionsBase(Reference<SnapshotReader> snapshotReference, IdGenerator idGenerator, IBufferPool bufferPool)
        {
            this.snapshotReference = snapshotReference;
            this.bufferPool = bufferPool;
            IdGenerator = idGenerator;
        }

        protected string CreateKey(params object[] values)
        {
            if (values == null || values.Length == 0)
                throw new InvalidOperationException("Cannot create an empty key.");

            if (values.Length == 1)
                return ConvertValueToString(values[0]);

            var sb = new StringBuilder();
            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                var valueAsString = ConvertValueToString(value);

                sb.Append(valueAsString);
                if (i < values.Length - 1)
                    sb.Append("/");
            }

            return sb.ToString();
        }

        protected RavenJObject LoadJson(Table table, Slice key, WriteBatch writeBatch, out ushort version)
        {
            var read = table.Read(Snapshot, key, writeBatch);
            if (read == null)
            {
                version = table.ReadVersion(Snapshot, key, writeBatch) ?? 0;
                return null;
            } 
            
            using (var stream = read.Reader.AsStream())
            {
                version = read.Version;
                return stream.ToJObject();
            }
        }

        protected BufferPoolMemoryStream CreateStream()
        {
            return new BufferPoolMemoryStream();
        }

        private static string ConvertValueToString(object value)
        {
            if (value is int)
                return ((int)value).ToString("D9");

            if (value is long)
                return ((long)value).ToString("D9");

            return value.ToString().ToLowerInvariant();
        }
    }
}
