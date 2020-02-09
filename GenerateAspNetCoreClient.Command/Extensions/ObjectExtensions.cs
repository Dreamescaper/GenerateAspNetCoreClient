using System;

namespace GenerateAspNetCoreClient.Command.Extensions
{
    internal static class ObjectExtensions
    {
        public static string ToLiteral(this object? obj)
        {
            if (obj == null)
                return "null";

            var type = obj.GetType();

            return obj switch
            {
                string s => '"' + s + '"',
                char c => "'" + c + "'",
                bool b => b ? "true" : "false",
                _ when type.IsEnum && Enum.IsDefined(type, obj) => $"{type.Name}.{obj}",
                _ when type.IsPrimitive => obj.ToString() ?? "",
                _ => throw new NotSupportedException()
            };
        }
    }
}
