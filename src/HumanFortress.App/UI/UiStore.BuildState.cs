using System.Collections.Generic;

namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public UiConstructionShape SelectedConstructionShape { get; set; } = UiConstructionShape.Wall;
    public MiningAction SelectedMiningAction { get; set; } = MiningAction.Dig;
    public string? SelectedBuildableConstructionId { get; set; } = null;
    public string? SelectedWorkshopCategory { get; set; } = null;
    public bool WorkshopBrowsingItems { get; set; } = false;
    public bool ConstructionMaterialDialogOpen { get; set; } = false;
    public List<string> ConstructionSelectedTags { get; } = new();
    public string? ConstructionPreferredMaterialId { get; set; } = null;

    public void ResetConstructionSelection()
    {
        ConstructionSelectedTags.Clear();
        ConstructionPreferredMaterialId = null;
    }

    public void ResetBuildableSelection()
    {
        SelectedBuildableConstructionId = null;
    }

    public void ResetWorkshopMenu()
    {
        SelectedWorkshopCategory = null;
        WorkshopBrowsingItems = false;
    }
}
