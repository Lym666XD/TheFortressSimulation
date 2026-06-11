namespace HumanFortress.Jobs.Craft;

public interface ICraftJobPlanner
{
    int DequeuePlannedJobs(int max, IList<PlannedCraftJob> into);
}
