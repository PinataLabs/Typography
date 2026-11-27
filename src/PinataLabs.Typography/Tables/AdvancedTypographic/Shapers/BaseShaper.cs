// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

internal abstract class BaseShaper
{
    public ScriptClass ScriptClass { get; protected set; }

    public MarkZeroingMode MarkZeroingMode { get; protected set; }

    /// <summary>
    /// Gets the tags of the features whose lookups do not apply: those that the caller turned off, and those that the
    /// shaper turns off whatever the caller asks for, without those that the shaper turns on.
    /// </summary>
    public abstract IReadOnlyList<Tag> DisabledFeatureTags { get; }

    /// <summary>
    /// Assigns the features to each glyph within the collection.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the elements to assign.</param>
    /// <param name="count">The number of elements to assign.</param>
    public void Plan(IGlyphShapingCollection collection, int index, int count)
    {
        int collectionCount = collection.Count;

        this.PlanPreprocessingFeatures(collection, index, count);

        RecalculateCount(collection, ref collectionCount, ref count);

        this.PlanFeatures(collection, index, count);

        RecalculateCount(collection, ref collectionCount, ref count);

        this.PlanPostprocessingFeatures(collection, index, count);

        RecalculateCount(collection, ref collectionCount, ref count);

        this.AssignFeatures(collection, index, count);
    }

    /// <summary>
    /// Assigns the features to each glyph within the collection.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the elements to assign.</param>
    /// <param name="count">The number of elements to assign.</param>
    protected abstract void PlanFeatures(IGlyphShapingCollection collection, int index, int count);

    /// <summary>
    /// Assigns the preprocessing features to each glyph within the collection.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the elements to assign.</param>
    /// <param name="count">The number of elements to assign.</param>
    protected abstract void PlanPreprocessingFeatures(IGlyphShapingCollection collection, int index, int count);

    /// <summary>
    /// Assigns the postprocessing features to each glyph within the collection.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the elements to assign.</param>
    /// <param name="count">The number of elements to assign.</param>
    protected abstract void PlanPostprocessingFeatures(IGlyphShapingCollection collection, int index, int count);

    /// <summary>
    /// Assigns the shaper specific substitution features to each glyph within the collection.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the elements to assign.</param>
    /// <param name="count">The number of elements to assign.</param>
    protected abstract void AssignFeatures(IGlyphShapingCollection collection, int index, int count);

    public abstract IReadOnlyList<ShapingStage> GetShapingStages();

    private static void RecalculateCount(IGlyphShapingCollection collection, ref int oldCount, ref int count)
    {
        // If the collection has changed size we need to recalculate the count.
        int delta = collection.Count - oldCount;
        count += delta;
        oldCount += delta;
    }
}
