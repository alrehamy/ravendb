﻿using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using Raven.Client.ServerWide.ETL;
using Raven.Server.Documents.Patch;
using Raven.Server.ServerWide.Context;

namespace Raven.Server.Documents.ETL
{
    public abstract class EtlTransformer<TExtracted, TTransformed> : DocumentPatcherBase where TExtracted : ExtractedItem
    {
        protected readonly DocumentsOperationContext Context;

        protected TExtracted Current;

        protected EtlTransformer(DocumentDatabase database, DocumentsOperationContext context) : base(database)
        {
            Context = context;
        }

        protected abstract string[] LoadToDestinations { get; }

        protected override void RemoveEngineCustomizations(Engine engine, PatcherOperationScope scope)
        {
            engine.Global.Delete(Transformation.LoadTo, true);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < LoadToDestinations.Length; i++)
            {
                engine.Global.Delete(Transformation.LoadTo + LoadToDestinations[i], true);
            }
        }

        protected override void CustomizeEngine(Engine engine, PatcherOperationScope scope)
        {
            engine.SetValue(Transformation.LoadTo, new Action<string, JsValue>((tableName, colsAsObject) => LoadToFunction(tableName, colsAsObject, scope)));

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < LoadToDestinations.Length; i++)
            {
                var collection = LoadToDestinations[i];
                engine.SetValue(Transformation.LoadTo + collection, (Action<JsValue>)(cols =>
                {
                    LoadToFunction(collection, cols, scope);
                }));
            }
        }

        protected abstract void LoadToFunction(string tableName, JsValue colsAsObject, PatcherOperationScope scope);

        public abstract IEnumerable<TTransformed> GetTransformedResults();

        public abstract void Transform(TExtracted item);

        public static void ThrowLoadParameterIsMandatory(string parameterName)
        {
            throw new ArgumentException($"{parameterName} parameter is mandatory");
        }
    }
}