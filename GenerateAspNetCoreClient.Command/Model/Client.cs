using System.Collections.Generic;

namespace GenerateAspNetCoreClient.Command.Model
{
    public class Client
    {
        /// <summary>
        /// Relative location from target folder.
        /// </summary>
        public string Location { get; }
        public string Namespace { get; }
        public string AccessModifier { get; }
        public string Name { get; }
        public IReadOnlyList<EndpointMethod> EndpointMethods { get; }

        public Client(
            string location,
            string @namespace, string accessModifier,
            string name,
            IReadOnlyList<EndpointMethod> endpointMethods)
        {
            Location = location;
            Namespace = @namespace;
            AccessModifier = accessModifier;
            Name = name;
            EndpointMethods = endpointMethods;
        }
    }
}
