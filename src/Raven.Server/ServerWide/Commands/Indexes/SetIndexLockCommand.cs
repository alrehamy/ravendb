﻿using Raven.Client.Documents.Indexes;
using Raven.Client.ServerWide;
using Sparrow.Json.Parsing;

namespace Raven.Server.ServerWide.Commands.Indexes
{
    public class SetIndexLockCommand : UpdateDatabaseCommand
    {
        public string IndexName;

        public IndexLockMode LockMode;

        public SetIndexLockCommand() : base(null)
        {
            // for deserialization
        }

        public SetIndexLockCommand(string name, IndexLockMode mode, string databaseName)
            : base(databaseName)
        {
            IndexName = name;
            LockMode = mode;
        }

        public override string UpdateDatabaseRecord(DatabaseRecord record, long etag)
        {
            if (record.Indexes.TryGetValue(IndexName, out IndexDefinition staticIndex))
            {
                staticIndex.LockMode = LockMode;
                staticIndex.Etag = etag;
            }

            if (record.AutoIndexes.TryGetValue(IndexName, out AutoIndexDefinition autoIndex))
            {
                autoIndex.LockMode = LockMode;
                autoIndex.Etag = etag;
            }

            return null;
        }

        public override void FillJson(DynamicJsonValue json)
        {
            json[nameof(IndexName)] = IndexName;
            json[nameof(LockMode)] = LockMode;
        }
    }
}