// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Says when <see cref="GlyphNormalizer"/> decomposes a character that the font has a glyph for.
/// </summary>
internal enum NormalizationMode
{
    /// <summary>
    /// Keeps every character that the font has, and composes nothing. The shaper composes its own script.
    /// </summary>
    None,

    /// <summary>
    /// Keeps a character that the font has, unless a mark follows it. A character with marks is decomposed,
    /// so that its marks can be sorted and composed again.
    /// </summary>
    ComposedDiacritics,

    /// <summary>
    /// Decomposes every character that has parts in the font, then composes again.
    /// </summary>
    ComposedDiacriticsNoShortCircuit,
}

/// <summary>
/// Says how a shaper changes the order of a run of marks after <see cref="GlyphNormalizer"/> has sorted it.
/// </summary>
internal enum MarkReordering
{
    None,

    /// <summary>
    /// The Arabic modifier marks move to the start of the run.
    /// </summary>
    ArabicModifierMarks,

    /// <summary>
    /// After a patah or qamats and a sheva or hiriq, a meteg or a mark below the base moves before the sheva or hiriq.
    /// </summary>
    HebrewMeteg,
}

/// <summary>
/// How one shaper wants its text normalized before GSUB. The values match what HarfBuzz does for the scripts of
/// each shaper, as observed in tests/tools/generate_normalization_reference.py.
/// </summary>
internal sealed class NormalizationProfile
{
    private readonly uint[] keptWhole;

    private readonly uint[] composedDespiteExclusion;

    private readonly (uint First, uint Last) presentationForms;

    private readonly (uint First, uint Second, uint Composed)[] presentationPairs;

    private NormalizationProfile(
        NormalizationMode mode,
        bool composesOntoMarks = true,
        MarkReordering markReordering = MarkReordering.None,
        uint[]? keptWhole = null,
        uint[]? composedDespiteExclusion = null,
        (uint First, uint Last) presentationForms = default,
        (uint First, uint Second, uint Composed)[]? presentationPairs = null)
    {
        this.Mode = mode;
        this.ComposesOntoMarks = composesOntoMarks;
        this.MarkReordering = markReordering;
        this.keptWhole = keptWhole ?? [];
        this.composedDespiteExclusion = composedDespiteExclusion ?? [];
        this.presentationForms = presentationForms;
        this.presentationPairs = presentationPairs ?? [];
    }

    public static NormalizationProfile Default { get; } = new(NormalizationMode.ComposedDiacritics);

    public static NormalizationProfile Arabic { get; } = new(NormalizationMode.ComposedDiacritics, markReordering: MarkReordering.ArabicModifierMarks);

    // The marks sort with the shin dot before the dagesh, so shin with both composes through the form with the dot.
    public static NormalizationProfile Hebrew { get; } = new(
        NormalizationMode.ComposedDiacritics,
        markReordering: MarkReordering.HebrewMeteg,
        presentationForms: (0xFB1D, 0xFB4E),
        presentationPairs: [(0xFB2A, 0x05BC, 0xFB2C), (0xFB2B, 0x05BC, 0xFB2D)]);

    public static NormalizationProfile Hangul { get; } = new(NormalizationMode.None);

    // Devanagari RRA, Bengali RRA and RHA, and Tamil AU stay whole. Bengali YYA composes although Unicode excludes it.
    public static NormalizationProfile Indic { get; } = new(
        NormalizationMode.ComposedDiacriticsNoShortCircuit,
        composesOntoMarks: false,
        keptWhole: [0x0931, 0x09DC, 0x09DD, 0x0B94],
        composedDespiteExclusion: [0x09DF]);

    public static NormalizationProfile Myanmar { get; } = new(NormalizationMode.ComposedDiacriticsNoShortCircuit);

    public static NormalizationProfile Universal { get; } = new(NormalizationMode.ComposedDiacriticsNoShortCircuit, composesOntoMarks: false);

    public NormalizationMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether a mark can be the first part of a composition.
    /// When it cannot, the two halves of a split vowel sign stay apart.
    /// </summary>
    public bool ComposesOntoMarks { get; }

    public MarkReordering MarkReordering { get; }

    /// <summary>
    /// Gets a value indicating whether the shaper composes presentation forms that Unicode excludes from composition,
    /// when the font does not attach marks with GPOS and so cannot place the mark on the letter.
    /// </summary>
    public bool ComposesPresentationForms => this.presentationForms.Last != 0;

    /// <summary>
    /// Gets the canonical decomposition of the character, one step deep, unless this shaper keeps it whole.
    /// </summary>
    /// <param name="codePoint">The character.</param>
    /// <param name="first">The first part.</param>
    /// <param name="second">The second part, or 0 when the decomposition is a single character.</param>
    /// <returns><see langword="true"/> if the character decomposes.</returns>
    public bool TryDecompose(uint codePoint, out uint first, out uint second)
    {
        if (Array.IndexOf(this.keptWhole, codePoint) >= 0)
        {
            first = 0;
            second = 0;
            return false;
        }

        return CanonicalMapping.TryDecompose(codePoint, out first, out second);
    }

    /// <summary>
    /// Gets the character that this shaper composes the two parts to.
    /// </summary>
    /// <param name="first">The first part.</param>
    /// <param name="second">The second part.</param>
    /// <param name="withPresentationForms">Whether the presentation forms of the shaper compose.</param>
    /// <param name="composed">The composed character.</param>
    /// <returns><see langword="true"/> if the parts compose.</returns>
    public bool TryCompose(uint first, uint second, bool withPresentationForms, out uint composed)
    {
        if (!CanonicalMapping.TryCompose(first, second, out composed, out bool excluded))
        {
            if (withPresentationForms)
            {
                foreach ((uint First, uint Second, uint Composed) pair in this.presentationPairs)
                {
                    if (pair.First == first && pair.Second == second)
                    {
                        composed = pair.Composed;
                        return true;
                    }
                }
            }

            return false;
        }

        if (excluded)
        {
            return Array.IndexOf(this.composedDespiteExclusion, composed) >= 0
                || (withPresentationForms && composed >= this.presentationForms.First && composed <= this.presentationForms.Last);
        }

        return this.ComposesOntoMarks || !CodePoint.IsMark(new CodePoint(first));
    }
}
