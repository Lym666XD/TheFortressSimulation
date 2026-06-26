namespace HumanFortress.App.UI;

internal sealed partial class OrdersUI
{
    private static readonly string[] MiningL3Lines =
    {
        "[Z] Dig",
        "[X] dig stairwell",
        "[C] dig ramp",
        "[V] dig channel",
        "[F] remove digging",
        "[,] cancel order"
    };

    private static readonly string[] LumberingL3Lines =
    {
        "[Z] lumber",
        "[,] cancel order"
    };

    private static readonly string[] GatherL3Lines =
    {
        "[Z] gather plant",
        "[X] remove plant",
        "[,] cancel order"
    };

    private static readonly string[] MasonryL3Lines =
    {
        "[Z] smooth",
        "[X] engrave",
        "[C] track",
        "[V] carve gap",
        "[,] cancel order"
    };

    private static readonly string[] HaulL3Lines =
    {
        "[Z] haul",
        "[X] emergency haul",
        "[,] cancel order"
    };

    private static readonly string[] CreatureL3Lines =
    {
        "[Z] hunting",
        "[X] kill",
        "[C] tame",
        "[V] rescue",
        "[,] cancel order"
    };

    private static readonly string[] OtherL3Lines =
    {
        "[Z] lock/disallow",
        "[X] unlock/allow",
        "[C] dump",
        "[V] remove dump",
        "[F] melt",
        "[T] remove melt",
        "[R] clean",
        "[,] cancel order"
    };
}
