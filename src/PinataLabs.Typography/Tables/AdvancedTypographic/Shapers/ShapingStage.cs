// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// An individual shaping stage.
/// A stage holds the features whose lookups are applied together, in LookupList order.
/// It can also contain pre and post processing operations, which run before and after those lookups.
/// </summary>
internal sealed class ShapingStage
{
    private readonly List<PlannedFeature> features = [];
    private readonly Action<IGlyphShapingCollection, int, int>? preAction;
    private Action<IGlyphShapingCollection, int, int>? postAction;

    public ShapingStage(Action<IGlyphShapingCollection, int, int>? preAction)
        => this.preAction = preAction;

    /// <summary>
    /// Gets the features of the stage, in the order in which the shaper planned them.
    /// </summary>
    public IReadOnlyList<PlannedFeature> Features => this.features;

    public void AddFeature(PlannedFeature feature) => this.features.Add(feature);

    public void SetPostAction(Action<IGlyphShapingCollection, int, int> action) => this.postAction = action;

    public void PreProcessFeatures(IGlyphShapingCollection collection, int index, int count)
        => this.preAction?.Invoke(collection, index, count);

    public void PostProcessFeatures(IGlyphShapingCollection collection, int index, int count)
        => this.postAction?.Invoke(collection, index, count);
}
