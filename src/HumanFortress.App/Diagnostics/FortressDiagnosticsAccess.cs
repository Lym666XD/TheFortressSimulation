namespace HumanFortress.App.Diagnostics;

internal sealed class FortressDiagnosticsAccess : IFortressDiagnosticsAccess
{
    public DiagnosticSnapshot GetSnapshot()
    {
        return Logger.GetSnapshot();
    }
}
