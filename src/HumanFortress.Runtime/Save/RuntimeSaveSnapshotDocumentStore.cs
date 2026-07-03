using System.Text;
using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotDocumentStore
{
    public const string DocumentFileName = "runtime_snapshot.json";

    public static void WriteAtomic(string directory, RuntimeSaveSnapshotDocumentData document)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Save snapshot directory must not be blank.", nameof(directory));

        Directory.CreateDirectory(directory);
        ValidateDocument(document);

        var json = RuntimeSaveSnapshotDocumentCodec.Serialize(document);
        var targetPath = Path.Combine(directory, DocumentFileName);
        var tempPath = targetPath + ".tmp";

        WriteTextDurably(tempPath, json);
        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    public static RuntimeSaveSnapshotDocumentData Read(string directory)
    {
        var document = ReadUnchecked(directory);
        ValidateDocument(document);
        return document;
    }

    internal static RuntimeSaveSnapshotDocumentData ReadUnchecked(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Save snapshot directory must not be blank.", nameof(directory));

        var targetPath = Path.Combine(directory, DocumentFileName);
        if (!File.Exists(targetPath))
            throw new FileNotFoundException("Save snapshot document was not found.", targetPath);

        return RuntimeSaveSnapshotDocumentCodec.Deserialize(File.ReadAllText(targetPath, Encoding.UTF8));
    }

    private static void WriteTextDurably(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
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
}
