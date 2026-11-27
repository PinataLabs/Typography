// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// A feature that a shaper plans in a shaping stage, with the joiners that its lookups do not skip.
/// </summary>
/// <param name="Tag">The tag of the feature.</param>
/// <param name="ManualJoiners">The joiners that the lookups of the feature do not skip.</param>
/// <param name="PerSyllable">
/// Whether the GSUB lookups of the feature match only glyphs of the syllable of the glyph that they apply to. This is the
/// <c>F_PER_SYLLABLE</c> flag of HarfBuzz.
/// </param>
internal readonly record struct PlannedFeature(Tag Tag, ManualJoiners ManualJoiners = ManualJoiners.None, bool PerSyllable = false);
