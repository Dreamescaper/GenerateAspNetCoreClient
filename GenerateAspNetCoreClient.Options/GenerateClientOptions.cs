using CommandLine;

namespace GenerateAspNetCoreClient.Options
{
    public class GenerateClientOptions
    {
        [Value(0, HelpText = "Relative path for input web assembly.")]
        public string InputPath { get; set; } = System.Environment.CurrentDirectory;

        [Option('o', "out-path", Required = true, HelpText = "Relative out path for generated files.")]
        public string OutPath { get; set; } = "";

        [Option('n', "namespace", Required = true, HelpText = "Namespace for generated client types.")]
        public string Namespace { get; set; } = "Client";

        [Option("environment", Required = false, HelpText = "Required ASPNETCORE_ENVIRONMENT.")]
        public string? Environment { get; set; }

        [Option("type-name-pattern", Required = false, Default = "I[controller]Api", HelpText = "Pattern by which client types are named.")]
        public string TypeNamePattern { get; set; } = "I[controller]Api";

        [Option("access-modifier", Required = false, Default = "public", HelpText = "Access modifier used for generated clients.")]
        public string AccessModifier { get; set; } = "public";

        [Option("add-cancellation-token", Required = false, Default = false, HelpText = "Add CancellationToken parameters to all endpoints.")]
        public bool AddCancellationTokenParameters { get; set; }

        [Option("use-query-models", Required = false, Default = false, HelpText = "Use query container type parameter (as defined in the endpoint) instead of separate parameters.")]
        public bool UseQueryModels { get; set; }

        [Option("use-api-responses", Required = false, Default = false, HelpText = "Use Task<IApiResponse<T>> return types for endpoints.")]
        public bool UseApiResponses { get; set; }

        [Option("exclude-types", Required = false, HelpText = "Exclude all controller types with substring in full name (including namespace).")]
        public string? ExcludeTypes { get; set; }

        [Option("exclude-paths", Required = false, HelpText = "Exclude all endpoints with substring in relative path.")]
        public string? ExcludePaths { get; set; }

        [Option("include-types", Required = false, HelpText = "Include only controller types with substring in full name (including namespace).")]
        public string? IncludeTypes { get; set; }

        [Option("include-paths", Required = false, HelpText = "Include only endpoints with substring in relative path.")]
        public string? IncludePaths { get; set; }

        public string? BuildExtensionsDir { get; set; }

        public string[] AdditionalNamespaces { get; set; } = [];
    }
}