namespace HumanFortress.Contracts.Runtime.Snapshots;

public readonly record struct RecentDesignationView(
    string Kind,
    string Description);

public readonly record struct SimulationOrdersDebugData(
    int ActiveHaulDesignations,
    int ActiveMiningDesignations,
    int ActiveConstructionSites,
    int ActiveBuildableDesignations,
    IReadOnlyList<RecentDesignationView> RecentDesignations);
