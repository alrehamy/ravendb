﻿using System;
using System.Collections.Generic;
using Sparrow.Json.Parsing;

namespace Raven.Client.ServerWide
{
    public abstract class ConnectionString
    {
        public string Name { get; set; }

        public bool Validate(ref List<string> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            var count = errors.Count;

            ValidateImpl(ref errors);

            return count == errors.Count;
        }

        public abstract ConnectionStringType Type { get; }

        protected abstract void ValidateImpl(ref List<string> errors);

        public virtual DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(Name)] = Name
            };
        }
    }

    public enum ConnectionStringType
    {
        Raven,
        Sql
    }
}