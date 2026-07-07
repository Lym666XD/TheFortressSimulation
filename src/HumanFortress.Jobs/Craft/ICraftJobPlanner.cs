namespace HumanFortress.Jobs.Craft;

internal interface ICraftJobPlanner
{
    int DequeuePlannedJobs(int max, IList<PlannedCraftJob> into);
}
