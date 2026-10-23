// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff
{
    internal readonly struct GlyphNameMap
    {
        public readonly ushort GlyphIndex;

        public readonly string GlyphName;

        public GlyphNameMap(ushort glyphIndex, string glyphName)
        {
            this.GlyphIndex = glyphIndex;
            this.GlyphName = glyphName;
        }
    }
}
