using System.Text;

var originalOut = Console.Out;
var originalError = Console.Error;
using var captureWriter = new StringWriter();
using var mirrorWriter = new MirrorTextWriter(originalOut, captureWriter);

Console.SetOut(mirrorWriter);
Console.SetError(mirrorWriter);

try
{
    TransportConstructionCraftRegressionTests.RunAll();
    MiningItemsDiffRegressionTests.RunAll();
    CoreRuntimeSmokeTests.RunAll();
    ArchitectureBoundarySmokeTests.RunAll();
    DeterministicAuthoritySmokeTests.RunAll();
    HumanFortress.App.PhaseTests.RunAllPhaseTests();
}
finally
{
    Console.SetOut(originalOut);
    Console.SetError(originalError);
}

var output = captureWriter.ToString();
return output.Contains("✗", StringComparison.Ordinal)
    || output.Contains("❌ FAIL", StringComparison.Ordinal)
    ? 1
    : 0;

internal sealed class MirrorTextWriter : TextWriter
{
    private readonly TextWriter _primary;
    private readonly TextWriter _secondary;

    public MirrorTextWriter(TextWriter primary, TextWriter secondary)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
    }

    public override Encoding Encoding => _primary.Encoding;

    public override void Write(char value)
    {
        _primary.Write(value);
        _secondary.Write(value);
    }

    public override void Write(string? value)
    {
        _primary.Write(value);
        _secondary.Write(value);
    }

    public override void WriteLine(string? value)
    {
        _primary.WriteLine(value);
        _secondary.WriteLine(value);
    }

    public override void Flush()
    {
        _primary.Flush();
        _secondary.Flush();
    }
}
