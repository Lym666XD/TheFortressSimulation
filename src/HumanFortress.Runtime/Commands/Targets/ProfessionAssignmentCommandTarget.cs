using HumanFortress.Runtime.Diff;

namespace HumanFortress.Runtime.Commands;

internal sealed class ProfessionAssignmentCommandTarget : IProfessionAssignmentCommandTarget
{
    private const string SystemId = "Runtime.ProfessionCommand";

    private readonly ProfessionAssignmentDiffLog _professionDiffLog;

    internal ProfessionAssignmentCommandTarget(ProfessionAssignmentDiffLog professionDiffLog)
    {
        _professionDiffLog = professionDiffLog ?? throw new ArgumentNullException(nameof(professionDiffLog));
    }

    void IProfessionAssignmentCommandTarget.SetProfessionWeight(Guid workerId, string professionId, int weight)
    {
        _professionDiffLog.AddSetWeight(workerId, professionId, weight, SystemId);
    }
}
