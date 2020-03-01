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
using Microsoft.Extensions.Hosting;

namespace GenerateAspNetCoreClient.Command
{
    public class GenerateClientCommand
    {
        public static void Invoke(Assembly assembly, GenerateClientOptions options)
        {
            var apiExplorer = GetApiExplorer(assembly);

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

        private static ApiDescriptionGroupCollection GetApiExplorer(Assembly assembly)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "local");

            var previousBaseDirectory = AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY") as string;
            SetAppContextBaseDirectory(Path.GetDirectoryName(assembly.Location));

            try
            {
                var entryType = assembly.EntryPoint?.DeclaringType;
                var hostBuilderMethod = entryType?.GetMethod("CreateHostBuilder");

                IServiceProvider? services = null;

                if (hostBuilderMethod != null)
                {
                    var hostBuilder = hostBuilderMethod.Invoke(null, new[] { Array.Empty<string>() }) as IHostBuilder;
                    var host = hostBuilder?.Build();
                    services = host?.Services;
                }

                var webHostBuilderMethod = entryType?.GetMethod("CreateWebHostBuilder");
                if (webHostBuilderMethod != null)
                {
                    var webHostBuilder = webHostBuilderMethod.Invoke(null, new[] { Array.Empty<string>() }) as IWebHostBuilder;
                    var webHost = webHostBuilder?.Build();
                    services = webHost?.Services;
                }

                if (services == null)
                {
                    throw new Exception("Entry class should have either 'CreateHostBuilder', or 'CreateWebHostBuilder' method");
                }

                var apiExplorerProvider = (IApiDescriptionGroupCollectionProvider)services.GetService(typeof(IApiDescriptionGroupCollectionProvider));

                return apiExplorerProvider.ApiDescriptionGroups;
            }
            finally
            {
                SetAppContextBaseDirectory(previousBaseDirectory);
            }

            static void SetAppContextBaseDirectory(string? path)
            {
                var setDataMethod = typeof(AppContext).GetMethod("SetData");

                if (setDataMethod != null)
                    setDataMethod.Invoke(null, new[] { "APP_CONTEXT_BASE_DIRECTORY", path });
            }
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
                    .Select(p =>
                    {
                        var attribute = p.Source == ParameterSource.Body ? "[Body] " : "";
                        var type = p.Source == ParameterSource.File ? "MultipartItem" : p.Type.GetName(ambiguousTypes);
                        var defaultValue = p.DefaultValueLiteral == null ? "" : " = " + p.DefaultValueLiteral;
                        return $"{attribute}{type} {p.Name}{defaultValue}";
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
    }
}