using System;
using System.Reflection;

namespace IRAAS.ImageProcessing;

public class OptionsAttribute : Attribute
{
    public string[] Options { get; }

    public OptionsAttribute(params string[] options)
    {
        Options = options;
    }
}

public class OptionsFrom : OptionsAttribute
{
    public OptionsFrom(Type type, string propertyName)
        : base(GenerateOptionsFrom(type, propertyName))
    {
            
    }

    private static string[] GenerateOptionsFrom(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        return (string[])prop.GetValue(null);
    }
}

public abstract class DoubleValueAttribute : Attribute
{
    public double Value { get; }

    public DoubleValueAttribute(double value)
    {
        Value = value;
    }
}

public class MaxAttribute : DoubleValueAttribute
{
    public MaxAttribute(double value) : base(value)
    {
    }
}

public class MinAttribute : DoubleValueAttribute
{
    public MinAttribute(double value) : base(value)
    {
    }
}

public class StepAttribute : DoubleValueAttribute
{
    public StepAttribute(double value) : base(value)
    {
    }
}

public class DefaultAttribute : DoubleValueAttribute
{
    public DefaultAttribute(double value) : base(value)
    {
    }
}