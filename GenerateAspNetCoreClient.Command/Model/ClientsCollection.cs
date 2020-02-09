using System;
using System.Collections;
using System.Collections.Generic;

namespace GenerateAspNetCoreClient.Command.Model
{
    public class ClientCollection : IEnumerable<Client>
    {
        public IReadOnlyList<Client> Clients { get; }

        /// <summary>
        /// Types that should be fully qualified, as multiple types with same name present
        /// </summary>
        public HashSet<Type> AmbiguousTypes { get; }

        public ClientCollection(IReadOnlyList<Client> clients, HashSet<Type> ambiguousTypes)
        {
            Clients = clients;
            AmbiguousTypes = ambiguousTypes;
        }

        public IEnumerator<Client> GetEnumerator()
        {
            return Clients.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Clients.GetEnumerator();
        }
    }
}
