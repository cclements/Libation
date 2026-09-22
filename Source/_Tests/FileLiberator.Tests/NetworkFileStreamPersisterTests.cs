using AaxDecrypter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace FileLiberator.Tests;

[TestClass]
public class NetworkFileStreamPersisterTests
{
    private string directory = null!;
    private string statePath = null!;
    private string mediaPath = null!;
    private static readonly Uri SyntheticUri = new("https://example.invalid/audio?synthetic-signature=fixture");

    [TestInitialize]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "libation-resume-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        statePath = Path.Combine(directory, "resume.json");
        mediaPath = Path.Combine(directory, "partial");
    }

    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);

    [TestMethod]
    public void NewResumeAndFinalCheckpointArePrivateAndReopenWithTheSameSchema()
    {
        using (var stream = CreateStream())
        using (var persister = new NetworkFileStreamPersister(stream, statePath))
        {
            AssertPrivate();
            stream.RequestHeaders["Synthetic-Header"] = "updated-fixture";
        }
        AssertPrivate();
        using var restored = new NetworkFileStreamPersister(statePath);
        Assert.AreEqual(SyntheticUri, restored.NetworkFileStream.Uri);
        Assert.AreEqual("updated-fixture", restored.NetworkFileStream.RequestHeaders["Synthetic-Header"]);
        Assert.AreEqual(0, restored.NetworkFileStream.WritePosition);
        Assert.AreEqual(2, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public void ReplacementOfLegacyReadableResumeBecomesPrivate()
    {
        WriteSnapshot(legacy: false);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(statePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        using (var persister = new NetworkFileStreamPersister(statePath))
            persister.NetworkFileStream.RequestHeaders["Synthetic-Header"] = "replacement-fixture";
        AssertPrivate();
        using var restored = new NetworkFileStreamPersister(statePath);
        Assert.AreEqual("replacement-fixture", restored.NetworkFileStream.RequestHeaders["Synthetic-Header"]);
        Assert.AreEqual(2, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OldAndCurrentResumeFilesPreserveOffsetsAndIdentityOnReopen(bool legacy)
    {
        WriteSnapshot(legacy);
        using (var restored = new NetworkFileStreamPersister(statePath))
        {
            Assert.AreEqual(2, restored.NetworkFileStream.WritePosition);
            Assert.AreEqual(4, restored.NetworkFileStream.ContentLength);
            Assert.AreEqual("\"synthetic-etag\"", restored.NetworkFileStream.EntityTag);
            Assert.AreEqual(legacy, restored.NetworkFileStream.ResourceIdentity is null);
            Assert.AreEqual(legacy, restored.NetworkFileStream.EffectiveResourceIdentity is null);
        }
        using var reopened = new NetworkFileStreamPersister(statePath);
        Assert.AreEqual(legacy, reopened.NetworkFileStream.ResourceIdentity is null);
        Assert.AreEqual(2, reopened.NetworkFileStream.WritePosition);
        Assert.AreEqual("ab", File.ReadAllText(mediaPath));
    }

    [TestMethod]
    public void NestedSnapshotPreservesOtherDocumentFields()
    {
        WriteSnapshot(legacy: false);
        var envelope = new JObject { ["unrelated"] = "keep", ["resume"] = JObject.Parse(File.ReadAllText(statePath)) };
        File.WriteAllText(statePath, envelope.ToString());
        using (var restored = new NetworkFileStreamPersister(statePath, "resume"))
            restored.NetworkFileStream.RequestHeaders["Synthetic-Header"] = "nested-fixture";
        var saved = JObject.Parse(File.ReadAllText(statePath));
        Assert.AreEqual("keep", saved["unrelated"]!.Value<string>());
        Assert.AreEqual("nested-fixture", saved["resume"]!["RequestHeaders"]!["Synthetic-Header"]!.Value<string>());
    }

    [TestMethod]
    public void DestinationSymlinkIsReplacedWithoutOverwritingItsTarget()
    {
        if (OperatingSystem.IsWindows()) { Assert.Inconclusive("Unix link behavior; Windows ACL/link evidence is separate."); return; }
        string unrelated = Path.Combine(directory, "unrelated");
        File.WriteAllText(unrelated, "preserve");
        File.CreateSymbolicLink(statePath, unrelated);
        using var stream = CreateStream();
        using var persister = new NetworkFileStreamPersister(stream, statePath);
        Assert.AreEqual("preserve", File.ReadAllText(unrelated));
        Assert.IsNull(new FileInfo(statePath).LinkTarget);
        AssertPrivate();
    }

    [TestMethod]
    public void FailedInitialPublicationLeavesNoTemporaryFileAndReleasesTheOwnedStream()
    {
        Directory.CreateDirectory(statePath);
        var stream = CreateStream();
        try
        {
            Exception? failure = null;
            try { using var persister = new NetworkFileStreamPersister(stream, statePath); }
            catch (Exception ex) { failure = ex; }
            Assert.IsTrue(failure is IOException or UnauthorizedAccessException);
            Assert.IsTrue(Directory.Exists(statePath));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
            using var exclusive = File.Open(mediaPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { stream.Dispose(); }
    }

    [TestMethod]
    public void StagingIsPrivateAndCompleteBeforeItReplacesThePreviousCheckpoint()
    {
        File.WriteAllText(statePath, "previous checkpoint");
        bool validated = false;
        PrivateFileWriter.WriteAllText(statePath, "{\"synthetic\":true}", staged =>
        {
            validated = true;
            Assert.AreEqual("previous checkpoint", File.ReadAllText(statePath));
            Assert.IsTrue(JObject.Parse(File.ReadAllText(staged))["synthetic"]!.Value<bool>());
            if (!OperatingSystem.IsWindows())
                Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(staged));
        });
        Assert.IsTrue(validated);
        Assert.AreEqual("{\"synthetic\":true}", File.ReadAllText(statePath));
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedStagingValidationRetainsPreviousCheckpointAndOriginalError(bool hasPrevious)
    {
        if (hasPrevious) File.WriteAllText(statePath, "previous checkpoint");
        var expected = new InvalidDataException("synthetic validation failure");
        var actual = Assert.ThrowsExactly<InvalidDataException>(() =>
            PrivateFileWriter.WriteAllText(statePath, "new synthetic checkpoint", _ => throw expected));
        Assert.AreSame(expected, actual);
        if (hasPrevious) Assert.AreEqual("previous checkpoint", File.ReadAllText(statePath));
        else Assert.IsFalse(File.Exists(statePath));
        Assert.AreEqual(hasPrevious ? 1 : 0, Directory.GetFiles(directory).Length);
    }

    private NetworkFileStream CreateStream() => new(mediaPath, SyntheticUri,
        requestHeaders: new Dictionary<string, string> { ["Synthetic-Header"] = "initial-fixture" });

    private void WriteSnapshot(bool legacy)
    {
        File.WriteAllText(mediaPath, "ab");
        using var stream = CreateStream();
        var json = JObject.FromObject(stream);
        json["WritePosition"] = 2;
        json["ContentLength"] = 4;
        json["EntityTag"] = "\"synthetic-etag\"";
        json["EffectiveResourceIdentity"] = json["ResourceIdentity"]!.DeepClone();
        if (legacy) { json.Remove("ResourceIdentity"); json.Remove("EffectiveResourceIdentity"); }
        File.WriteAllText(statePath, json.ToString(Formatting.Indented));
    }

    private void AssertPrivate()
    {
        if (!OperatingSystem.IsWindows())
            Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(statePath));
    }
}
