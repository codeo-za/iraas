using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace IRAAS.Logging;

public interface ILogMessageGenerator
{
    string GenerateMessageFor(Exception ex);
}

public class LogMessageGenerator : ILogMessageGenerator
{
    public string GenerateMessageFor(Exception ex)
    {
        if (ex is null)
        {
            return "(null)";
        }

        return $"{ex.Message} exception::{GenerateExceptionJsonFor(ex)}";
    }

    private string GenerateExceptionJsonFor(Exception exception)
    {
        var exType = exception.GetType();
        var dict = new OrderedDictionary();
        var props = MetaPropertiesFor(exType);
        dict["Type"] = exType.Name;
        foreach (var prop in props)
        {
            dict[prop.Name] = prop.GetValue(exception);
        }
        dict["StackTrace"] = exception.StackTrace;

        return JsonSerializer.Serialize(dict);
    }
        
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ExceptionMetaPropertyCache
        = new();

    private static PropertyInfo[] MetaPropertiesFor(Type type)
    {
        if (ExceptionMetaPropertyCache.TryGetValue(type, out var result))
        {
            return result;
        }

        result = type.GetProperties()
            .Where(p => p.DeclaringType != typeof(Exception))
            .ToArray();
        ExceptionMetaPropertyCache.TryAdd(type, result);
        return result;
    }
}