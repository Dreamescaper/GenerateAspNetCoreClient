using CommandLine;

namespace Options
{
    public class GenerateClientOptions
    {
        [Value(0, Required = true, HelpText = "Relative path for input web assembly.")]
        public string InputPath { get; set; } = "";

        [Option('o', "out-path", Required = true, HelpText = "Relative out path for generated files.")]
        public string OutPath { get; set; } = "";

        [Option('n', "namespace", Required = true, HelpText = "Namespace for generated client types.")]
        public string Namespace { get; set; } = "Client";

        [Option("exclude-types", Required = false, HelpText = "Exclude all controller types with substring in full name.")]
        public string? ExcludeTypes { get; set; }

        [Option("exclude-paths", Required = false, HelpText = "Exclude all endpoints with substring in relative path.")]
        public string? ExcludePaths { get; set; }
    }
}