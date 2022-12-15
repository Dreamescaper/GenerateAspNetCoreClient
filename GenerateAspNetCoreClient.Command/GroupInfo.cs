using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace GenerateAspNetCoreClient.Command
{
    internal struct GroupInfo
    {
        public string GroupName { get; set; }
        public string? Namespace { get; set; }

        public static GroupInfo From(ApiDescription apiDescription, string? defaultGroupName)
        {
            var controllerDescriptor = apiDescription.ActionDescriptor as ControllerActionDescriptor;

            return new GroupInfo
            {
                GroupName = controllerDescriptor?.ControllerName ?? apiDescription.GroupName ?? defaultGroupName ?? "",
                Namespace = controllerDescriptor?.ControllerTypeInfo?.Namespace
            };
        }
    }
}
