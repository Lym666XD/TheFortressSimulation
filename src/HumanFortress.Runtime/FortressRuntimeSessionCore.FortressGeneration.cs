using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Runtime.WorldGeneration;

namespace HumanFortress.Runtime;

internal sealed partial class FortressRuntimeSessionCore
{
    RuntimeFortressGenerationResult IFortressRuntimeSessionBootstrapPort.GenerateAndFillFortressWorld(
        RuntimeFortressGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RuntimeFortressGenerationRunner.GenerateAndFill(
            request,
            _runtimeContentSnapshot,
            FillRuntimeWorld,
            _log,
            DiagnosticHub.Sink);
    }
}
