using System;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NExpect;
using NExpect.Interfaces;
using NExpect.MatcherLogic;
using PeanutButter.Utils;
using static NExpect.Expectations;

namespace IRAAS.Tests.Controllers;

public static class ControllerMatchers
{
    public static SupportingExtension Method(
        this IHave<Type> have,
        string method
    )
    {
        var result = new SupportingExtension();
        have.AddMatcher(
            actual =>
            {
                result.Member = method;
                result.Continuation = have;
                var passed = actual.GetMethod(method) != null;
                return new MatcherResult(
                    passed,
                    () => $"Expected type {actual} to have method {method}");
            });
        return result;
    }

    public static IMore<Type> Route(
        this IHave<Type> have,
        string expected)
    {
        return have.Compose(
            actual =>
            {
                var attribs = actual.GetCustomAttributes(false).OfType<RouteAttribute>();
                Expect(attribs).To.Contain.Exactly(1)
                    .Matched.By(
                        a => a.Template == expected,
                        () => $"Expected {actual.Name} to have route {expected}");
            });
    }

    public static IMore<Type> Route(
        this IHave<Type> have,
        string member,
        string expected)
    {
        return have.Compose(
            actual =>
            {
                var method = actual.GetMethod(member);
                Expect(method).Not.To.Be.Null(() => $"Expected to find method {actual}.{method}");
                var attribs = method.GetCustomAttributes(false).OfType<RouteAttribute>();
                Expect(attribs).To.Contain.Exactly(1)
                    .Matched.By(
                        a => a.Template == expected,
                        () => $"Expected {actual}.{method} to have route {expected}");
            });
    }

    public class SupportingExtension
    {
        public string Member { get; set; }
        public IHave<Type> Continuation { get; set; }

        public AndSupportingExtension Supporting(HttpMethod method)
        {
            Continuation.AddMatcher(
                controllerType =>
                {
                    var supportedMethods = controllerType.GetMethod(Member)
                        ?.GetCustomAttributes(false)
                        .Select(attrib => attrib as IActionHttpMethodProvider)
                        .Where(a => a != null)
                        .SelectMany(a => a.HttpMethods)
                        .Distinct()
                        .ToArray();
                    var passed = supportedMethods
                            ?.Any(m => m.Equals(method.Method, StringComparison.OrdinalIgnoreCase))
                        ?? false;
                    return new MatcherResult(
                        passed,
                        () => $"Expected {controllerType}.{Member} to support HttpMethod {method}"
                    );
                });
            return Next();
        }

        public SupportingExtension With => this;

        public SupportingExtension Route(string expected)
        {
            Continuation.AddMatcher(
                controllerType =>
                {
                    var routes = controllerType.GetMethod(Member)
                        ?.GetCustomAttributes(false)
                        .OfType<RouteAttribute>()
                        .Select(a => a.Template)
                        .ToArray();
                    var passed = routes.Contains(expected);
                    return new MatcherResult(
                        passed,
                        () =>
                        {
                            var start = $"Expected {controllerType}.{Member} to have route '{expected}'";
                            var count = routes.Count();
                            var no = count == 0
                                ? "no "
                                : "";
                            var s = count == 1
                                ? ""
                                : "s";
                            var colon = count > 0
                                ? ":"
                                : "";
                            return new[]
                                {
                                    start,
                                    $"Have {no}route{s}{colon}"
                                }
                                .Concat(routes.Select(r => $" - {r}"))
                                .JoinWith("\n");
                        }
                    );
                });
            return this;
        }

        private AndSupportingExtension Next()
        {
            return new AndSupportingExtension(Continuation, Member);
        }
    }

    public class AndSupportingExtension
    {
        public string Member { get; set; }
        public IHave<Type> Continuation { get; set; }

        public AndSupportingExtension(
            IHave<Type> continuation,
            string member)
        {
            Continuation = continuation;
            Member = member;
        }

        public AndSupportingExtension And(HttpMethod method)
        {
            return (new SupportingExtension()
            {
                Member = Member,
                Continuation = Continuation
            }).Supporting(method);
        }
    }
}