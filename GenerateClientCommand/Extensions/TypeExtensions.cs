using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GenerateClientCommand.Extensions
{
    internal static class TypeExtensions
    {
        private static readonly Dictionary<Type, string> DefaultTypes = new Dictionary<Type, string>
        {
            [typeof(void)] = "void",
            [typeof(object)] = "object",
            [typeof(sbyte)] = "sbyte",
            [typeof(byte)] = "byte",
            [typeof(short)] = "short",
            [typeof(ushort)] = "ushort",
            [typeof(int)] = "int",
            [typeof(uint)] = "uint",
            [typeof(long)] = "long",
            [typeof(ulong)] = "ulong",
            [typeof(decimal)] = "decimal",
            [typeof(float)] = "float",
            [typeof(double)] = "double",
            [typeof(bool)] = "bool",
            [typeof(char)] = "char",
            [typeof(string)] = "string"
        };

        public static bool IsBuiltInType(this Type @this)
        {
            if (DefaultTypes.ContainsKey(@this))
                return true;

            if (@this.IsGenericType
                && @this.GetGenericTypeDefinition() == typeof(Nullable<>)
                && DefaultTypes.ContainsKey(@this.GetGenericArguments()[0]))
            {
                return true;
            }

            if (@this.IsArray && DefaultTypes.ContainsKey(@this.GetElementType()!))
                return true;

            return false;
        }

        public static string GetName(this Type @this, HashSet<Type> ambiguousTypes)
        {
            if (@this.IsGenericType && @this.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = @this.GenericTypeArguments[0];
                return underlyingType.GetName(ambiguousTypes) + "?";
            }

            if (@this.IsArray)
            {
                var elementType = @this.GetElementType();

                if (elementType != null)
                {
                    return elementType.GetName(ambiguousTypes) + "[]";
                }
            }

            if (!DefaultTypes.TryGetValue(@this, out var name))
                name = @this.Name;

            if (ambiguousTypes.Contains(@this))
            {
                name = @this.Namespace + "." + name;
            }

            if (@this.IsConstructedGenericType)
            {
                name = name.Substring(0, name.LastIndexOf('`'));

                var genericNames = @this.GetGenericArguments().Select(a => a.GetName(ambiguousTypes));

                name += $"<{string.Join(", ", genericNames)}>";
            }

            return name;
        }

        public static Type WrapInTask(this Type @this)
        {
            if (@this == typeof(void))
                return typeof(Task);

            return typeof(Task<>).MakeGenericType(@this);
        }
    }
}
