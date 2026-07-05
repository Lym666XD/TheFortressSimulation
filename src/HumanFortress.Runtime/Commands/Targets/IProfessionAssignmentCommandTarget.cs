namespace HumanFortress.Runtime.Commands;

internal interface IProfessionAssignmentCommandTarget
{
    void SetProfessionWeight(Guid workerId, string professionId, int weight);
}
