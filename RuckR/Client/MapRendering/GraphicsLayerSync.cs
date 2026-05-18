using dymaptic.GeoBlazor.Core.Components;
using dymaptic.GeoBlazor.Core.Components.Layers;

namespace RuckR.Client.MapRendering;

/// <summary>
/// Small helpers for replacing GeoBlazor graphics in app-owned graphics layers.
/// </summary>
public static class GraphicsLayerSync
{
    /// <summary>Replace a layer with keyed graphics and mirror the keys into an index.</summary>
    /// <typeparam name="TKey">Graphic lookup key type.</typeparam>
    /// <param name="layer">Layer to replace.</param>
    /// <param name="index">Index to clear and repopulate with the added graphics.</param>
    /// <param name="graphics">Graphics keyed by app entity id.</param>
    /// <returns>A task that completes after the layer and index are updated.</returns>
    public static async Task ReplaceManyAsync<TKey>(
        GraphicsLayer layer,
        IDictionary<TKey, Graphic> index,
        IReadOnlyList<KeyValuePair<TKey, Graphic>> graphics)
        where TKey : notnull
    {
        await layer.Clear();
        index.Clear();

        if (graphics.Count == 0)
        {
            return;
        }

        await layer.AddMany(graphics.Select(item => item.Value).ToArray());
        foreach (var (key, graphic) in graphics)
        {
            index[key] = graphic;
        }
    }

    /// <summary>Replace a layer with a single graphic.</summary>
    /// <param name="layer">Layer to replace.</param>
    /// <param name="graphic">Graphic to add.</param>
    /// <returns>A task that completes after the layer is updated.</returns>
    public static async Task ReplaceOneAsync(GraphicsLayer layer, Graphic graphic)
    {
        await layer.Clear();
        await layer.Add(graphic);
    }

    /// <summary>Replace several layers with keyed graphics grouped by an app value.</summary>
    /// <typeparam name="TKey">Graphic lookup key type.</typeparam>
    /// <typeparam name="TGroup">Layer grouping value type.</typeparam>
    /// <param name="layers">Layers keyed by group.</param>
    /// <param name="index">Index to clear and repopulate with all added graphics.</param>
    /// <param name="graphics">Graphics keyed by app entity id.</param>
    /// <param name="groupSelector">Returns the target layer group for each keyed graphic.</param>
    /// <returns>A task that completes after all layers and the index are updated.</returns>
    public static async Task ReplaceGroupedAsync<TKey, TGroup>(
        IReadOnlyDictionary<TGroup, GraphicsLayer> layers,
        IDictionary<TKey, Graphic> index,
        IReadOnlyList<KeyValuePair<TKey, Graphic>> graphics,
        Func<KeyValuePair<TKey, Graphic>, TGroup> groupSelector)
        where TKey : notnull
        where TGroup : notnull
    {
        foreach (var layer in layers.Values)
        {
            await layer.Clear();
        }

        index.Clear();

        foreach (var group in graphics.GroupBy(groupSelector))
        {
            if (!layers.TryGetValue(group.Key, out var layer))
            {
                continue;
            }

            var groupGraphics = group.Select(item => item.Value).ToArray();
            if (groupGraphics.Length > 0)
            {
                await layer.AddMany(groupGraphics);
            }
        }

        foreach (var (key, graphic) in graphics)
        {
            index[key] = graphic;
        }
    }
}
