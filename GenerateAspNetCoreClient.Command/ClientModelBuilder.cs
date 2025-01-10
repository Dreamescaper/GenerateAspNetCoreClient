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
        private readonly Assembly webProjectAssembly;

        public ClientModelBuilder(
            ApiDescriptionGroupCollection apiExplorer,
            GenerateClientOptions options,
            Assembly webProjectAssembly)
        {
            this.apiExplorer = apiExplorer;
            this.options = options;
            this.webProjectAssembly = webProjectAssembly;
        }

        public List<Client> GetClientCollection()
        {
            var apiDescriptions = apiExplorer.Items
                .SelectMany(i => i.Items)
                .ToList();

            FilterDescriptions(apiDescriptions);

            var assemblyName = webProjectAssembly.GetName().Name;
            var apiGroupsDescriptions = apiDescriptions.GroupBy(i => GroupInfo.From(i, assemblyName));

            string commonControllerNamespacePart = GetCommonNamespacesPart(apiGroupsDescriptions);

            var clients = apiGroupsDescriptions.Select(apis =>
                GetClientModel(
                    commonControllerNamespace: commonControllerNamespacePart,
                    controllerInfo: apis.Key,
                    apiDescriptions: apis.ToList())
                ).ToList();

            return clients;
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
            GroupInfo controllerInfo,
            List<ApiDescription> apiDescriptions)
        {
            apiDescriptions = HandleDuplicates(apiDescriptions);

            var subPath = GetSubPath(controllerInfo, commonControllerNamespace);

            var groupNamePascalCase = controllerInfo.GroupName.ToPascalCase();
            var name = options.TypeNamePattern
                .Replace("[controller]", groupNamePascalCase)
                .Replace("[group]", groupNamePascalCase);

            var clientNamespace = string.Join(".", new[] { options.Namespace }.Concat(subPath));

            var methods = apiDescriptions.Select(GetEndpointMethod).ToList();

            return new Client
            (
                location: Path.Combine(subPath),
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
                var paramType = parameterDescription.ParameterDescriptor?.ParameterType;

                if (paramType == typeof(CancellationToken))
                    continue;

                // IFormFile
                if (paramType == typeof(IFormFile))
                {
                    var name = parameterDescription.ParameterDescriptor.Name;

                    parametersList.Add(new Parameter(
                        source: ParameterSource.File,
                        type: typeof(Stream),
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

                // IFormFile[], List<IFormFile>
                if (paramType != null)
                {
                    bool isFormFileArray = paramType == typeof(IFormFile[]);
                    bool isFormFileList =
                        paramType.IsGenericType
                        && paramType.GetGenericTypeDefinition() == typeof(List<>)
                        && paramType.GetGenericArguments()[0] == typeof(IFormFile);

                    if (isFormFileArray || isFormFileList)
                    {
                        var name = parameterDescription.ParameterDescriptor?.Name;

                        parametersList.Add(new Parameter(
                            source: ParameterSource.File,
                            type: typeof(List<Stream>),
                            name: parameterDescription.Name,
                            parameterName: name.ToCamelCase(),
                            defaultValueLiteral: null));

                        continue;
                    }
                }

                // Form
                // API explorer shows form as separate parameters. We want to have single model parameter.
                if (parameterDescription.Source == BindingSource.Form)
                {
                    var name = parameterDescription.ParameterDescriptor?.Name ?? "form";
                    var formType = paramType ?? typeof(object);

                    if (formType == typeof(IFormCollection))
                    {
                        parametersList.Add(new Parameter(
                            source: ParameterSource.Form,
                            type: typeof(Dictionary<string, string>),
                            name: parameterDescription.Name,
                            parameterName: name.ToCamelCase(),
                            defaultValueLiteral: null));

                        continue;
                    }
                    else
                    {
                        var sameFormParameters = apiDescription.ParameterDescriptions.Skip(i - 1)
                            .TakeWhile(d => d.ParameterDescriptor?.ParameterType == formType && d.ParameterDescriptor?.Name == name)
                            .ToArray();

                        // If form model has file parameters - we have to put it as separate parameters.
                        if (!sameFormParameters.Any(p => p.Source.Id == "FormFile"))
                        {
                            parametersList.Add(new Parameter(
                                source: ParameterSource.Form,
                                type: formType,
                                name: parameterDescription.Name,
                                parameterName: name.ToCamelCase(),
                                defaultValueLiteral: "null"));

                            if (sameFormParameters.Length > 0)
                                i += sameFormParameters.Length - 1;

                            continue;
                        }
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

                var isQueryModel = source is ParameterSource.Query or ParameterSource.Form
                    && parameterDescription.Type != parameterDescription.ParameterDescriptor?.ParameterType;

                // If query model - use parameterDescription.Name, as ParameterDescriptor.Name is name for the whole model,
                // not separate parameters.
                var parameterName = isQueryModel
                    ? parameterDescription.Name.ToCamelCase()
                    : (parameterDescription.ParameterDescriptor?.Name ?? parameterDescription.Name).ToCamelCase();

                parameterName = new string(parameterName.Where(c => char.IsLetterOrDigit(c)).ToArray());

                var type = parameterDescription.Source == BindingSource.FormFile
                    ? parameterDescription.Type == typeof(IFormFile[]) ||
                      (parameterDescription.Type.IsGenericType &&
                       parameterDescription.Type.GetGenericTypeDefinition() == typeof(List<>)
                       && parameterDescription.Type.GetGenericArguments()[0] == typeof(IFormFile))
                        ? typeof(List<Stream>)
                        : typeof(Stream)
                    : parameterDescription.ModelMetadata?.ModelType ?? parameterDescription.Type ?? typeof(string);

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

        private static string? GetDefaultValueLiteral(ApiParameterDescription parameter, Type parameterType)
        {
            var defaultValue = parameter.DefaultValue;

            if (defaultValue != null && defaultValue is not DBNull)
            {
                // If defaultValue is not null - return it.
                return defaultValue.ToLiteral();
            }

            var isRequired = parameter.IsRequired;
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
