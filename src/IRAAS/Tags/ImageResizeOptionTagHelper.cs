using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using IRAAS.ImageProcessing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PeanutButter.Utils;

namespace IRAAS.Tags;

[HtmlTargetElement("image-resize-option")]
public class ImageResizeOptionTagHelper : TagHelper
{
    private readonly IDefaultImageResizeParameters _defaults;

    public ImageResizeOptionTagHelper(
        IDefaultImageResizeParameters defaults
    )
    {
        _defaults = defaults;
    }

    [HtmlAttributeName("prop")]
    public PropertyInfo Prop { get; set; }

    public override void Process(
        TagHelperContext context,
        TagHelperOutput output
    )
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        var userInput = InputGenerators.Aggregate(
            null as XElement,
            (acc, cur) => acc ?? cur(Prop, _defaults)
        );

        var root = new XElement(
            "div",
            Class("row"),
            new XElement(
                "div",
                Class("label"),
                CreateLabel()
            ),
            new XElement(
                "div",
                Class("input"),
                userInput
            )
        );

        output.Content.AppendHtml(root.ToString());
    }

    private static readonly Func<PropertyInfo, IDefaultImageResizeParameters, XElement>[] InputGenerators =
    {
        CreateDropDownByAttribute,
        CreateDropDownForEnum,
        CreateNumericInput,
        CreateTextInput
    };

    private static XElement CreateNumericInput(
        PropertyInfo prop,
        IDefaultImageResizeParameters defaults
    )
    {
        var underlyingType = prop.PropertyType.GetUnderlyingType();
        var minAttrib = prop.GetCustomAttributes<MinAttribute>().FirstOrDefault();
        var maxAttrib = prop.GetCustomAttributes<MaxAttribute>().FirstOrDefault();
        var stepAttrib = prop.GetCustomAttributes<StepAttribute>().FirstOrDefault();
        var defaultAttrib = prop.GetCustomAttributes<DefaultAttribute>().FirstOrDefault();
        var fallback = FindDefaultValueFor(prop, defaults);
        var defaultValue = defaultAttrib?.Value
            ?? (double.TryParse(fallback, out var v) ? v : null);

        if (underlyingType == typeof(byte))
        {
            return CreateNumericInputWithAttributes(
                prop,
                minAttrib?.Value ?? byte.MinValue,
                maxAttrib?.Value ?? byte.MaxValue,
                stepAttrib?.Value ?? 1,
                defaultValue
            );
        }

        if (underlyingType == typeof(int) || underlyingType == typeof(float))
        {
            return CreateNumericInputWithAttributes(
                prop,
                minAttrib?.Value ?? int.MinValue,
                maxAttrib?.Value ?? int.MaxValue,
                stepAttrib?.Value ?? 1,
                defaultValue
            );
        }

        return null;
    }

    private static XElement CreateNumericInputWithAttributes(
        PropertyInfo prop,
        double min,
        double max,
        double step,
        double? defaultValue
    )
    {
        var result = new XElement(
            "input",
            Id(prop),
            Name(prop),
            new XAttribute("type", "number"),
            new XAttribute("min", min),
            new XAttribute("max", max),
            new XAttribute("step", step)
        );
        if (defaultValue is not null)
        {
            result.Add(new XAttribute("value", defaultValue));
        }

        return result;
    }

    private static XElement CreateDropDownForEnum(
        PropertyInfo prop,
        IDefaultImageResizeParameters defaults
    )
    {
        var underlyingType = prop.PropertyType.GetUnderlyingType();
        if (!underlyingType.IsEnum)
        {
            return null;
        }

        var options = Enum.GetNames(underlyingType).ToList();
        if (prop.PropertyType.IsNullableType())
        {
            options.Insert(0, "");
        }

        var selected = FindDefaultValueFor(prop, defaults);

        return CreateDropDownFor(
            prop,
            $"{selected}",
            options
        );
    }

    private static XElement CreateDropDownByAttribute(
        PropertyInfo prop,
        IDefaultImageResizeParameters defaults
    )
    {
        var allowedValuesAttrib = prop.GetCustomAttributes<OptionsAttribute>()
            .FirstOrDefault();
        var selected = FindDefaultValueFor(prop, defaults);
        return allowedValuesAttrib is null
            ? null
            : CreateDropDownFor(prop, selected, allowedValuesAttrib.Options);
    }

    private static string FindDefaultValueFor(
        PropertyInfo prop,
        IDefaultImageResizeParameters defaults
    )
    {
        var defaultProp = TryFindDefaultPropertyMatching(prop);
        if (defaultProp is null)
        {
            return null;
        }

        return $"{defaultProp.GetValue(defaults)}";
    }

    private static PropertyInfo TryFindDefaultPropertyMatching(
        PropertyInfo other
    )
    {
        var all = ReadProperties<IDefaultImageResizeParameters>();
        return all.TryGetValue(other.Name, out var result)
            ? result
            : null;
    }

    private static Dictionary<string, PropertyInfo> ReadProperties<T>() where T : class
    {
        var type = typeof(T);
        return PropertyCache.FindOrAdd(
            type,
            () =>
            {
                if (!type.IsInterface)
                {
                    return type.GetProperties()
                        .ToDictionary(
                            o => o.Name,
                            o => o
                        );
                }

                return new[] { type }.Concat(
                        type.GetInterfaces()
                    ).SelectMany(o => o.GetProperties())
                    .ToDictionary(
                        o => o.Name,
                        o => o
                    );
            }
        );
    }

    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = new();

    private static XAttribute Class(string name)
    {
        return new XAttribute("class", name);
    }

    private static XElement CreateDropDownFor(
        PropertyInfo prop,
        string selected,
        IEnumerable<string> values
    )
    {
        var options = values.Select(
            v => selected == v
                ? new XElement(
                    "option",
                    new XAttribute("value", v),
                    new XAttribute("selected", ""),
                    new XText(v)
                )
                : new XElement(
                    "option",
                    new XAttribute("value", v),
                    new XText(v)
                )
        );
        return new XElement(
            "select",
            Id(prop),
            Name(prop),
            options
        );
    }

    private XElement CreateLabel()
    {
        return new XElement(
            "label",
            new XAttribute("for", Prop.Name),
            new XText(Prop.Name)
        );
    }

    private static XElement CreateTextInput(
        PropertyInfo prop,
        IDefaultImageResizeParameters defaultImageResizeParameters
    )
    {
        return new XElement(
            "input",
            Name(prop),
            Id(prop)
        );
    }

    private static XAttribute Id(PropertyInfo prop)
    {
        return Id(prop.Name.ToCamelCase());
    }

    private static XAttribute Name(PropertyInfo prop)
    {
        return Name(prop.Name.ToCamelCase());
    }

    private static XAttribute Id(string value)
    {
        return new XAttribute("id", value);
    }

    private static XAttribute Name(string value)
    {
        return new XAttribute("name", value);
    }
}