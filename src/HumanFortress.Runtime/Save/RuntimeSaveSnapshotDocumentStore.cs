using HumanFortress.Contracts.Runtime.Save;

namespace HumanFortress.Runtime.Save;

internal static partial class RuntimeSaveSnapshotDocumentStore
{
    internal const string DocumentFileName = RuntimeSaveSlotFormat.SnapshotDocumentFileName;
    internal const string SlotManifestFileName = RuntimeSaveSlotFormat.ManifestFileName;

    internal static void WriteAtomic(string directory, RuntimeSaveSnapshotDocumentData document)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Save snapshot directory must not be blank.", nameof(directory));

        Directory.CreateDirectory(directory);
        ValidateDocument(document);

        var json = RuntimeSaveSnapshotDocumentCodec.Serialize(document);
        var targetPath = Path.Combine(directory, DocumentFileName);
        var tempPath = targetPath + ".tmp";

        WriteTextDurably(tempPath, json);
        ReplaceOrMove(tempPath, targetPath);

        var slotManifest = RuntimeSaveSlotManifestBuilder.Build(document);
        var slotJson = RuntimeSaveSlotManifestCodec.Serialize(slotManifest);
        var slotPath = Path.Combine(directory, SlotManifestFileName);
        var slotTempPath = slotPath + ".tmp";

        WriteTextDurably(slotTempPath, slotJson);
        ReplaceOrMove(slotTempPath, slotPath);
    }

    internal static RuntimeSaveSnapshotDocumentData Read(string directory)
    {
        var document = ReadUnchecked(directory);
        ValidateSlotManifest(directory, document);
        ValidateDocument(document);
        return document;
    }

    internal static RuntimeSaveSnapshotDocumentValidationResultData ValidateDirectory(string directory)
    {
        return InspectDirectory(directory).Validation;
    }
}
