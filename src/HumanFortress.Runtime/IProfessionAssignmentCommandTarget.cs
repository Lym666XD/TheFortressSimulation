namespace HumanFortress.Runtime;

public interface IProfessionAssignmentCommandTarget
{
    void SetProfessionWeight(Guid workerId, string professionId, int weight);
}
