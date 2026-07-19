using HumanFortress.Content.Definitions;
using HumanFortress.Contracts.Jobs;
using HumanFortress.Jobs.Profession;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Composition;

internal sealed class FortressRuntimeWorkforce
{
    private FortressRuntimeWorkforce(ProfessionAssignments professionAssignments)
    {
        ProfessionAssignments = professionAssignments;
    }

    internal ProfessionAssignments ProfessionAssignments { get; }

    internal static FortressRuntimeWorkforce FromContent(
        World world,
        IProfessionRegistry? professions,
        string baseDir,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var professionRegistry = professions ?? ProfessionRegistryLoader.Load(baseDir, log);
        return new FortressRuntimeWorkforce(new ProfessionAssignments(professionRegistry, world.Creatures));
    }
}
