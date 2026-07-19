using System.Text;
using System.Text.Json;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentStore
{
    internal static RuntimeSaveSnapshotDocumentData ReadUnchecked(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Save snapshot directory must not be blank.", nameof(directory));

        var targetPath = Path.Combine(directory, DocumentFileName);
        if (!File.Exists(targetPath))
            throw new FileNotFoundException("Save snapshot document was not found.", targetPath);

        return RuntimeSaveSnapshotDocumentCodec.Deserialize(File.ReadAllText(targetPath, Encoding.UTF8));
    }

    private static RuntimeSaveSlotManifestData ReadSlotManifestUnchecked(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Save snapshot directory must not be blank.", nameof(directory));

        var targetPath = Path.Combine(directory, SlotManifestFileName);
        if (!File.Exists(targetPath))
            throw new FileNotFoundException("Save slot manifest was not found.", targetPath);

        return RuntimeSaveSlotManifestCodec.Deserialize(File.ReadAllText(targetPath, Encoding.UTF8));
    }

    private static void ValidateSlotManifest(string directory, RuntimeSaveSnapshotDocumentData document)
    {
        var validation = ValidateSlotManifestNoThrow(directory, document);
        if (validation.Success)
            return;

        var firstIssue = validation.Issues.FirstOrDefault();
        throw new InvalidDataException(
            $"Save slot manifest failed validation: {firstIssue.Section}: {firstIssue.Message}");
    }

    private static RuntimeSaveSnapshotDocumentValidationResultData ValidateSlotManifestNoThrow(
        string directory,
        RuntimeSaveSnapshotDocumentData document)
    {
        try
        {
            var manifest = ReadSlotManifestUnchecked(directory);
            return RuntimeSaveSlotManifestVerifier.Validate(manifest, document);
        }
        catch (Exception ex) when (IsDirectoryReadException(ex))
        {
            return BuildDirectoryFailure("slot.manifest", ex);
        }
    }

    private static void ValidateDocument(RuntimeSaveSnapshotDocumentData document)
    {
        var validation = RuntimeSaveSnapshotDocumentVerifier.Validate(document);
        if (validation.Success)
            return;

        var firstIssue = validation.Issues.FirstOrDefault();
        throw new InvalidDataException(
            $"Save snapshot document failed validation: {firstIssue.Section}: {firstIssue.Message}");
    }

    private static void WriteTextDurably(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void ReplaceOrMove(string tempPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    private static bool IsDirectoryReadException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException
            or JsonException;
    }

    private static RuntimeSaveSnapshotDocumentValidationResultData BuildDirectoryFailure(
        string section,
        Exception ex)
    {
        return new RuntimeSaveSnapshotDocumentValidationResultData(
            false,
            new[]
            {
                new RuntimeSaveSnapshotDocumentIssueData(
                    section,
                    null,
                    ex.Message)
            });
    }
}
