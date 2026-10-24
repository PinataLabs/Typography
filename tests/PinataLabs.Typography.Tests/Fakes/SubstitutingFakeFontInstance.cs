// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.TrueType;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Fakes;

/// <summary>
/// A fake font that applies an arbitrary single-glyph substitution (standing in for a <c>ccmp</c> or <c>locl</c>
/// lookup) and has a vertical advance that differs from its horizontal advance.
/// </summary>
internal sealed class SubstitutingFakeFontInstance : FakeFontInstance
{
    public const ushort VerticalAdvance = 50;

    private readonly CodePoint target;
    private readonly CodePoint replacement;

    public SubstitutingFakeFontInstance(string text, CodePoint target, CodePoint replacement)
        : base(CreateTables(text))
    {
        this.target = target;
        this.replacement = replacement;
    }

    internal override void ApplySubstitution(GlyphSubstitutionCollection collection)
    {
        this.TryGetGlyphId(this.replacement, out ushort replacementId);
        for (int i = 0; i < collection.Count; i++)
        {
            if (collection[i].CodePoint == this.target)
            {
                collection.Replace(i, replacementId);
            }
        }
    }

    private static TrueTypeFontTables CreateTables(string text)
    {
        TrueTypeFontTables tables = CreateTrueTypeFontTables(text);
        HashSet<int> distinct = [];
        foreach (CodePoint codePoint in text.AsSpan().EnumerateCodePoints())
        {
            distinct.Add(codePoint.Value);
        }

        // Glyph 0 is .notdef.
        int glyphCount = distinct.Count + 1;
        tables.Vmtx = new VerticalMetricsTable(
            Enumerable.Repeat(VerticalAdvance, glyphCount).ToArray(),
            Enumerable.Repeat((short)10, glyphCount).ToArray());
        return tables;
    }
}
