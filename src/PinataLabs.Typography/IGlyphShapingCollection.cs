// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography;

/// <summary>
/// Defines the contract for glyph shaping collections.
/// </summary>
internal interface IGlyphShapingCollection
{
    /// <summary>
    /// Gets the collection count.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the text options used by this collection.
    /// </summary>
    TextOptions TextOptions { get; }

    /// <summary>
    /// Gets the index of the first glyph of the script run that the GSUB or GPOS table shapes. HarfBuzz shapes each
    /// script run in a buffer of its own, so a lookup matches, skips to and attaches only glyphs of the run. Outside a
    /// run it is 0.
    /// </summary>
    int RunStart { get; }

    /// <summary>
    /// Gets the index after the last glyph of the script run. A substitution that adds or removes glyphs of the run
    /// moves it. Outside a run it is <see cref="Count"/>.
    /// </summary>
    int RunEnd { get; }

    /// <summary>
    /// Gets the glyph shaping data at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the elements to get.</param>
    /// <returns>The <see cref="GlyphShapingData"/>.</returns>
    GlyphShapingData this[int index] { get; }

    /// <summary>
    /// Adds the shaping feature to the collection which should be applied to the glyph at a specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <param name="feature">The feature to apply.</param>
    void AddShapingFeature(int index, TagEntry feature);

    /// <summary>
    /// Enables a previously added shaping feature.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <param name="feature">The feature to enable.</param>
    void EnableShapingFeature(int index, Tag feature);

    /// <summary>
    /// Disables a previously added shaping feature.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <param name="feature">The feature to disable.</param>
    void DisableShapingFeature(int index, Tag feature);
}
