using System;

namespace GenerateAspNetCoreClient.Command.Model
{
    public class Parameter
    {
        public ParameterSource Source { get; }

        public Type Type { get; }

        public string Name { get; }

        public string ParameterName { get; }

        public string? DefaultValueLiteral { get; }

        public bool IsConstant { get; }

        public Parameter(ParameterSource source, Type type, string name, string parameterName, string? defaultValueLiteral, bool isStaticValue = false)
        {
            Source = source;
            Type = type;
            Name = name;
            ParameterName = parameterName;
            DefaultValueLiteral = defaultValueLiteral;
            IsConstant = isStaticValue;
        }
    }
}
