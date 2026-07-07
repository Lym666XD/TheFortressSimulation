internal static class TestRepositoryPaths
{
    internal static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HumanFortress.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not find repository root from {AppContext.BaseDirectory}.");
    }

    internal static IEnumerable<string> EnumerateSourceFiles(string directory)
    {
        return Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    internal static string RelativePath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
