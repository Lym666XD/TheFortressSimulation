namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct SimulationWorkDrawerData(
    bool HasWorld,
    SimulationJobsDebugData? Jobs,
    WorkforceDebugData Workforce,
    SimulationOrdersDebugData Orders,
    SimulationWorkshopDebugData Workshops)
{
    public static SimulationWorkDrawerData Empty { get; } = new(
        false,
        null,
        new WorkforceDebugData(
            Array.Empty<ProfessionDefinitionView>(),
            Array.Empty<ProfessionRosterEntryView>(),
            0,
            0),
        new SimulationOrdersDebugData(
            0,
            0,
            0,
            0,
            Array.Empty<RecentDesignationView>()),
        new SimulationWorkshopDebugData(
            Array.Empty<WorkshopSummaryView>(),
            0,
            0,
            0));
}
