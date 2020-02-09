using System;

namespace GenerateAspNetCoreClient.Command.Model
{
    public class Parameter
    {
        public ParameterSource Source { get; }
        public Type Type { get; }
        public string Name { get; }
        public string? DefaultValueLiteral { get; }

        public Parameter(ParameterSource source, Type type, string name, string? defaultValueLiteral)
        {
            Source = source;
            Type = type;
            Name = name;
            DefaultValueLiteral = defaultValueLiteral;
        }
    }
}
