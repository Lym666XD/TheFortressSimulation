namespace HumanFortress.Core.Commands;

/// <summary>
/// Optional replay metadata for command wrappers that add runtime/session identity.
/// </summary>
public interface ICommandReplayIdentity
{
    long? CommandIdentitySequence { get; }
}
