using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace GenerateAspNetCoreClient
{
    internal class CustomLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver dependencyResolver;
        private readonly string frameworkPath;
        private readonly Assembly sharedAssemply;

        public CustomLoadContext(string assemblyPath, Assembly sharedAssembly)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyPath);
            frameworkPath = GetFrameworkPath(assemblyPath);
            sharedAssemply = sharedAssembly;
        }

        public Assembly Load(AssemblyName assemblyName, bool fallbackToDefault)
        {
            if (assemblyName.FullName == sharedAssemply.FullName)
                return sharedAssemply;
            var path = ResolveAssemblyToPath(assemblyName);

            if (path == null && fallbackToDefault)
            {
                var defaultLoaded = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);

                if (defaultLoaded != null)
                    path = defaultLoaded.Location;
            }

            return path != null ? LoadFromAssemblyPath(path) : null;
        }

        private string ResolveAssemblyToPath(AssemblyName assemblyName)
        {
            var path = dependencyResolver.ResolveAssemblyToPath(assemblyName);

            if (path != null)
                return path;

            if (frameworkPath != null)
            {
                var frameworkAssemblyPath = Path.Combine(frameworkPath, assemblyName.Name + ".dll");

                if (File.Exists(frameworkAssemblyPath))
                    return frameworkAssemblyPath;
            }

            return null;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, true);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var resolvedPath = dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            return resolvedPath == null
                ? IntPtr.Zero
                : LoadUnmanagedDllFromPath(resolvedPath);
        }

        private static string GetFrameworkPath(string assemblyPath)
        {
            // Unfortunately, AssemblyDependencyResolver does not resolve framework references.
            // In case of matching TargetFramework for generator tool and web project we can simply fallback to 
            // AssemblyLoadContext.Default, but that doesn't work for older ASP.NET versions (e.g. .NET Core 3.1).
            // Therefore we attempt to resolve framework assembly path manually - by parsing runtimeconfig.json, and finding
            // framework folder.
            // Any better ideas?...

            try
            {
                var runtimeConfigPath = Path.Combine(Path.GetDirectoryName(assemblyPath),
                    Path.GetFileNameWithoutExtension(assemblyPath) + ".runtimeconfig.json");

                if (!File.Exists(runtimeConfigPath))
                    return null;

                var runtimeConfig = JsonDocument.Parse(File.ReadAllText(runtimeConfigPath));

                var framework = runtimeConfig.RootElement
                    .GetProperty("runtimeOptions")
                    .GetProperty("framework");

                // e.g. name = Microsoft.AspNetCore.App, version = 3.1.0.
                var name = framework.GetProperty("name").GetString();
                var version = framework.GetProperty("version").GetString();

                if(name != "Microsoft.AspNetCore.App")
                {
                    // Do not attempt to resolve Microsoft.NETCore.App references.
                    // This block is only expected to execute for ASP.Net 2.1 projects (before FrameworkReferences were introduced).
                    return null;
                }

                var sharedDirectoryPath =
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            Path.GetDirectoryName(typeof(object).Assembly.Location)));

                var frameworkVersionDirectories = new DirectoryInfo(Path.Combine(sharedDirectoryPath, name)).GetDirectories().Reverse();

                // Attempt to find strict match first, but fallback to fuzzy match (e.g. 3.1.12 instead of 3.1.0).
                while (!string.IsNullOrEmpty(version))
                {
                    var versionDirectory = frameworkVersionDirectories.FirstOrDefault(d => d.Name.StartsWith(version));

                    if (versionDirectory != null)
                        return versionDirectory.FullName;

                    version = version.Substring(0, version[0..^1].LastIndexOf('.') + 1);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to find framework directory: " + e.ToString());
            }

            return null;
        }
    }
}
