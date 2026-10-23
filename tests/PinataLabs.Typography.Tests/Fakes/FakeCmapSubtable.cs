// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using PinataLabs.Typography.Tables.General.CMap;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Fakes;

internal class FakeCmapSubtable : CMapSubTable
{
    private readonly List<FakeGlyphSource> glyphs;

    public FakeCmapSubtable(List<FakeGlyphSource> glyphs)
        => this.glyphs = glyphs;

    public override bool TryGetGlyphId(CodePoint codePoint, out ushort glyphId)
    {
        foreach (FakeGlyphSource c in this.glyphs)
        {
            if (c.CodePoint == codePoint)
            {
                glyphId = c.Index;
                return true;
            }
        }

        glyphId = 0;
        return false;
    }

    public override IEnumerable<int> GetAvailableCodePoints()
        => this.glyphs.Select(x => x.CodePoint.Value);
}
