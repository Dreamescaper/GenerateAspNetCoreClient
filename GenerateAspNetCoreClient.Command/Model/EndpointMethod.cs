using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace GenerateAspNetCoreClient.Command.Model
{
    public class EndpointMethod
    {
        public string? XmlDoc { get; }
        public HttpMethod HttpMethod { get; }
        public string Path { get; }
        public Type ResponseType { get; }
        public string Name { get; }
        public IReadOnlyList<Parameter> Parameters { get; }

        public bool IsMultipart => Parameters.Any(p => p.Source == ParameterSource.File);

        public EndpointMethod(string? xmlDoc, HttpMethod httpMethod, string path, Type responseType, string name, IReadOnlyList<Parameter> parameters)
        {
            XmlDoc = xmlDoc;
            HttpMethod = httpMethod;
            Path = path;
            ResponseType = responseType;
            Name = name;
            Parameters = parameters;
        }
    }
}
