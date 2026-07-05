using HumanFortress.Content.Definitions;
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

    internal static FortressRuntimeWorkforce Load(World world, string baseDir, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);

        var professionRegistry = ProfessionRegistryLoader.Load(baseDir, log);
        return new FortressRuntimeWorkforce(new ProfessionAssignments(professionRegistry, world.Creatures));
    }
}
