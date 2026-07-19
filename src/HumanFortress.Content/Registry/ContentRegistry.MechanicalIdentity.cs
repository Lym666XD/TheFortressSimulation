namespace HumanFortress.Content.Registry;

internal sealed partial class ContentRegistry
{
    internal IReadOnlyList<string> GetLoadedMaterialCanonicalIds()
    {
        return _materials.GetCanonicalIdsSnapshot();
    }
}
