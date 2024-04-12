using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using IRAAS.ImageProcessing;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PeanutButter.Utils;

namespace IRAAS.Tags
{
    [HtmlTargetElement("image-resize-option")]
    public class ImageResizeOptionTagHelper : TagHelper
    {
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
                (acc, cur) => acc ?? cur(Prop)
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

        private static readonly Func<PropertyInfo, XElement>[] InputGenerators =
        {
            CreateDropDownByAttribute,
            CreateDropDownForEnum,
            CreateNumericInput,
            CreateTextInput
        };

        private static XElement CreateNumericInput(PropertyInfo prop)
        {
            var underlyingType = prop.PropertyType.GetUnderlyingType();
            var minAttrib = prop.GetCustomAttributes<MinAttribute>().FirstOrDefault();
            var maxAttrib = prop.GetCustomAttributes<MaxAttribute>().FirstOrDefault();
            var stepAttrib = prop.GetCustomAttributes<StepAttribute>().FirstOrDefault();
            var defaultAttrib = prop.GetCustomAttributes<DefaultAttribute>().FirstOrDefault();

            if (underlyingType == typeof(byte))
            {
                return CreateNumericInputWithAttributes(
                    prop,
                    minAttrib?.Value ?? byte.MinValue,
                    maxAttrib?.Value ?? byte.MaxValue,
                    stepAttrib?.Value ?? 1,
                    defaultAttrib?.Value
                );
            }

            if (underlyingType == typeof(int) || underlyingType == typeof(float))
            {
                return CreateNumericInputWithAttributes(
                    prop,
                    minAttrib?.Value ?? int.MinValue,
                    maxAttrib?.Value ?? int.MaxValue,
                    stepAttrib?.Value ?? 1,
                    defaultAttrib?.Value
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
            if (defaultValue != null)
            {
                result.Add(new XAttribute("value", defaultValue));
            }

            return result;
        }

        private static XElement CreateDropDownForEnum(PropertyInfo prop)
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

            return CreateDropDownFor(
                prop,
                options
            );
        }

        private static XElement CreateDropDownByAttribute(PropertyInfo prop)
        {
            var allowedValuesAttrib = prop.GetCustomAttributes<OptionsAttribute>()
                .FirstOrDefault();
            return allowedValuesAttrib == null
                ? null
                : CreateDropDownFor(prop, allowedValuesAttrib.Options);
        }

        private static XAttribute Class(string name)
        {
            return new XAttribute("class", name);
        }

        private static XElement CreateDropDownFor(
            PropertyInfo prop,
            IEnumerable<string> values
        )
        {
            var options = values.Select(
                v =>
                    new XElement(
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

        private static XElement CreateTextInput(PropertyInfo prop)
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
}