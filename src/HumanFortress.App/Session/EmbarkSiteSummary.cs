using SadRogue.Primitives;

namespace HumanFortress.App.Session;

internal readonly record struct EmbarkSiteSummary(
    Point Location,
    string BiomeName,
    float Elevation);
