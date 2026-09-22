using AaxDecrypter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace FileLiberator.Tests;

[TestClass]
public class DashKeySelectorTests
{
    private static readonly Guid[] Ids =
    [
        Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
        Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"),
        Guid.Parse("22334455-6677-8899-aabb-ccddeeff0011")
    ];
    private static KeyData[] Keys() => Ids.Select((id, index) =>
        new KeyData(id.ToByteArray(bigEndian: true), Enumerable.Repeat((byte)(index + 1), 16).ToArray())).ToArray();

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void SelectingAnyPositionPreservesTheOriginalCollection(int index)
    {
        var keys = Keys();
        var before = keys.ToArray();
        var selected = DashKeySelector.Select(keys, Ids[index]);
        Assert.AreSame(before[index].KeyPart1, selected.KeyId);
        Assert.AreSame(before[index].KeyPart2, selected.Key);
        CollectionAssert.AreEqual(before, keys);
    }

    [TestMethod]
    public void ReusingTheSameLicenseCanSelectEveryOriginalKeyInAnyOrder()
    {
        var keys = Keys();
        foreach (int index in new[] { 2, 0, 1, 2 })
        {
            var selected = DashKeySelector.Select(keys, Ids[index]);
            CollectionAssert.AreEqual(Ids[index].ToByteArray(bigEndian: true), selected.KeyId);
            CollectionAssert.AreEqual(Enumerable.Repeat((byte)(index + 1), 16).ToArray(), selected.Key);
        }
    }

    [TestMethod]
    public void MissingSelectedKeyDoesNotChangeTheCollection()
    {
        var keys = Keys();
        keys[2] = new KeyData(keys[2].KeyPart1);
        var before = keys.ToArray();
        Assert.ThrowsExactly<InvalidOperationException>(() => DashKeySelector.Select(keys, Ids[2]));
        CollectionAssert.AreEqual(before, keys);
    }

    [TestMethod]
    public void UnmatchedKidRemainsAnErrorWithoutChangingKeys()
    {
        var keys = Keys();
        var before = keys.ToArray();
        Assert.ThrowsExactly<InvalidOperationException>(() => DashKeySelector.Select(keys, Guid.Empty));
        CollectionAssert.AreEqual(before, keys);
    }

    [TestMethod]
    public void MalformedKidStillRejectsRatherThanBeingSilentlyIgnored()
    {
        var keys = Keys();
        keys[2] = new KeyData(new byte[15], new byte[16]);
        var before = keys.ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => DashKeySelector.Select(keys, Ids[0]));
        CollectionAssert.AreEqual(before, keys);
    }

    [TestMethod]
    public void EmptyCollectionRemainsAnError()
        => Assert.ThrowsExactly<InvalidOperationException>(() => DashKeySelector.Select([], Guid.Empty));
}
