using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using CommandLine;
using DotNet.Cli.Build;
using Options;

namespace TestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default
                .ParseArguments<GenerateClientOptions>(args)
                .WithParsed(options => CreateClient(options));
        }

        private static void CreateClient(GenerateClientOptions options)
        {
            var assemblyPath = GetAssemblyPath(options.InputPath);
            var directory = Path.GetDirectoryName(assemblyPath);

            var sharedOptionsAssembly = typeof(GenerateClientOptions).Assembly;
            var context = new CustomLoadContext(assemblyPath, sharedOptionsAssembly);
            AssemblyLoadContext.Default.Resolving += (_, name) => context.LoadInternal(name);

            var webProjectAssembly = context.LoadFromAssemblyPath(assemblyPath);
            var commandAssembly = context.LoadFromAssemblyPath(typeof(GenerateClientCommand.GenerateClientCommand).Assembly.Location);

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

            var project = Project.FromPath(path);
            project.Build();
            return project.OutputFilePath;
        }
    }
}
