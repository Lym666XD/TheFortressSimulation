namespace HumanFortress.App.Diagnostics;

internal interface IFortressDiagnosticsAccess
{
    DiagnosticSnapshot GetSnapshot();
}
