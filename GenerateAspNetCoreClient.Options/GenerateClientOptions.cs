using System;
using CommandLine;

namespace GenerateAspNetCoreClient.Options
{
    public class GenerateClientOptions
    {
        [Value(0, HelpText = "Relative path for input web assembly.")]
        public string InputPath { get; set; } = Environment.CurrentDirectory;

        [Option('o', "out-path", Required = true, HelpText = "Relative out path for generated files.")]
        public string OutPath { get; set; } = "";

        [Option('n', "namespace", Required = true, HelpText = "Namespace for generated client types.")]
        public string Namespace { get; set; } = "Client";

        [Option("type-name-pattern", Required = false, Default = "I[controller]Api", HelpText = "Pattern by which client types are named.")]
        public string TypeNamePattern { get; set; } = "I[controller]Api";

        [Option("access-modifier", Required = false, Default = "public", HelpText = "Access modifier used for generated clients.")]
        public string AccessModifier { get; set; } = "public";

        [Option("exclude-types", Required = false, HelpText = "Exclude all controller types with substring in full name.")]
        public string? ExcludeTypes { get; set; }

        [Option("exclude-paths", Required = false, HelpText = "Exclude all endpoints with substring in relative path.")]
        public string? ExcludePaths { get; set; }
    }
}