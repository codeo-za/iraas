using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace IRAAS.Security;

public interface IWhitelist
{
    bool IsAllowed(string source);
}

public class Whitelist
    : IWhitelist
{
    private readonly Regex[] _expressions;

    public Whitelist(IAppSettings settings)
    {
        _expressions = (settings.DomainWhitelist ?? "")
            .Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
            .Select(rule => rule.Trim())
            .Where(glob => !string.IsNullOrWhiteSpace(glob))
            .Select(glob => new Regex(
                $"{MakeRegexFor(glob)}",
                RegexOptions.Compiled | RegexOptions.IgnoreCase
            )).ToArray();
    }

    private string MakeRegexFor(string glob)
    {
        return $@"^{
            Regex.Escape(glob)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".")
        }$";
    }

    public bool IsAllowed(string source)
    {
        if (_expressions.Length == 0 ||
            string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        var uri = new Uri(source);
        return _expressions.Any(re => re.Match(uri.Host).Success);
    }
}