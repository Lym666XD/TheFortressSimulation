using HumanFortress.Contracts.Runtime;

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
            _log);
    }
}
