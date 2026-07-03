namespace HumanFortress.Core.Commands;

/// <summary>
/// Rehydrates executable commands from immutable replay/save command records.
/// Concrete command registries live in Runtime because command implementations
/// and content-aware payload decoding are runtime-owned.
/// </summary>
public interface ICommandReplayFactory
{
    bool TryCreateCommand(
        CommandReplayRecord record,
        out ICommand? command,
        out string? errorMessage);
}
