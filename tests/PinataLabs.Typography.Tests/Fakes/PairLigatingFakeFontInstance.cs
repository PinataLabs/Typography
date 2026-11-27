// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tests.Fakes;

/// <summary>
/// A fake font that ligates a character and the one after it into the glyph of the first, so the second has no glyph
/// of its own.
/// </summary>
internal sealed class PairLigatingFakeFontInstance : FakeFontInstance
{
    private readonly char first;
    private readonly char second;

    public PairLigatingFakeFontInstance(string text, char first, char second)
        : base(text)
    {
        this.first = first;
        this.second = second;
    }

    internal override void ApplySubstitution(GlyphSubstitutionCollection collection)
    {
        for (int i = 0; i < collection.Count - 1; i++)
        {
            if (collection[i].CodePoint.Value == this.first && collection[i + 1].CodePoint.Value == this.second)
            {
                collection.Replace(i, 1, collection[i].GlyphId);
            }
        }
    }
}
