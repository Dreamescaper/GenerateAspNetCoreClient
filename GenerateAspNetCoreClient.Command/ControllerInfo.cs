using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace GenerateAspNetCoreClient.Command
{
    internal struct ControllerInfo
    {
        public string ControllerName { get; set; }
        public TypeInfo ControllerTypeInfo { get; set; }

        public static ControllerInfo From(ControllerActionDescriptor controllerActionDescriptor)
        {
            return new ControllerInfo
            {
                ControllerName = controllerActionDescriptor.ControllerName,
                ControllerTypeInfo = controllerActionDescriptor.ControllerTypeInfo
            };
        }
    }
}
