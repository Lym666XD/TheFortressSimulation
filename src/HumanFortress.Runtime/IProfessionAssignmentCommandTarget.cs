namespace HumanFortress.Runtime;

internal interface IProfessionAssignmentCommandTarget
{
    void SetProfessionWeight(Guid workerId, string professionId, int weight);
}
