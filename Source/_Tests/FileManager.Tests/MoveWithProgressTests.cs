using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FileManager.Tests;

[TestClass]
public class MoveWithProgressTests
{
    private string directory = null!;
    private string source = null!;
    private string destination = null!;
    private static readonly byte[] Original = [91, 92, 93, 94];

    [TestInitialize]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "libation-move-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        source = Path.Combine(directory, "source.m4b");
        destination = Path.Combine(directory, "destination.m4b");
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);
    private static MoveWithProgress CopyMover() => new(entry => entry is FileInfo ? "source-device" : "destination-device");
    private void PrepareLargeCopy()
    {
        using (var file = File.Create(source)) { file.SetLength(20 * 1024 * 1024); file.WriteByte(77); }
        File.WriteAllBytes(destination, Original);
    }
    private void AssertOriginalsRetained()
    {
        Assert.IsTrue(File.Exists(source));
        CollectionAssert.AreEqual(Original, File.ReadAllBytes(destination));
        Assert.AreEqual(2, Directory.GetFiles(directory).Length, "No abandoned staging output should remain after a handled failure.");
    }

    [TestMethod]
    public async Task CancelledProgressKeepsExistingBookAndSource()
    {
        PrepareLargeCopy();
        var mover = CopyMover(); mover.MoveProgress += (_, args) => args.Continue = false;
        Assert.IsFalse(await mover.MoveAsync(source, destination, true));
        AssertOriginalsRetained();
    }

    [TestMethod]
    public async Task ProgressFailureKeepsExistingBookAndOriginalException()
    {
        PrepareLargeCopy();
        var expected = new IOException("synthetic progress failure");
        var mover = CopyMover(); mover.MoveProgress += (_, _) => throw expected;
        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => mover.MoveAsync(source, destination, true));
        Assert.AreSame(expected, actual);
        AssertOriginalsRetained();
    }

    [TestMethod]
    public async Task CancellationDuringCopyKeepsExistingBookAndSource()
    {
        PrepareLargeCopy();
        using var cancellation = new CancellationTokenSource();
        var mover = CopyMover(); mover.MoveProgress += (_, _) => cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => mover.MoveAsync(source, destination, true, cancellation.Token));
        AssertOriginalsRetained();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PreCancelledMoveDoesNotChangeEitherFile(bool sameDevice)
    {
        PrepareLargeCopy();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var mover = sameDevice ? new MoveWithProgress(_ => "same") : CopyMover();
        await Assert.ThrowsAsync<OperationCanceledException>(() => mover.MoveAsync(source, destination, true, cancellation.Token));
        AssertOriginalsRetained();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(30000)]
    public async Task SuccessfulOverwritePublishesExactBytesAfterCopyFinishes(int length)
    {
        byte[] expected = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        File.WriteAllBytes(source, expected); File.WriteAllBytes(destination, Original);
        bool oldBookAlwaysVisible = true;
        var mover = CopyMover(); mover.MoveProgress += (_, _) =>
            oldBookAlwaysVisible &= File.ReadAllBytes(destination).SequenceEqual(Original);
        Assert.IsTrue(await mover.MoveAsync(source, destination, true));
        Assert.IsTrue(oldBookAlwaysVisible, "Readers must retain the old book until the replacement is ready.");
        Assert.IsFalse(File.Exists(source));
        CollectionAssert.AreEqual(expected, File.ReadAllBytes(destination));
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public async Task ExistingDestinationWithoutOverwriteIsPreserved()
    {
        PrepareLargeCopy();
        await Assert.ThrowsExactlyAsync<IOException>(() => CopyMover().MoveAsync(source, destination));
        AssertOriginalsRetained();
    }

    [TestMethod]
    public async Task NewDestinationCreatedDuringCopyIsNotClobberedWithoutOverwrite()
    {
        File.WriteAllBytes(source, new byte[30000]);
        bool created = false;
        var mover = CopyMover(); mover.MoveProgress += (_, _) =>
        {
            if (!created) { File.WriteAllBytes(destination, Original); created = true; }
        };
        await Assert.ThrowsAsync<IOException>(() => mover.MoveAsync(source, destination));
        AssertOriginalsRetained();
    }

    [TestMethod]
    public async Task MissingDestinationReceivesExactFile()
    {
        File.WriteAllBytes(source, Original);
        Assert.IsTrue(await CopyMover().MoveAsync(source, destination));
        Assert.IsFalse(File.Exists(source));
        CollectionAssert.AreEqual(Original, File.ReadAllBytes(destination));
    }

    [TestMethod]
    public async Task OwnerOnlyModeSurvivesTheCopyPathOnUnix()
    {
        if (OperatingSystem.IsWindows()) { Assert.Inconclusive("Unix permission contract; Windows ACL proof is separate."); return; }
        File.WriteAllBytes(source, Original);
        File.SetUnixFileMode(source, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Assert.IsTrue(await CopyMover().MoveAsync(source, destination));
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(destination));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(30000)]
    public async Task ProgressCanStopEvenTheLastOrEmptyBlock(int length)
    {
        File.WriteAllBytes(source, new byte[length]); File.WriteAllBytes(destination, Original);
        var mover = CopyMover(); mover.MoveProgress += (_, args) => args.Continue = false;
        Assert.IsFalse(await mover.MoveAsync(source, destination, true));
        AssertOriginalsRetained();
    }

    [TestMethod]
    public async Task UnknownDevicesUseStagingInsteadOfAssumingSameVolume()
    {
        PrepareLargeCopy();
        var mover = new MoveWithProgress(_ => null);
        mover.MoveProgress += (_, args) => args.Continue = false;
        Assert.IsFalse(await mover.MoveAsync(source, destination, true));
        AssertOriginalsRetained();
    }

    [TestMethod]
    public async Task SamePathWithUnknownDeviceCannotDeleteItself()
    {
        File.WriteAllBytes(source, Original);
        Assert.IsTrue(await new MoveWithProgress(_ => null).MoveAsync(source, source, true));
        CollectionAssert.AreEqual(Original, File.ReadAllBytes(source));
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public async Task PublicationFailureKeepsSourceAndExistingDirectory()
    {
        File.WriteAllBytes(source, Original); Directory.CreateDirectory(destination);
        Exception? failure = null;
        try { await CopyMover().MoveAsync(source, destination, true); }
        catch (Exception ex) { failure = ex; }
        Assert.IsTrue(failure is IOException or UnauthorizedAccessException);
        CollectionAssert.AreEqual(Original, File.ReadAllBytes(source));
        Assert.IsTrue(Directory.Exists(destination));
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public async Task KnownSameVolumeRenameStillMovesExactBytes()
    {
        File.WriteAllBytes(source, Original);
        Assert.IsTrue(await new MoveWithProgress(_ => "same").MoveAsync(source, destination));
        Assert.IsFalse(File.Exists(source));
        CollectionAssert.AreEqual(Original, File.ReadAllBytes(destination));
    }

    [TestMethod]
    public void UnixDeviceLookupTreatsShellSyntaxAsLiteralFilename()
    {
        if (OperatingSystem.IsWindows()) { Assert.Inconclusive("Unix stat path contract."); return; }
        var lookup = typeof(MoveWithProgress).GetMethod("GetDeviceId", BindingFlags.NonPublic | BindingFlags.Static)!;
        string path = Path.Combine(directory, "book $(printf injected) `printf other` \"quoted\".m4b");
        File.WriteAllBytes(path, Original);
        var expected = lookup.Invoke(null, [new DirectoryInfo(directory)]) as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(expected));
        Assert.AreEqual(expected, lookup.Invoke(null, [new FileInfo(path)]) as string);
    }
}
