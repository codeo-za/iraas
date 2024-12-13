using System;
using System.Reflection;

namespace IRAAS.Tags;

public static class TypeExtensions
{
    internal static bool IsNullableType(this Type arg)
    {
        var method = GenericGetDefaultValueMethod.MakeGenericMethod(arg);
        var defaultValueForType = method.Invoke(null, new object[] { });
        return defaultValueForType == null;
    }

    internal static Type GetUnderlyingType(this Type type)
    {
        return (type.IsGenericType)
            ? type.GetGenericArguments()[0]
            : type; // 
    }
        

    private static readonly MethodInfo GenericGetDefaultValueMethod =
        typeof(TypeExtensions).GetMethod(
            nameof(GetDefaultValueFor),
            BindingFlags.NonPublic | BindingFlags.Static
        );

    private static T GetDefaultValueFor<T>()
    {
        return default(T);
    } 
}