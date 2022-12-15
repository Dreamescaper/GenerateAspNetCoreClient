using System;
using System.Collections.Generic;
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
        private readonly List<string> frameworkPaths;
        private readonly Assembly sharedAssemply;

        public CustomLoadContext(string assemblyPath, Assembly sharedAssembly)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyPath);
            frameworkPaths = GetFrameworkPaths(assemblyPath);
            sharedAssemply = sharedAssembly;
        }

        public Assembly Load(AssemblyName assemblyName, bool fallbackToDefault)
        {
            if (assemblyName.FullName == sharedAssemply.FullName)
                return sharedAssemply;
            var path = assemblyName.Name.StartsWith("System.Private") ? null : ResolveAssemblyToPath(assemblyName);

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

            foreach (var frameworkPath in frameworkPaths)
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

        private static List<string> GetFrameworkPaths(string assemblyPath)
        {
            // Unfortunately, AssemblyDependencyResolver does not resolve framework references.
            // In case of matching TargetFramework for generator tool and web project we can simply fallback to 
            // AssemblyLoadContext.Default, but that doesn't work for older ASP.NET versions (e.g. .NET Core 3.1).
            // Therefore we attempt to resolve framework assembly path manually - by parsing runtimeconfig.json, and finding
            // framework folder.
            // Any better ideas?...

            var paths = new List<string>();

            try
            {
                var runtimeConfigPath = Path.Combine(Path.GetDirectoryName(assemblyPath),
                    Path.GetFileNameWithoutExtension(assemblyPath) + ".runtimeconfig.json");

                if (!File.Exists(runtimeConfigPath))
                    return null;

                var runtimeConfig = JsonDocument.Parse(File.ReadAllText(runtimeConfigPath));

                var runtimeOptionsNode = runtimeConfig.RootElement.GetProperty("runtimeOptions");

                IEnumerable<JsonElement> frameworkNodes = runtimeOptionsNode.TryGetProperty("frameworks", out var frameworksNode)
                    ? frameworksNode.EnumerateArray()
                    : new[] { runtimeOptionsNode.GetProperty("framework") };

                foreach (var frameworkNode in frameworkNodes)
                {
                    // e.g. name = Microsoft.AspNetCore.App, version = 3.1.0.
                    var name = frameworkNode.GetProperty("name").GetString();
                    var version = frameworkNode.GetProperty("version").GetString();
                    var path = FindFrameworkVersionPath(name, version);

                    if (path != null)
                        paths.Add(path);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to find framework directory: " + e.ToString());
            }

            return paths;

            static string FindFrameworkVersionPath(string name, string version)
            {
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

                    version = version[..(version[0..^1].LastIndexOf('.') + 1)];
                }

                return null;
            }
        }
    }
}
