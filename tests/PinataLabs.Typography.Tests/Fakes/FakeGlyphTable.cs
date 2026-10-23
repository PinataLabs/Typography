// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using PinataLabs.Typography.Tables.TrueType.Glyphs;

namespace PinataLabs.Typography.Tests.Fakes
{
    internal class FakeGlyphTable : GlyphTable
    {
        private readonly List<FakeGlyphSource> glyphs;

        public FakeGlyphTable(List<FakeGlyphSource> glyphs)
            : base(new GlyphLoader[glyphs.Count])
            => this.glyphs = glyphs;

        internal override GlyphVector GetGlyph(int index)
        {
            foreach (FakeGlyphSource c in this.glyphs)
            {
                if (c.Index == index)
                {
                    return c.Vector;
                }
            }

            return default;
        }
    }
}
