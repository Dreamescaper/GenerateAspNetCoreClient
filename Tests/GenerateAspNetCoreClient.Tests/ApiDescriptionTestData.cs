using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TestWebApi.Controllers;
using TestWebApi.Models;

namespace GenerateAspNetCoreClient.Tests
{
    public class ApiDescriptionTestData
    {
        public static ApiParameterDescription CreateParameter(string name = "id",
            BindingSource bindingSource = null, Type type = null)
        {
            return new ApiParameterDescription
            {
                IsRequired = true,
                Name = name,
                ParameterDescriptor = new ParameterDescriptor
                {
                    Name = name,
                    ParameterType = type ?? typeof(Guid),
                },
                Type = type ?? typeof(Guid),
                Source = bindingSource ?? BindingSource.Path
            };
        }

        public static ApiDescription CreateApiDescription(
            string httpMethod = "GET",
            string actionName = "Get",
            string path = "WeatherForecast/{id}",
            IList<ApiParameterDescription> apiParameters = null,
            Type responseType = null,
            Type controllerType = null)
        {
            controllerType ??= typeof(WeatherForecastController);

            var apiDescription = new ApiDescription
            {
                ActionDescriptor = new ControllerActionDescriptor
                {
                    ActionName = actionName,
                    ControllerName = controllerType.Name.Replace("Controller", ""),
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    DisplayName = $"{controllerType.FullName}.{actionName} ({controllerType.Assembly.GetName().Name})",
                    MethodInfo = typeof(WeatherForecastController).GetMethod("Get", Array.Empty<Type>())
                },
                HttpMethod = httpMethod,
                RelativePath = path,
                SupportedResponseTypes =
                {
                    new ApiResponseType
                    {
                        StatusCode = 200,
                        Type = responseType ?? typeof(WeatherForecastRecord)
                    }
                }
            };

            foreach (var parDescription in apiParameters ?? new[] { CreateParameter() })
            {
                apiDescription.ParameterDescriptions.Add(parDescription);
            }

            return apiDescription;
        }

        public static ApiDescriptionGroupCollection CreateApiExplorer(ApiDescription[] apiDescriptions)
        {
            return new ApiDescriptionGroupCollection(new List<ApiDescriptionGroup>
            {
                new ApiDescriptionGroup(null, apiDescriptions)
            }, 1);
        }

        public static ApiDescriptionGroupCollection CreateApiExplorer(
            string httpMethod = "GET",
            string actionName = "Get",
            string path = "WeatherForecast/{id}",
            IList<ApiParameterDescription> apiParameters = null,
            Type responseType = null)
        {
            var apiDescription = CreateApiDescription(httpMethod, actionName, path, apiParameters, responseType);
            return CreateApiExplorer(new[] { apiDescription });
        }
    }
}
