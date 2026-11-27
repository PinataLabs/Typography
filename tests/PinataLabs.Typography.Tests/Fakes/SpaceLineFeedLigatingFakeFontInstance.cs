// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tests.Fakes;

/// <summary>
/// A fake font that ligates a space and the line feed after it into the glyph of the space, so the line feed has no
/// glyph of its own.
/// </summary>
internal sealed class SpaceLineFeedLigatingFakeFontInstance : FakeFontInstance
{
    public SpaceLineFeedLigatingFakeFontInstance(string text)
        : base(text)
    {
    }

    internal override void ApplySubstitution(GlyphSubstitutionCollection collection)
    {
        for (int i = 0; i < collection.Count - 1; i++)
        {
            if (collection[i].CodePoint.Value == ' ' && collection[i + 1].CodePoint.Value == '\n')
            {
                collection.Replace(i, 1, collection[i].GlyphId);
            }
        }
    }
}
