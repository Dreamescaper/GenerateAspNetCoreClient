using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GenerateAspNetCoreClient.Command.Extensions;
using GenerateAspNetCoreClient.Command.Model;
using GenerateAspNetCoreClient.Options;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace GenerateAspNetCoreClient.Command
{
    public class GenerateClientCommand
    {
        public static void Invoke(Assembly assembly, GenerateClientOptions options)
        {
            options.AdditionalNamespaces = ["System.Threading.Tasks", "Refit"];
            var apiExplorer = GetApiExplorer(assembly, options.Environment);
            var clientModelBuilder = new ClientModelBuilder(apiExplorer, options, assembly);
            var clientCollection = clientModelBuilder.GetClientCollection();

            var ambiguousTypes = GetAmbiguousTypes(clientCollection);

            foreach (var clientModel in clientCollection)
            {
                var clientText = CreateClient(clientModel, ambiguousTypes, options);

                var path = Path.Combine(options.OutPath, clientModel.Location);
                Directory.CreateDirectory(path);

                File.WriteAllText(Path.Combine(path, $"{clientModel.Name}.cs"), clientText);
            }
        }

        private static ApiDescriptionGroupCollection GetApiExplorer(Assembly assembly, string? environment)
        {
            using var _ = new RunSettings(Path.GetDirectoryName(assembly.Location)!, environment);

            var services = ServiceProviderResolver.GetServiceProvider(assembly);
            var apiExplorerProvider = services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

            return apiExplorerProvider.ApiDescriptionGroups;
        }

        private static string CreateClient(Client clientModel, HashSet<Type> ambiguousTypes, GenerateClientOptions options)
        {
            IEnumerable<EndpointMethod> endpointMethods = clientModel.EndpointMethods;
            endpointMethods = HandleEndpointDuplicates(endpointMethods, ambiguousTypes);
            endpointMethods = HandleSignatureDuplicates(endpointMethods, ambiguousTypes);
            var namespaces = GetImportedNamespaces(clientModel, ambiguousTypes, options);

            var methodDescriptions = endpointMethods.Select(endpointMethod =>
            {
                var xmlDoc = endpointMethod.XmlDoc;

                if (!string.IsNullOrEmpty(xmlDoc))
                    xmlDoc += Environment.NewLine;

                var multipartAttribute = endpointMethod.IsMultipart
                    ? "[Multipart]" + Environment.NewLine
                    : "";

                var staticHeaders = endpointMethod.Parameters.Where(p => p.Source == ParameterSource.Header && p.IsConstant).ToArray();
                var staticHeadersAttribute = staticHeaders.Length > 0
                    ? $"[Headers({string.Join(", ", staticHeaders.Select(h => $"\"{h.Name}: {h.DefaultValueLiteral!.Trim('"')}\""))})]" + Environment.NewLine
                    : "";

                var parameterStrings = endpointMethod.Parameters
                    .Except(staticHeaders)
                    .OrderBy(p => p.DefaultValueLiteral != null)
                    .Select(p =>
                    {
                        var attribute = p.Source switch
                        {
                            ParameterSource.Body => "[Body] ",
                            ParameterSource.Form when !endpointMethod.IsMultipart => "[Body(BodySerializationMethod.UrlEncoded)] ",
                            ParameterSource.Header => $"[Header(\"{p.Name}\")] ",
                            ParameterSource.Query => GetQueryAttribute(p),
                            _ => ""
                        };

                        string type;
                        if (p.Source == ParameterSource.File)
                        {
                            type = p.Type.IsArray || (p.Type.IsGenericType &&
                                                      p.Type.GetGenericTypeDefinition() == typeof(List<>))
                                ? "List<MultipartItem>"
                                : "MultipartItem";
                        }
                        else
                        {
                            type = p.Type.GetName(ambiguousTypes);
                        }

                        var defaultValue = p.DefaultValueLiteral == null ? "" : " = " + p.DefaultValueLiteral;
                        return $"{attribute}{type} {p.ParameterName}{defaultValue}";
                    })
                    .ToArray();

                var httpMethodAttribute = endpointMethod.HttpMethod.ToString().ToPascalCase();
                var methodPathAttribute = $@"[{httpMethodAttribute}(""/{endpointMethod.Path}"")]";

                var responseTypeName = GetResponseTypeName(endpointMethod.ResponseType, ambiguousTypes, options);

                return
    $@"{xmlDoc}{multipartAttribute}{staticHeadersAttribute}{methodPathAttribute}
{responseTypeName} {endpointMethod.Name}({string.Join(", ", parameterStrings)});";
            }).ToArray();

            return
    $@"//<auto-generated />

{string.Join(Environment.NewLine, namespaces.Select(n => $"using {n};"))}

namespace {clientModel.Namespace}
{{
    {clientModel.AccessModifier} partial interface {clientModel.Name}
    {{
{string.Join(Environment.NewLine + Environment.NewLine, methodDescriptions).Indent("        ")}
    }}
}}";
        }

        private static IEnumerable<string> GetImportedNamespaces(Client clientModel, HashSet<Type> ambiguousTypes, GenerateClientOptions options)
        {
            var namespaces = GetNamespaces(clientModel.EndpointMethods, ambiguousTypes)
                .Concat(options.AdditionalNamespaces);

            if (options.AddCancellationTokenParameters)
                namespaces = namespaces.Append("System.Threading");

            namespaces = namespaces
                .OrderByDescending(ns => ns.StartsWith("System"))
                .ThenBy(ns => ns);

            return namespaces;
        }

        private static string GetResponseTypeName(Type responseType, HashSet<Type> ambiguousTypes, GenerateClientOptions options)
        {
            if (options.UseApiResponses)
            {
                return responseType == typeof(void)
                    ? "Task<IApiResponse>"
                    : $"Task<IApiResponse<{responseType.GetName(ambiguousTypes)}>>";
            }
            else
            {
                return responseType.WrapInTask().GetName(ambiguousTypes);
            }
        }

        private static IEnumerable<EndpointMethod> HandleSignatureDuplicates(IEnumerable<EndpointMethod> endpointMethods, HashSet<Type> ambiguousTypes)
        {
            var dictionary = new Dictionary<string, EndpointMethod>();

            foreach (var endpointMethod in endpointMethods)
            {
                var parameterTypes = endpointMethod.Parameters.Where(p => !p.IsConstant).Select(p => p.Type.GetName(ambiguousTypes));
                var signatureDescription = $"{endpointMethod.Name}({string.Join(",", parameterTypes)})";

                if (dictionary.ContainsKey(signatureDescription))
                    Console.WriteLine("Duplicate API method " + signatureDescription);

                dictionary[signatureDescription] = endpointMethod;
            }

            return dictionary.Values;
        }

        private static IEnumerable<EndpointMethod> HandleEndpointDuplicates(IEnumerable<EndpointMethod> endpointMethods, HashSet<Type> ambiguousTypes)
        {
            var dictionary = new Dictionary<string, EndpointMethod>();

            foreach (var endpointMethod in endpointMethods)
            {
                var parameterDescriptions = endpointMethod.Parameters.Select(p => $"{p.Source} {p.Type.GetName(ambiguousTypes)} {p.Name} {(p.IsConstant ? ": " + p.DefaultValueLiteral : "")}");
                var endpointDescription = $"{endpointMethod.HttpMethod} {endpointMethod.Path} ({string.Join(", ", parameterDescriptions)})";

                if (dictionary.ContainsKey(endpointDescription))
                    Console.WriteLine("Duplicate API endpoint " + endpointDescription);

                dictionary[endpointDescription] = endpointMethod;
            }

            return dictionary.Values;
        }

        private static string GetQueryAttribute(Parameter parameter)
        {
            bool isKeyValuePairs = parameter.Type != typeof(string)
                && !parameter.Type.IsAssignableTo(typeof(IDictionary))
                && parameter.Type.IsAssignableTo(typeof(IEnumerable));

            if (parameter.Type != typeof(string) && !parameter.Type.IsValueType && !isKeyValuePairs)
                return "[Query] ";

            if (!string.Equals(parameter.Name, parameter.ParameterName, StringComparison.OrdinalIgnoreCase))
                return $"[AliasAs(\"{parameter.Name}\")] ";

            return "";
        }

        private static HashSet<Type> GetAmbiguousTypes(IEnumerable<Client> clients)
        {
            var allNamespaces = GetNamespaces(clients.SelectMany(c => c.EndpointMethods));

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a =>
                {
                    try
                    {
                        return a.ExportedTypes;
                    }
                    catch
                    {
                        return [];
                    }
                })
                .Where(t => t.DeclaringType == null && allNamespaces.Contains(t.Namespace!))
                .GroupBy(t => t.Name)
                .Where(g => g.Select(t => t.Namespace).Distinct().Count() > 1)
                .SelectMany(g => g)
                .ToHashSet();
        }

        private static HashSet<string> GetNamespaces(IEnumerable<EndpointMethod> apiDescriptions, HashSet<Type>? ambiguousTypes = null)
        {
            var namespaces = new HashSet<string>();

            foreach (var apiDescription in apiDescriptions)
            {
                AddForType(apiDescription.ResponseType);

                foreach (var parameterDescription in apiDescription.Parameters)
                {
                    AddForType(parameterDescription.Type);
                }
            }

            return namespaces;

            void AddForType(Type? type)
            {
                if (type != null && !type.IsBuiltInType() && ambiguousTypes?.Contains(type) != true)
                {
                    if (type.Namespace != null)
                        namespaces.Add(type.Namespace);

                    if (type.IsGenericType)
                    {
                        foreach (var typeArg in type.GetGenericArguments())
                            AddForType(typeArg);
                    }
                }
            }
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