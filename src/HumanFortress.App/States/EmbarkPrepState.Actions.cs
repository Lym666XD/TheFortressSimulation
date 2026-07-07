using HumanFortress.App.Session;

namespace HumanFortress.App.States
{
    internal sealed partial class EmbarkPrepState
    {
        private void StartEmbark()
        {
            if (!FortressSessionSizeRules.IsValid(_fortressSize))
            {
                Logger.Warning("UI.EmbarkPrep", $"[EmbarkPrepState] WARNING: Invalid fortress size {_fortressSize}, defaulting to {FortressSessionSizeRules.DefaultFortressSize}");
                _fortressSize = FortressSessionSizeRules.DefaultFortressSize;
            }

            Logger.Log($"[EmbarkPrepState] Starting embark at {SelectedTile} with size {_fortressSize}x{_fortressSize}");
            _session.ConfigureEmbark(SelectedTile, _fortressSize);

            Logger.Log("[EmbarkPrepState] Changing state to FortressPlay");
            _navigator.ShowFortressPlay();
        }
    }
}
