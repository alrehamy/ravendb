using System;
using System.Collections.Generic;
using System.Linq;

using Raven.Abstractions.Data;
using Raven.Abstractions.Database.Smuggler.Common;
using Raven.Abstractions.Json;
using Raven.Imports.Newtonsoft.Json;
using Raven.Imports.Newtonsoft.Json.Linq;
using Raven.Json.Linq;

namespace Raven.Abstractions.Database.Smuggler.Database
{
    public class DatabaseSmugglerOptions
    {
        private int _batchSize;

        public DatabaseSmugglerOptions()
        {
            Filters = new List<FilterSetting>();
            ConfigureDefaultFilters();
            OperateOnTypes = DatabaseItemType.Indexes | DatabaseItemType.Documents | DatabaseItemType.Transformers;
            ShouldExcludeExpired = false;
            Limit = int.MaxValue;
            StartDocsDeletionEtag = StartDocsEtag = Etag.Empty;
            BatchSize = 16 * 1024;
        }

        public Etag StartDocsEtag { get; set; }

        public Etag StartDocsDeletionEtag { get; set; }

        public DatabaseItemType OperateOnTypes { get; set; }

        public int BatchSize
        {
            get { return _batchSize; }
            set
            {
                if (value < 1)
                    throw new InvalidOperationException("Batch size cannot be zero or a negative number");
                _batchSize = value;
            }
        }

        public bool IgnoreErrorsAndContinue { get; set; }

        public int Limit { get; set; }

        public bool ShouldExcludeExpired { get; set; }

        public List<FilterSetting> Filters { get; set; }

        public string TransformScript { get; set; }

        public bool SkipConflicted { get; set; }

        public bool StripReplicationInformation { get; set; }

        public bool ShouldDisableVersioningBundle { get; set; }

        public int MaxStepsForTransformScript { get; set; }

        public bool MatchFilters(RavenJObject document)
        {
            foreach (var filter in Filters)
            {
                bool anyRecords = false;
                bool matchedFilter = false;
                foreach (var tuple in document.SelectTokenWithRavenSyntaxReturningFlatStructure(filter.Path))
                {
                    if (tuple == null || tuple.Item1 == null)
                        continue;

                    anyRecords = true;

                    var val = tuple.Item1.Type == JTokenType.String
                                  ? tuple.Item1.Value<string>()
                                  : tuple.Item1.ToString(Formatting.None);
                    matchedFilter |= filter.Values.Any(value => string.Equals(val, value, StringComparison.OrdinalIgnoreCase)) ==
                                     filter.ShouldMatch;
                }

                if (filter.ShouldMatch == false && anyRecords == false) // RDBQA-7
                    return true;

                if (matchedFilter == false)
                    return false;
            }
            return true;
        }

        public bool ExcludeExpired(RavenJObject document, DateTime now)
        {
            var metadata = document.Value<RavenJObject>("@metadata");

            const string RavenExpirationDate = "Raven-Expiration-Date";

            // check for expired documents and exclude them if expired
            if (metadata == null)
            {
                return false;
            }
            var property = metadata[RavenExpirationDate];
            if (property == null)
                return false;

            DateTime dateTime;
            try
            {
                dateTime = property.Value<DateTime>();
            }
            catch (FormatException)
            {
                return false;
            }

            return dateTime < now;
        }

        public DatabaseSmugglerOptions Clone()
        {
            return new DatabaseSmugglerOptions
            {
                IgnoreErrorsAndContinue = IgnoreErrorsAndContinue,
                OperateOnTypes = OperateOnTypes,
                BatchSize = BatchSize,
                Limit = Limit,
                Filters = Filters,
                MaxStepsForTransformScript = MaxStepsForTransformScript,
                ShouldDisableVersioningBundle = ShouldDisableVersioningBundle,
                ShouldExcludeExpired = ShouldExcludeExpired,
                SkipConflicted = SkipConflicted,
                StartDocsDeletionEtag = StartDocsDeletionEtag,
                StartDocsEtag = StartDocsEtag,
                StripReplicationInformation = StripReplicationInformation,
                TransformScript = TransformScript
            };
        }

        private void ConfigureDefaultFilters()
        {
            // filter out encryption verification key document to enable import to encrypted db from encrypted db.
            Filters.Add(new FilterSetting
            {
                Path = "@metadata.@id",
                ShouldMatch = false,
                Values = { Constants.InResourceKeyVerificationDocumentName }
            });
        }
    }
}
