using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class PakInlineHeaderBatchReaderTests
{
    private const int ModernHashOffset = 28;

    [Fact]
    public void ReadFingerprints_ReturnsEachRequestsStoredHash_WhateverTheRequestOrder()
    {
        // Offsets out of order, unaligned, and far apart, so a sort or a batching step that loses
        // track of which result belongs to which request shows up as a swapped hash.
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "a.pak");
        WriteContainer(path, 3_000_000, (2_500_013, 100, 200, 0x33), (0, 10, 20, 0x11), (4_099, 30, 40, 0x22));

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(
            [Request(path, 2_500_013, 100, 200), Request(path, 0, 10, 20), Request(path, 4_099, 30, 40)],
            _ => throw new InvalidOperationException("the container exists, so no fallback is expected"));

        AssertFingerprints([Hash(0x33), Hash(0x11), Hash(0x22)], fingerprints);
    }

    [Fact]
    public void ReadFingerprints_KeepsContainersApart()
    {
        // The same offset in both, so only the container tells the two hashes apart.
        using var temp = new TempDir();
        var first = System.IO.Path.Combine(temp.Path, "first.pak");
        var second = System.IO.Path.Combine(temp.Path, "second.pak");
        WriteContainer(first, 8_192, (512, 1, 2, 0x41));
        WriteContainer(second, 8_192, (512, 1, 2, 0x42));

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(
            [Request(second, 512, 1, 2), Request(first, 512, 1, 2)], _ => null);

        AssertFingerprints([Hash(0x42), Hash(0x41)], fingerprints);
    }

    [Fact]
    public void ReadFingerprints_ReturnsNullWhenTheStoredSizesDoNotMatch()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "a.pak");
        WriteContainer(path, 8_192, (0, 10, 20, 0x11), (1_000, 30, 40, 0x22));

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(
            [Request(path, 0, 11, 20), Request(path, 1_000, 30, 41)], _ => null);

        AssertFingerprints([null, null], fingerprints);
    }

    [Fact]
    public void ReadFingerprints_ReturnsNullForAnAllZeroHash()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "a.pak");
        WriteContainer(path, 8_192, (0, 10, 20, 0x00));

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints([Request(path, 0, 10, 20)], _ => null);

        AssertFingerprints([null], fingerprints);
    }

    [Fact]
    public void ReadFingerprints_ReturnsNullWhenTheHeaderRunsPastTheEndOfTheFile()
    {
        // The header starts inside the file but its hash runs past the end: a short read.
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "a.pak");
        var truncatedOffset = 8_192 - ModernHashOffset - 10;
        WriteContainer(path, 8_192, (0, 10, 20, 0x11), (truncatedOffset, 10, 20, 0x77));

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(
            [Request(path, 0, 10, 20), Request(path, truncatedOffset, 10, 20)], _ => null);

        AssertFingerprints([Hash(0x11), null], fingerprints);
    }

    [Fact]
    public void ReadFingerprints_UsesTheFallbackForAContainerThatCannotBeOpened()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "a.pak");
        var missing = System.IO.Path.Combine(temp.Path, "not-on-disk.pak");
        WriteContainer(path, 8_192, (0, 10, 20, 0x11));
        var fallbackCalls = new List<int>();

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(
            [Request(missing, 0, 10, 20), Request(path, 0, 10, 20), Request(missing, 64, 10, 20)],
            index =>
            {
                lock (fallbackCalls)
                    fallbackCalls.Add(index);
                return $"fallback-{index}";
            });

        AssertFingerprints(["fallback-0", Hash(0x11), "fallback-2"], fingerprints);
        Assert.Equal([0, 2], fallbackCalls.Order());
    }

    [Fact]
    public void ReadFingerprints_UsesTheFallbackForARequestWithNoContainerPath()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "a.pak");
        WriteContainer(path, 8_192, (0, 10, 20, 0x11));

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(
            [new PakInlineHeaderRequest(null, 0, 0, 0, 0), Request(path, 0, 10, 20)],
            index => $"fallback-{index}");

        AssertFingerprints(["fallback-0", Hash(0x11)], fingerprints);
    }

    [Fact]
    public void ReadFingerprints_ReadsThousandsOfHeadersConcurrentlyWithoutMixingThemUp()
    {
        // Enough headers that reads overlap, each with a hash unique to it, so a buffer shared
        // between concurrent reads shows up as a hash landing on the wrong request.
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "a.pak");
        const int count = 5_000;
        const int spacing = 700;
        var headers = Enumerable.Range(0, count)
            .Select(i => ((long) i * spacing, (long) i, (long) i + 1, i))
            .ToArray();
        WriteContainer(path, (long) count * spacing + 1_000, headers);

        var requests = headers.Reverse().Select(h => Request(path, h.Item1, h.Item2, h.Item3)).ToList();
        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(requests, _ => null);

        var expected = headers.Reverse().Select(h => UniqueHash(h.Item4)).ToArray();
        AssertFingerprints(expected, fingerprints);
    }

    private static void AssertFingerprints(string?[] expected, string?[] actual) =>
        Assert.Equal(expected.AsEnumerable(), actual.AsEnumerable());

    private static PakInlineHeaderRequest Request(string path, long offset, long compressedSize, long uncompressedSize) =>
        new(path, offset, compressedSize, uncompressedSize, ModernHashOffset);

    private static string Hash(byte fill) => Convert.ToBase64String(Enumerable.Repeat(fill, 20).ToArray());

    private static string UniqueHash(int seed) => Convert.ToBase64String(UniqueHashBytes(seed));

    private static byte[] UniqueHashBytes(int seed)
    {
        var bytes = new byte[20];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        BitConverter.GetBytes(~seed).CopyTo(bytes, 16);
        bytes[8] = 0x5A;
        return bytes;
    }

    private static void WriteContainer(string path, long length, params (long Offset, long Compressed, long Uncompressed, byte Fill)[] headers) =>
        WriteContainer(path, length, headers.Select(h => (h.Offset, h.Compressed, h.Uncompressed, Enumerable.Repeat(h.Fill, 20).ToArray())));

    private static void WriteContainer(string path, long length, (long Offset, long Compressed, long Uncompressed, int Seed)[] headers) =>
        WriteContainer(path, length, headers.Select(h => (h.Offset, h.Compressed, h.Uncompressed, UniqueHashBytes(h.Seed))));

    private static void WriteContainer(string path, long length, IEnumerable<(long Offset, long Compressed, long Uncompressed, byte[] Hash)> headers)
    {
        var bytes = new byte[length];
        foreach (var (offset, compressed, uncompressed, hash) in headers)
        {
            BitConverter.GetBytes(compressed).CopyTo(bytes, offset + 8);
            BitConverter.GetBytes(uncompressed).CopyTo(bytes, offset + 16);
            var fits = (int) Math.Min(hash.Length, length - offset - ModernHashOffset);
            hash.AsSpan(0, fits).CopyTo(bytes.AsSpan((int) offset + ModernHashOffset));
        }

        File.WriteAllBytes(path, bytes);
    }
}
