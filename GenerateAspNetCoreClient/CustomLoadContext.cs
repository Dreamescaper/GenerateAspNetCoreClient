using System.Reflection;
using System.Runtime.Loader;

namespace GenerateAspNetCoreClient
{
    internal class CustomLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver dependencyResolver;
        private readonly string assemblyPath;
        private readonly Assembly sharedAssemply;

        public CustomLoadContext(string assemblyPath, Assembly sharedAssemply)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyPath);
            this.assemblyPath = assemblyPath;
            this.sharedAssemply = sharedAssemply;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (assemblyName.FullName == sharedAssemply.FullName)
                return sharedAssemply;

            return LoadInternal(assemblyName);
        }

        internal Assembly LoadInternal(AssemblyName assemblyName)
        {
            var path = dependencyResolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }
    }
}
