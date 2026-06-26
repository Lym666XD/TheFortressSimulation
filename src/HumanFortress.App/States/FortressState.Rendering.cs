using HumanFortress.App.Rendering;

namespace HumanFortress.App.States;

internal sealed partial class FortressState
{
    private void DrawUI()
    {
        FortressFrameRenderer.Render(_viewContexts.CreateFrame());
    }
}
