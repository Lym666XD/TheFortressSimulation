namespace HumanFortress.App.Startup;

internal enum AppStartupCommand
{
    RunGame,
    LegacyTestShim,
    LegacyValidateShim,
    TestCrash,
    InitOnly
}

internal readonly record struct AppStartupOptions(
    AppStartupCommand Command,
    bool AutoDig,
    bool StrictContent,
    bool ContentWarningsAsErrors)
{
    internal static AppStartupOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Length > 0
            ? args[0] switch
            {
                "--test" => AppStartupCommand.LegacyTestShim,
                "--validate" => AppStartupCommand.LegacyValidateShim,
                "--test-crash" => AppStartupCommand.TestCrash,
                "--init-only" => AppStartupCommand.InitOnly,
                _ => AppStartupCommand.RunGame
            }
            : AppStartupCommand.RunGame;

        bool autoDig = HasFlag(args, "--auto-dig");
        bool contentWarningsAsErrors = HasFlag(args, "--content-warnings-as-errors");
        bool strictContent = contentWarningsAsErrors || HasFlag(args, "--strict-content");

        return new AppStartupOptions(
            command,
            autoDig,
            strictContent,
            contentWarningsAsErrors);
    }

    private static bool HasFlag(IEnumerable<string> args, string flag)
    {
        return args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
    }
}
