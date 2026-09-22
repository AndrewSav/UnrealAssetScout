using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace UnrealAssetScout.Incremental;

// Reads the stored hash of every pak entry in one batch on a pool of reader threads, because each is
// a small read at a scattered offset, and on a cold cache reading them one at a time leaves the drive
// idle between round trips. Called by SourceFingerprintIndex. The fallback for a container that is
// not a plain file is only ever called on the calling thread, one request at a time, so it need not
// be thread-safe.
internal static class PakInlineHeaderBatchReader
{
    private const int ReaderThreads = 64;

    internal static string?[] ReadFingerprints(IReadOnlyList<PakInlineHeaderRequest> requests, Func<int, string?> fallback)
    {
        var fingerprints = new string?[requests.Count];
        var openable = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var readable = new List<int>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            var path = requests[index].ContainerPath;
            if (path is not null && !openable.TryGetValue(path, out _))
                openable[path] = CanOpen(path);

            if (path is not null && openable[path])
                readable.Add(index);
            else
                fingerprints[index] = fallback(index);
        }

        readable.Sort((left, right) =>
        {
            var byContainer = string.Compare(requests[left].ContainerPath, requests[right].ContainerPath, StringComparison.OrdinalIgnoreCase);
            return byContainer != 0 ? byContainer : requests[left].Offset.CompareTo(requests[right].Offset);
        });

        var unopened = new ConcurrentBag<int>();
        ExceptionDispatchInfo? failure = null;
        var next = -1;

        // Dedicated threads with a synchronous handle each, rather than overlapped reads: a read
        // that misses the file cache blocks its caller even on an overlapped handle, and callers
        // sharing one synchronous handle are serialized on it.
        var threads = new List<Thread>();
        for (var count = 0; count < Math.Min(ReaderThreads, readable.Count); count++)
        {
            var thread = new Thread(() =>
            {
                string? currentPath = null;
                SafeFileHandle? currentHandle = null;
                try
                {
                    int position;
                    while ((position = Interlocked.Increment(ref next)) < readable.Count)
                    {
                        var index = readable[position];
                        var request = requests[index];
                        if (!string.Equals(request.ContainerPath, currentPath, StringComparison.OrdinalIgnoreCase))
                        {
                            currentHandle?.Dispose();
                            currentPath = request.ContainerPath;
                            currentHandle = TryOpen(currentPath!);
                        }

                        if (currentHandle is null)
                            unopened.Add(index);
                        else
                            fingerprints[index] = ReadFingerprint(currentHandle, request);
                    }
                }
                catch (Exception e)
                {
                    Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(e), null);
                }
                finally
                {
                    currentHandle?.Dispose();
                }
            }) { IsBackground = true };
            threads.Add(thread);
            thread.Start();
        }

        foreach (var thread in threads)
            thread.Join();

        failure?.Throw();

        foreach (var index in unopened)
            fingerprints[index] = fallback(index);

        return fingerprints;
    }

    private static bool CanOpen(string path)
    {
        using var handle = TryOpen(path);
        return handle is not null;
    }

    private static SafeFileHandle? TryOpen(string path)
    {
        try
        {
            return File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.RandomAccess);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? ReadFingerprint(SafeFileHandle handle, PakInlineHeaderRequest request)
    {
        var header = new byte[request.HashOffset + PakInlineHeaderLayout.HashSize];
        try
        {
            var filled = 0;
            while (filled < header.Length)
            {
                var read = RandomAccess.Read(handle, header.AsSpan(filled), request.Offset + filled);
                if (read == 0)
                    return null;

                filled += read;
            }
        }
        catch (IOException)
        {
            return null;
        }

        return PakInlineHeaderFingerprints.LayoutMatches(header, request.CompressedSize, request.UncompressedSize)
            ? PakInlineHeaderFingerprints.ExtractHash(header, request.HashOffset)
            : null;
    }
}
