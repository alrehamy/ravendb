using System;

namespace Raven.Client.Documents.Session
{
    public interface ILoadConfiguration
    {
        /// <summary>
        /// Adds transformer parameter that will be passed to transformer on server-side.
        /// </summary>
        /// <param name="name">name of the parameter</param>
        /// <param name="value">value of the parameter</param>
        void AddTransformerParameter(string name, object value);

        void AddTransformerParameter(string name, DateTime value);
    }
}
