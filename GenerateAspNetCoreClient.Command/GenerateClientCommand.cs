using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GenerateAspNetCoreClient.Command.Extensions;
using GenerateAspNetCoreClient.Command.Model;
using GenerateAspNetCoreClient.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GenerateAspNetCoreClient.Command
{
    public class GenerateClientCommand
    {
        public static void Invoke(Assembly assembly, GenerateClientOptions options)
        {
            var apiExplorer = GetApiExplorer(assembly, options.Environment);

            var clientModelBuilder = new ClientModelBuilder(apiExplorer, options,
                additionalNamespaces: new[] { "System.Threading.Tasks", "Refit" });
            var clientCollection = clientModelBuilder.GetClientCollection();

            foreach (var clientModel in clientCollection)
            {
                var clientText = CreateClient(clientModel, clientCollection.AmbiguousTypes);

                var path = Path.Combine(options.OutPath, clientModel.Location);
                Directory.CreateDirectory(path);

                File.WriteAllText(Path.Combine(path, $"{clientModel.Name}.cs"), clientText);
            }
        }

        private static ApiDescriptionGroupCollection GetApiExplorer(Assembly assembly, string? environment)
        {
            using var _ = new RunSettings(Path.GetDirectoryName(assembly.Location)!, environment);

            var entryType = assembly.EntryPoint?.DeclaringType;
            var hostBuilderMethod = entryType?.GetMethod("CreateHostBuilder");

            IServiceProvider? services = null;

            if (hostBuilderMethod != null)
            {
                var hostBuilder = hostBuilderMethod.Invoke(null, new[] { Array.Empty<string>() }) as IHostBuilder;
                hostBuilder?.ConfigureLogging(c => c.ClearProviders());
                var host = hostBuilder?.Build();
                services = host?.Services;
            }

            var webHostBuilderMethod = entryType?.GetMethod("CreateWebHostBuilder");
            if (webHostBuilderMethod != null)
            {
                var webHostBuilder = webHostBuilderMethod.Invoke(null, new[] { Array.Empty<string>() }) as IWebHostBuilder;
                webHostBuilder?.ConfigureLogging(c => c.ClearProviders());
                var webHost = webHostBuilder?.Build();
                services = webHost?.Services;
            }

            if (services == null)
            {
                throw new Exception("Entry class should have either 'CreateHostBuilder', or 'CreateWebHostBuilder' method");
            }

            var apiExplorerProvider = services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

            return apiExplorerProvider.ApiDescriptionGroups;
        }

        private static string CreateClient(Client clientModel, HashSet<Type> ambiguousTypes)
        {
            var methodDescriptions = clientModel.EndpointMethods.Select(endpointMethod =>
            {
                var xmlDoc = endpointMethod.XmlDoc;

                if (!string.IsNullOrEmpty(xmlDoc))
                    xmlDoc += Environment.NewLine;

                var multipartAttribute = endpointMethod.IsMultipart
                    ? "[Multipart]" + Environment.NewLine
                    : "";

                var parameterStrings = endpointMethod.Parameters
                    .OrderBy(p => p.DefaultValueLiteral != null)
                    .Select(p =>
                    {
                        var attribute = p.Source switch
                        {
                            ParameterSource.Body => "[Body] ",
                            ParameterSource.Form => "[Body(BodySerializationMethod.UrlEncoded)] ",
                            ParameterSource.Header => $"[Header(\"{p.Name}\")] ",
                            ParameterSource.Query when p.Type != typeof(string) && !p.Type.IsValueType => "[Query] ",
                            _ => ""
                        };

                        var type = p.Source == ParameterSource.File ? "MultipartItem" : p.Type.GetName(ambiguousTypes);
                        var defaultValue = p.DefaultValueLiteral == null ? "" : " = " + p.DefaultValueLiteral;
                        return $"{attribute}{type} {p.ParameterName}{defaultValue}";
                    })
                    .ToArray();

                var httpMethodAttribute = endpointMethod.HttpMethod.ToString().ToPascalCase();
                var methodPathAttribute = $@"[{httpMethodAttribute}(""/{endpointMethod.Path}"")]";

                return
    $@"{xmlDoc}{multipartAttribute}{methodPathAttribute}
{endpointMethod.ResponseType.WrapInTask().GetName(ambiguousTypes)} {endpointMethod.Name}({string.Join(", ", parameterStrings)});";
            }).ToArray();

            return
    $@"//<auto-generated />

{string.Join(Environment.NewLine, clientModel.ImportedNamespaces.Select(n => $"using {n};"))}

namespace {clientModel.Namespace}
{{
    {clientModel.AccessModifier} interface {clientModel.Name}
    {{
{string.Join(Environment.NewLine + Environment.NewLine, methodDescriptions).Indent("        ")}
    }}
}}";
        }

        private class RunSettings : IDisposable
        {
            private readonly string? environment;
            private readonly string? originalEnvironment;
            private readonly string originalCurrentDirectory;
            private readonly string? originalBaseDirectory;

            public RunSettings(string location, string? environment)
            {
                this.environment = environment;

                originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                originalCurrentDirectory = Directory.GetCurrentDirectory();
                originalBaseDirectory = AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY") as string;

                if (environment != null)
                    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

                // Update AppContext.BaseDirectory and Directory.CurrentDirectory, since they are often used for json files paths.
                SetAppContextBaseDirectory(location);
                Directory.SetCurrentDirectory(location);
            }

            public void Dispose()
            {
                if (environment != null)
                    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);

                SetAppContextBaseDirectory(originalBaseDirectory);
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }

            private static void SetAppContextBaseDirectory(string? path)
            {
                var setDataMethod = typeof(AppContext).GetMethod("SetData");

                if (setDataMethod != null)
                    setDataMethod.Invoke(null, new[] { "APP_CONTEXT_BASE_DIRECTORY", path });
            }
        }
    }
}