using System.Collections.Generic;
using HumanFortress.Contracts.Runtime;

namespace HumanFortress.App.UI;

internal sealed partial class UiStore
{
    public UiConstructionShape SelectedConstructionShape { get; set; } = UiConstructionShape.Wall;
    public MiningAction SelectedMiningAction { get; set; } = MiningAction.Dig;
    public string? SelectedBuildableConstructionId { get; set; } = null;
    public string? SelectedWorkshopCategory { get; set; } = null;
    public bool WorkshopBrowsingItems { get; set; } = false;
    public bool ConstructionMaterialDialogOpen { get; set; } = false;
    public List<RuntimeConstructionMaterialRequirement> ConstructionMaterialRequirements { get; } = new();
    public string? ConstructionResultMaterialId { get; set; } = null;

    public void ResetConstructionSelection()
    {
        ConstructionMaterialRequirements.Clear();
        ConstructionResultMaterialId = null;
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
