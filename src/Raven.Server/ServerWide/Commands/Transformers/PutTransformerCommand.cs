using Raven.Client.Documents.Transformers;
using Raven.Client.ServerWide;
using Raven.Server.Utils;
using Sparrow.Json.Parsing;

namespace Raven.Server.ServerWide.Commands.Transformers
{
    public class PutTransformerCommand : UpdateDatabaseCommand
    {
        public TransformerDefinition Definition;

        public PutTransformerCommand()
            : base(null)
        {
            // for deserialization
        }

        public PutTransformerCommand(TransformerDefinition definition, string databaseName)
            : base(databaseName)
        {
            Definition = definition;
        }

        public override string UpdateDatabaseRecord(DatabaseRecord record, long etag)
        {
            Definition.Etag = etag;
            record.AddTransformer(Definition);
            return null;
        }

        public override void FillJson(DynamicJsonValue json)
        {
            json[nameof(Definition)] = TypeConverter.ToBlittableSupportedType(Definition);
        }
    }
}