using System;
using System.Collections.Generic;

namespace UnrealAssetScout.Export.Exporters;

// Lazily creates and caches audio middleware providers used by simple export routines.
// Called by AudioBankExporter and CriWareExporter so repeated exports can reuse provider initialization
// results instead of reconstructing providers for every file.
internal static class AudioProviderFactory
{
    // These caches store both successful and failed initialization results so we do not
    // repeatedly retry provider construction on every export after a failure.
    // Assumption: UnrealAssetScout uses a single provider/game-directory context per process run,
    // so these cached middleware providers never need to be reset mid-run.
    private static readonly Dictionary<Type, object> Caches = new();

    public static T? GetProvider<T>(ExportItemInfo item) where T : class
        => GetOrCreate<T>(item, context => (T)Activator.CreateInstance(typeof(T), context.Provider, context.GameDirectory)!);

    private static T? GetOrCreate<T>(ExportItemInfo item, Func<ExportItemInfo, T> factory) where T : class
    {
        if (!Caches.TryGetValue(typeof(T), out var cacheObj))
        {
            cacheObj = new ProviderCache<T>();
            Caches[typeof(T)] = cacheObj;
        }

        var cache = (ProviderCache<T>)cacheObj;
        if (cache.InitializationAttempted)
            return cache.Value;

        cache.InitializationAttempted = true;
        try
        {
            cache.Value = factory(item);
        }
        catch
        {
            cache.Value = null;
        }

        return cache.Value;
    }

    // Stores the cached provider instance and whether initialization has already been attempted for that type.
    // Created inside AudioProviderFactory.GetOrCreate and kept in the AudioProviderFactory cache dictionary for the lifetime
    // of the current UnrealAssetScout process.
    private sealed class ProviderCache<T> where T : class
    {
        public bool InitializationAttempted { get; set; }
        public T? Value { get; set; }
    }
}
