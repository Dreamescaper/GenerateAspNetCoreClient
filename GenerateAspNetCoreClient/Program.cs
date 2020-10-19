using CommandLine;
using DotNet.Cli.Build;
using GenerateAspNetCoreClient.Command;
using GenerateAspNetCoreClient.Options;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;

namespace GenerateAspNetCoreClient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default
                .ParseArguments<GenerateClientOptions>(args)
                .WithParsed(options => CreateClient(options));
        }

        internal static void CreateClient(GenerateClientOptions options)
        {
            var assemblyPath = GetAssemblyPath(options.InputPath);
            var directory = Path.GetDirectoryName(assemblyPath);

            var sharedOptionsAssembly = typeof(GenerateClientOptions).Assembly;

            var context = new CustomLoadContext(assemblyPath, sharedOptionsAssembly);
            AssemblyLoadContext.Default.Resolving += (_, name) => context.Load(name, false);

            var webProjectAssembly = context.LoadFromAssemblyPath(assemblyPath);
            var commandAssembly = context.LoadFromAssemblyPath(typeof(GenerateClientCommand).Assembly.Location);

            commandAssembly.GetTypes().First(t => t.Name == "GenerateClientCommand")
                .GetMethod("Invoke")
                .Invoke(null, new object[] { webProjectAssembly, options });
        }

        private static string GetAssemblyPath(string path)
        {
            if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                // If path is .dll file - return straight away 
                return Path.GetFullPath(path);
            }
            else
            {
                // Otherwise - publish the project and return built .dll
                var project = Project.FromPath(path);
                project.Publish();
                return project.PublishFilePath;
            }
        }
    }
}
