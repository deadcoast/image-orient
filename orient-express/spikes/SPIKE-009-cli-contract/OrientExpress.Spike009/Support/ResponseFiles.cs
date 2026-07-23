using System.Text;

namespace OrientExpress.Spike009.Support;

/// <summary>
/// DOC-CLI response files: UTF-8, one argument per line, `#` comments and
/// blank lines ignored, no nesting. Expansion is explicit and testable rather
/// than delegated to parser internals.
/// </summary>
public static class ResponseFiles
{
    public static bool TryExpand(string[] args, out string[] expanded, out string? error)
    {
        var result = new List<string>(args.Length);
        foreach (var argument in args)
        {
            if (!argument.StartsWith('@') || argument.Length == 1)
            {
                result.Add(argument);
                continue;
            }

            var path = argument[1..];
            if (!File.Exists(path))
            {
                expanded = [];
                error = $"response file not found: {path}";
                return false;
            }

            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                {
                    result.Add(trimmed);
                }
            }
        }

        expanded = [.. result];
        error = null;
        return true;
    }
}
