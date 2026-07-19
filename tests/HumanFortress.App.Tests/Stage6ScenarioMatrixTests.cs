using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("stage6-determinism")]
public sealed class Stage6ScenarioMatrixTests
{
    [TestMethod]
    [Timeout(240_000)]
    public void ProductionScenarioMatchesAcrossProcessesCachesGcJitAndWorkerCounts()
    {
        var root = TestRepositoryPaths.FindRepositoryRoot();
        var runner = FindRunner(root);
        var profile = Path.Combine(
            root,
            "benchmarks",
            "scenarios",
            "determinism-ci.v1.json");
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"humanfortress-stage6-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var baseline = Run(
                root,
                runner,
                profile,
                outputDirectory,
                "baseline-a",
                tieredCompilation: "0");
            var variants = new[]
            {
                Run(root, runner, profile, outputDirectory, "baseline-b", tieredCompilation: "0"),
                Run(root, runner, profile, outputDirectory, "cache-warm", tieredCompilation: "0", "--prime-derived-caches"),
                Run(root, runner, profile, outputDirectory, "forced-gc", tieredCompilation: "0", "--force-gc-every", "100"),
                Run(root, runner, profile, outputDirectory, "process-warm", tieredCompilation: "0", "--process-warm"),
                Run(root, runner, profile, outputDirectory, "workers-4", tieredCompilation: "0", "--transport-workers", "4"),
                Run(root, runner, profile, outputDirectory, "tiered-on", tieredCompilation: "1"),
            };

            foreach (var variant in variants)
                Compare(root, runner, baseline, variant);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string Run(
        string root,
        string runner,
        string profile,
        string outputDirectory,
        string id,
        string tieredCompilation,
        params string[] extraArguments)
    {
        var output = Path.Combine(outputDirectory, id + ".json");
        var arguments = new List<string>
        {
            "run",
            "--profile",
            profile,
            "--output",
            output,
            "--base-dir",
            root,
        };
        arguments.AddRange(extraArguments);
        Execute(
            root,
            runner,
            arguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_TieredCompilation"] = tieredCompilation,
            });
        Assert.IsTrue(File.Exists(output), $"Scenario variant '{id}' did not produce an artifact.");
        return output;
    }

    private static void Compare(
        string root,
        string runner,
        string baseline,
        string variant)
    {
        Execute(
            root,
            runner,
            new[]
            {
                "compare",
                "--left",
                baseline,
                "--right",
                variant,
            },
            environment: null);
    }

    private static void Execute(
        string root,
        string runner,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string>? environment)
    {
        var start = new ProcessStartInfo
        {
            FileName = ResolveDotnetHost(),
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add(runner);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        if (environment != null)
        {
            foreach (var pair in environment)
                start.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the Stage 6 scenario runner.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(180_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("Stage 6 scenario process exceeded the 180 second timeout.");
        }

        Task.WaitAll(stdout, stderr);
        Assert.AreEqual(
            0,
            process.ExitCode,
            $"Stage 6 scenario command failed.\nstdout:\n{stdout.Result}\nstderr:\n{stderr.Result}");
    }

    private static string FindRunner(string root)
    {
        var bin = Path.Combine(root, "tools", "HumanFortress.Scenarios", "bin");
        var candidates = Directory.Exists(bin)
            ? Directory.EnumerateFiles(
                    bin,
                    "HumanFortress.Scenarios.dll",
                    SearchOption.AllDirectories)
                .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("ref", StringComparer.Ordinal))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray()
            : Array.Empty<string>();
        Assert.IsTrue(
            candidates.Length > 0,
            "HumanFortress.Scenarios was not built. Build the solution before running Stage 6 tests.");
        return candidates[0];
    }

    private static string ResolveDotnetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var current = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(current)
            && string.Equals(
                Path.GetFileNameWithoutExtension(current),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        return "dotnet";
    }
}
