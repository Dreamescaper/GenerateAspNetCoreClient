using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using GenerateAspNetCoreClient.Command.Extensions;
using GenerateAspNetCoreClient.Command.Model;
using GenerateAspNetCoreClient.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Namotion.Reflection;

namespace GenerateAspNetCoreClient.Command
{
    public class ClientModelBuilder
    {
        private readonly ApiDescriptionGroupCollection apiExplorer;
        private readonly GenerateClientOptions options;
        private readonly string[] additionalNamespaces;
        private readonly Assembly webProjectAssembly;

        public ClientModelBuilder(
            ApiDescriptionGroupCollection apiExplorer,
            GenerateClientOptions options,
            string[] additionalNamespaces,
            Assembly webProjectAssembly)
        {
            this.apiExplorer = apiExplorer;
            this.options = options;
            this.additionalNamespaces = additionalNamespaces;
            this.webProjectAssembly = webProjectAssembly;
        }

        public ClientCollection GetClientCollection()
        {
            var apiDescriptions = apiExplorer.Items
                .SelectMany(i => i.Items)
                .ToList();

            FilterDescriptions(apiDescriptions);

            var allNamespaces = GetNamespaces(apiDescriptions);
            var ambiguousTypes = GetAmbiguousTypes(allNamespaces);

            var assemblyName = webProjectAssembly.GetName().Name;
            var apiGroupsDescriptions = apiDescriptions.GroupBy(i => GroupInfo.From(i, assemblyName));

            string commonControllerNamespacePart = GetCommonNamespacesPart(apiGroupsDescriptions);

            var clients = apiGroupsDescriptions.Select(apis =>
                GetClientModel(
                    commonControllerNamespace: commonControllerNamespacePart,
                    additionalNamespaces: additionalNamespaces,
                    controllerInfo: apis.Key,
                    apiDescriptions: apis.ToList(),
                    ambiguousTypes: ambiguousTypes)
                ).ToList();

            return new ClientCollection(clients, ambiguousTypes);
        }

        private void FilterDescriptions(List<ApiDescription> apiDescriptions)
        {
            if (!string.IsNullOrEmpty(options.ExcludeTypes))
            {
                apiDescriptions.RemoveAll(api => api.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor
                    && controllerActionDescriptor.ControllerTypeInfo.FullName?.Contains(options.ExcludeTypes) == true);
            }

            if (!string.IsNullOrEmpty(options.IncludeTypes))
            {
                apiDescriptions.RemoveAll(api => api.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor
                    && controllerActionDescriptor.ControllerTypeInfo.FullName?.Contains(options.IncludeTypes) != true);
            }

            if (!string.IsNullOrEmpty(options.ExcludePaths))
            {
                apiDescriptions.RemoveAll(api => ("/" + api.RelativePath).Contains(options.ExcludePaths));
            }

            if (!string.IsNullOrEmpty(options.IncludePaths))
            {
                apiDescriptions.RemoveAll(api => !("/" + api.RelativePath).Contains(options.IncludePaths));
            }
        }

        internal Client GetClientModel(
            string commonControllerNamespace,
            string[] additionalNamespaces,
            GroupInfo controllerInfo,
            List<ApiDescription> apiDescriptions,
            HashSet<Type> ambiguousTypes)
        {
            apiDescriptions = HandleDuplicates(apiDescriptions);

            var subPath = GetSubPath(controllerInfo, commonControllerNamespace);

            var groupNamePascalCase = controllerInfo.GroupName.ToPascalCase();
            var name = options.TypeNamePattern
                .Replace("[controller]", groupNamePascalCase)
                .Replace("[group]", groupNamePascalCase);

            var clientNamespace = string.Join(".", new[] { options.Namespace }.Concat(subPath));

            var namespaces = GetNamespaces(apiDescriptions, ambiguousTypes)
                .Concat(additionalNamespaces);

            if (options.AddCancellationTokenParameters)
                namespaces = namespaces.Append("System.Threading");

            namespaces = namespaces
                .OrderByDescending(ns => ns.StartsWith("System"))
                .ThenBy(ns => ns);

            var methods = apiDescriptions.Select(GetEndpointMethod).ToList();

            return new Client
            (
                location: Path.Combine(subPath),
                importedNamespaces: namespaces.ToList(),
                @namespace: clientNamespace,
                accessModifier: options.AccessModifier,
                name: name,
                endpointMethods: methods
            );
        }

        internal EndpointMethod GetEndpointMethod(ApiDescription apiDescription)
        {
            var responseType = GetResponseType(apiDescription);

            if (responseType == null)
            {
                Console.WriteLine($"Cannot find return type for " + apiDescription.ActionDescriptor.DisplayName);
                responseType = typeof(void);
            }

            return new EndpointMethod
            (
                xmlDoc: GetXmlDoc(apiDescription),
                httpMethod: new HttpMethod(apiDescription.HttpMethod ?? HttpMethod.Get.Method),
                path: apiDescription.RelativePath?.TrimEnd('/') ?? "",
                responseType: responseType,
                name: GetActionName(apiDescription),
                parameters: GetParameters(apiDescription)
            );
        }

        private string GetActionName(ApiDescription apiDescription)
        {
            if (apiDescription.ActionDescriptor.EndpointMetadata.OfType<RouteNameMetadata>().FirstOrDefault()?.RouteName is string routeName)
                return routeName;

            if (apiDescription.ActionDescriptor is ControllerActionDescriptor { ActionName: string actionName })
                return actionName;

            var method = apiDescription.HttpMethod ?? "GET";
            return (method + " " + apiDescription.RelativePath).ToPascalCase();
        }

        private List<Parameter> GetParameters(ApiDescription apiDescription)
        {
            var parametersList = new List<Parameter>();

            for (int i = 0; i < apiDescription.ParameterDescriptions.Count; i++)
            {
                var parameterDescription = apiDescription.ParameterDescriptions[i];

                if (parameterDescription.ParameterDescriptor?.ParameterType == typeof(CancellationToken))
                    continue;

                // IFormFile
                if (parameterDescription.ParameterDescriptor?.ParameterType == typeof(IFormFile))
                {
                    var name = parameterDescription.ParameterDescriptor.Name;

                    parametersList.Add(new Parameter(
                        source: ParameterSource.File,
                        type: typeof(IFormFile),
                        name: parameterDescription.Name,
                        parameterName: name.ToCamelCase(),
                        defaultValueLiteral: null));

                    // Skip parameters that correspond to same file
                    while (i + 1 < apiDescription.ParameterDescriptions.Count
                        && apiDescription.ParameterDescriptions[i + 1].ParameterDescriptor?.ParameterType == typeof(IFormFile)
                        && apiDescription.ParameterDescriptions[i + 1].ParameterDescriptor?.Name == name)
                    {
                        i++;
                    }

                    continue;
                }

                // Form
                // API explorer shows form as separate parameters. We want to have single model parameter.
                if (parameterDescription.Source == BindingSource.Form)
                {
                    var name = parameterDescription.ParameterDescriptor?.Name ?? "form";
                    var formType = parameterDescription.ParameterDescriptor?.ParameterType ?? typeof(object);

                    parametersList.Add(new Parameter(
                        source: ParameterSource.Form,
                        type: formType,
                        name: parameterDescription.Name,
                        parameterName: name.ToCamelCase(),
                        defaultValueLiteral: "null"));

                    // Skip parameters that correspond to same form
                    while (i + 1 < apiDescription.ParameterDescriptions.Count
                        && apiDescription.ParameterDescriptions[i + 1].ParameterDescriptor?.ParameterType == formType
                        && apiDescription.ParameterDescriptions[i + 1].ParameterDescriptor?.Name == name)
                    {
                        i++;
                    }
                }

                if (options.UseQueryModels
                    && parameterDescription.Source == BindingSource.Query
                    && parameterDescription.ModelMetadata?.ContainerType != null
                    && parameterDescription.ParameterDescriptor != null)
                {
                    var name = parameterDescription.ParameterDescriptor.Name;
                    var containerType = parameterDescription.ModelMetadata.ContainerType;

                    parametersList.Add(new Parameter(
                        source: ParameterSource.Query,
                        type: containerType,
                        name: parameterDescription.Name,
                        parameterName: name.ToCamelCase(),
                        defaultValueLiteral: "null"));

                    // Skip parameters that correspond to same query model
                    while (i + 1 < apiDescription.ParameterDescriptions.Count
                        && apiDescription.ParameterDescriptions[i + 1].ModelMetadata?.ContainerType == containerType
                        && apiDescription.ParameterDescriptions[i + 1].ParameterDescriptor?.Name == name)
                    {
                        i++;
                    }

                    continue;
                }

                if (parametersList.Any(p => p.Name.Equals(parameterDescription.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"Duplicate parameter '{parameterDescription.Name}' for '{apiDescription.ActionDescriptor.DisplayName}'");
                    continue;
                }

                var source = parameterDescription.Source.Id switch
                {
                    "Body" => ParameterSource.Body,
                    "Path" => ParameterSource.Path,
                    "FormFile" => ParameterSource.File,
                    "Query" => ParameterSource.Query,
                    "Header" => ParameterSource.Header,
                    "Form" => ParameterSource.Form,
                    _ => ParameterSource.Query
                };

                // Is it possible to have other static values, apart from headers?
                var isStaticValue = parameterDescription.Source == BindingSource.Header && parameterDescription.BindingInfo is null;

                var isQueryModel = source == ParameterSource.Query
                    && parameterDescription.Type != parameterDescription.ParameterDescriptor?.ParameterType;

                // If query model - use parameterDescription.Name, as ParameterDescriptor.Name is name for the whole model,
                // not separate parameters.
                var parameterName = isQueryModel
                    ? parameterDescription.Name.ToCamelCase()
                    : (parameterDescription.ParameterDescriptor?.Name ?? parameterDescription.Name).ToCamelCase();

                parameterName = new string(parameterName.Where(c => char.IsLetterOrDigit(c)).ToArray());

                var type = parameterDescription.Type ?? typeof(string);

                var defaultValue = GetDefaultValueLiteral(parameterDescription, type);

                if (defaultValue == "null")
                {
                    type = type.ToNullable();
                }

                parametersList.Add(new Parameter(
                    source: source,
                    type: type,
                    name: parameterDescription.Name,
                    parameterName: parameterName,
                    defaultValueLiteral: defaultValue,
                    isStaticValue: isStaticValue));
            }

            if (options.AddCancellationTokenParameters)
            {
                parametersList.Add(new Parameter(
                    source: ParameterSource.Other,
                    type: typeof(CancellationToken),
                    name: "cancellationToken",
                    parameterName: "cancellationToken",
                    defaultValueLiteral: "default"));
            }

            return parametersList;
        }

        private static HashSet<Type> GetAmbiguousTypes(IEnumerable<string> namespaces)
        {
            var namespacesSet = namespaces.ToHashSet();

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
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => t.DeclaringType == null && namespacesSet.Contains(t.Namespace!))
                .GroupBy(t => t.Name)
                .Where(g => g.Select(t => t.Namespace).Distinct().Count() > 1)
                .SelectMany(g => g)
                .ToHashSet();
        }

        private static string? GetXmlDoc(ApiDescription apiDescription)
        {
            var xmlElement = (apiDescription.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo.GetXmlDocsElement();

            if (xmlElement == null)
                return null;

            var xmlLines = xmlElement.Elements()
                .Select(e => e.ToString())
                .SelectMany(s => s.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                .Select(line => line.Trim().Replace("cref=\"T:", "cref=\""));

            var xmlDoc = string.Join(Environment.NewLine, xmlLines).Indent("/// ");

            return xmlDoc;
        }

        private static Type? GetResponseType(ApiDescription apiDescription)
        {
            var responseType = apiDescription.SupportedResponseTypes
                .OrderBy(r => r.StatusCode)
                .FirstOrDefault(r => r.StatusCode >= 200 && r.StatusCode < 300)
                ?.Type;

            if (responseType is null)
            {
                // Workaround for bug https://github.com/dotnet/aspnetcore/issues/30465
                var methodInfo = (apiDescription.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
                var methodResponseType = methodInfo?.ReturnType?.UnwrapTask();

                if (methodResponseType?.IsAssignableTo(typeof(FileResult)) == true)
                {
                    responseType = typeof(Stream);
                }
            }

            return responseType;
        }

        private static string GetCommonNamespacesPart(IEnumerable<IGrouping<GroupInfo, ApiDescription>> controllerApiDescriptions)
        {
            var namespaces = controllerApiDescriptions
                .Select(c => c.Key)
                .Select(c => c.Namespace ?? "");

            return namespaces.GetCommonPart(".");
        }

        private List<string> GetNamespaces(IEnumerable<ApiDescription> apiDescriptions, HashSet<Type>? ambiguousTypes = null)
        {
            var namespaces = new HashSet<string>();

            foreach (var apiDescription in apiDescriptions)
            {
                var responseType = GetResponseType(apiDescription);
                AddForType(responseType);

                foreach (var parameterDescription in apiDescription.ParameterDescriptions)
                {
                    switch (parameterDescription.Source.Id)
                    {
                        case "FormFile":
                            // Skip FormFile, as it won't be present in result file 
                            // (not needed for 3.1+)
                            break;
                        case "Form":
                            AddForType(parameterDescription.ParameterDescriptor.ParameterType);
                            break;
                        case "Query" when options.UseQueryModels && parameterDescription.ModelMetadata?.ContainerType != null:
                            AddForType(parameterDescription.ModelMetadata.ContainerType);
                            break;
                        default:
                            AddForType(parameterDescription.Type);
                            break;
                    }
                }
            }

            return namespaces.ToList();

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

        private static string? GetDefaultValueLiteral(ApiParameterDescription parameter, Type parameterType)
        {
            // Use reflection for AspNetCore 2.1 compatibility.
            var defaultValue = parameter.TryGetPropertyValue<object>(nameof(parameter.DefaultValue));

            if (defaultValue != null && defaultValue is not DBNull)
            {
                // If defaultValue is not null - return it.
                return defaultValue.ToLiteral();
            }

            var isRequired = parameter.TryGetPropertyValue<bool?>(nameof(parameter.IsRequired)) == true;
            isRequired |= parameter.ModelMetadata?.IsBindingRequired == true;

            if (!parameterType.IsValueType || parameterType.IsNullable())
            {
                isRequired |= parameter.ModelMetadata?.IsRequired == true;
            }

            if (isRequired == false)
            {
                // If defaultValue is null, but value is not required - return it anyway.
                return "null";
            }

            return null;
        }

        private static string[] GetSubPath(GroupInfo controllerActionDescriptor, string commonNamespace)
        {
            return (controllerActionDescriptor.Namespace ?? "")
                .Substring(commonNamespace.Length)
                .Split(".")
                .Select(nsPart => nsPart.Replace("Controllers", ""))
                .Where(nsPart => nsPart != "")
                .ToArray();
        }

        private static List<ApiDescription> HandleDuplicates(List<ApiDescription> apiDescriptions)
        {
            var conflictingApisGroups = apiDescriptions
                .Where(api => api.ActionDescriptor is ControllerActionDescriptor)
                .GroupBy(api => ((ControllerActionDescriptor)api.ActionDescriptor).ActionName
                    + string.Concat(api.ParameterDescriptions.Select(p => p.Type?.FullName ?? "-")))
                .Where(g => g.Count() > 1);

            foreach (var conflictingApis in conflictingApisGroups)
            {
                // Take suffixes from path
                var commonPathPart = conflictingApis.Select(api => api.RelativePath ?? "").GetCommonPart("/");

                foreach (var api in conflictingApis)
                {
                    var suffix = api.RelativePath is null || api.RelativePath == commonPathPart
                        ? ""
                        : api.RelativePath[(commonPathPart.Length + 1)..].ToPascalCase();

                    ((ControllerActionDescriptor)api.ActionDescriptor).ActionName += suffix;
                }
            }

            return apiDescriptions;
        }
    }
}
