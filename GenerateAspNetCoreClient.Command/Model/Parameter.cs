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

        public Parameter(ParameterSource source, Type type, string name, string parameterName, string? defaultValueLiteral)
        {
            Source = source;
            Type = type;
            Name = name;
            ParameterName = parameterName;
            DefaultValueLiteral = defaultValueLiteral;
        }
    }
}
