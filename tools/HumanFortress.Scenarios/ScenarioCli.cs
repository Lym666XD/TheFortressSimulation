namespace HumanFortress.Scenarios;

internal static class ScenarioCli
{
    internal static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            return args[0] switch
            {
                "run" => RunScenario(args[1..]),
                "compare" => Compare(args[1..]),
                "create-journal" => CreateJournal(args[1..]),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static int RunScenario(string[] args)
    {
        var options = ParseOptions(
            args,
            valueOptions: new HashSet<string>(StringComparer.Ordinal)
            {
                "--profile",
                "--output",
                "--base-dir",
                "--transport-workers",
                "--force-gc-every",
            },
            flagOptions: new HashSet<string>(StringComparer.Ordinal)
            {
                "--prime-derived-caches",
                "--process-warm",
            });
        var profilePath = Require(options, "--profile");
        var outputPath = Require(options, "--output");
        var baseDirectory = options.Values.TryGetValue("--base-dir", out var configuredBase)
            ? Path.GetFullPath(configuredBase)
            : FindRepositoryRoot();
        var transportWorkers = ParseNonNegativeInt(options, "--transport-workers", 1);
        if (transportWorkers == 0)
            throw new ArgumentOutOfRangeException("--transport-workers", "Transport workers must be positive.");
        var forceGcEvery = ParseNonNegativeInt(options, "--force-gc-every", 0);

        var inputs = ScenarioInputLoader.Load(profilePath);
        var artifact = ScenarioRunEngine.Run(
            inputs,
            new ScenarioRunOptions(
                baseDirectory,
                transportWorkers,
                options.Flags.Contains("--prime-derived-caches"),
                forceGcEvery,
                options.Flags.Contains("--process-warm")));
        ScenarioRunEngine.WriteArtifact(outputPath, artifact);
        Console.WriteLine(
            $"scenario={artifact.Identity.ProfileId} ticks={artifact.Deterministic.TotalTicks} "
            + $"hash={artifact.Deterministic.ReplayCheckpoints[^1].AggregateHash} "
            + $"p95_us={artifact.Performance.TickMicroseconds.P95} "
            + $"alloc_bytes={artifact.Performance.TotalAllocatedBytes}");
        return 0;
    }

    private static int Compare(string[] args)
    {
        var options = ParseOptions(
            args,
            valueOptions: new HashSet<string>(StringComparer.Ordinal)
            {
                "--left",
                "--right",
            },
            flagOptions: new HashSet<string>(StringComparer.Ordinal));
        var left = Require(options, "--left");
        var right = Require(options, "--right");
        if (ScenarioArtifactComparer.Compare(left, right, out var differences))
        {
            Console.WriteLine("deterministic evidence matches");
            return 0;
        }

        foreach (var difference in differences)
            Console.Error.WriteLine($"difference: {difference}");
        return 1;
    }

    private static int CreateJournal(string[] args)
    {
        var options = ParseOptions(
            args,
            valueOptions: new HashSet<string>(StringComparer.Ordinal)
            {
                "--output",
                "--id",
                "--tick",
                "--x",
                "--y",
                "--z",
            },
            flagOptions: new HashSet<string>(StringComparer.Ordinal));
        var output = Require(options, "--output");
        var id = options.Values.TryGetValue("--id", out var configuredId)
            ? configuredId
            : "stage6-determinism-journal-v1";
        var tick = ParseNonNegativeInt(options, "--tick", 25);
        var x = ParseNonNegativeInt(options, "--x", 3);
        var y = ParseNonNegativeInt(options, "--y", 3);
        var z = ParseNonNegativeInt(options, "--z", 1);
        ScenarioJournalTemplate.Write(output, id, (ulong)tick, new SadRogue.Primitives.Point(x, y), z);
        Console.WriteLine(Path.GetFullPath(output));
        return 0;
    }

    private static ParsedOptions ParseOptions(
        string[] args,
        IReadOnlySet<string> valueOptions,
        IReadOnlySet<string> flagOptions)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            if (valueOptions.Contains(option))
            {
                if (!values.TryAdd(
                        option,
                        ++index < args.Length
                            ? args[index]
                            : throw new ArgumentException($"{option} requires a value.")))
                {
                    throw new ArgumentException($"{option} was provided more than once.");
                }
                continue;
            }

            if (flagOptions.Contains(option))
            {
                if (!flags.Add(option))
                    throw new ArgumentException($"{option} was provided more than once.");
                continue;
            }

            throw new ArgumentException($"Unknown option '{option}'.");
        }

        return new ParsedOptions(values, flags);
    }

    private static string Require(ParsedOptions options, string name)
    {
        if (!options.Values.TryGetValue(name, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.");
        }

        return value;
    }

    private static int ParseNonNegativeInt(
        ParsedOptions options,
        string name,
        int fallback)
    {
        if (!options.Values.TryGetValue(name, out var value))
            return fallback;
        if (!int.TryParse(
                value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
            || parsed < 0)
        {
            throw new ArgumentException($"{name} must be a non-negative integer.");
        }

        return parsed;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "HumanFortress.sln"))
                    && Directory.Exists(Path.Combine(current.FullName, "content"))
                    && Directory.Exists(Path.Combine(current.FullName, "data")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the HumanFortress repository root; pass --base-dir explicitly.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("HumanFortress deterministic scenario runner");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run --profile <json> --output <json> [--base-dir <path>]");
        Console.WriteLine("      [--transport-workers <n>] [--prime-derived-caches]");
        Console.WriteLine("      [--force-gc-every <ticks>] [--process-warm]");
        Console.WriteLine("  compare --left <artifact> --right <artifact>");
        Console.WriteLine("  create-journal --output <json> [--id <id>] [--tick <n>]");
        Console.WriteLine("      [--x <n>] [--y <n>] [--z <n>]");
    }

    private sealed record ParsedOptions(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Flags);
}
