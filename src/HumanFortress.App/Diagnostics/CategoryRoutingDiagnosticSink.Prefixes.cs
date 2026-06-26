namespace HumanFortress.App.Diagnostics;

internal sealed partial class CategoryRoutingDiagnosticSink
{
    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
