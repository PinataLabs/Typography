// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff;

internal class CffFont
{
    public CffFont(string name, CffTopDictionary metrics, CffGlyphData[] glyphs)
    {
        this.FontName = name;
        this.Metrics = metrics;
        this.Glyphs = glyphs;
    }

    public string FontName { get; set; }

    public CffTopDictionary Metrics { get; set; }

    public CffGlyphData[] Glyphs { get; }
}
