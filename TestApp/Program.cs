using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using CommandLine;
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
            var assemblyPath = Path.GetFullPath(options.InputPath);
            var directory = Path.GetDirectoryName(assemblyPath);

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "local");

            var previousBaseDirectory = AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
            typeof(AppContext).GetMethod("SetData").Invoke(null, new[] { "APP_CONTEXT_BASE_DIRECTORY", directory });

            try
            {
                var sharedOptionsAssembly = typeof(GenerateClientOptions).Assembly;
                var context = new CustomLoadContext(assemblyPath, sharedOptionsAssembly);
                AssemblyLoadContext.Default.Resolving += (_, name) => context.LoadInternal(name);

                var webProjectAssembly = context.LoadFromAssemblyPath(assemblyPath);
                var commandAssembly = context.LoadFromAssemblyPath(typeof(GenerateClientCommand.GenerateClientCommand).Assembly.Location);

                commandAssembly.GetTypes().First(t => t.Name == "GenerateClientCommand")
                    .GetMethod("Invoke")
                    .Invoke(null, new object[] { webProjectAssembly, options });
            }
            finally
            {
                typeof(AppContext).GetMethod("SetData").Invoke(null, new[] { "APP_CONTEXT_BASE_DIRECTORY", previousBaseDirectory });
            }
        }
    }
}
