// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Text.Json;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// The fontTools reference data for one font, written by tests/tools/generate_font_program_fixtures.py.
/// </summary>
internal sealed class FontProgramFixture
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Gets or sets the path of the font, relative to the fixture.</summary>
    public string Font { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public bool IsCidKeyed { get; set; }

    /// <summary>Gets or sets the matrix from glyph space to ems as [a b c d e f].</summary>
    public double[] FontMatrix { get; set; } = [];

    /// <summary>Gets or sets the glyph ID for each of the 256 codes, or null when the font has no built-in encoding.</summary>
    public int[]? Encoding { get; set; }

    public FixtureGlyph[] Glyphs { get; set; } = [];

    /// <summary>Gets the full path of the font file.</summary>
    public string FontPath { get; private set; } = string.Empty;

    public static FontProgramFixture Load(string name)
    {
        string path = TestFonts.GetPath("FontPrograms/" + name + ".json");
        FontProgramFixture fixture = JsonSerializer.Deserialize<FontProgramFixture>(File.ReadAllText(path), Options)!;
        fixture.FontPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, fixture.Font));
        return fixture;
    }

    internal sealed class FixtureGlyph
    {
        /// <summary>Gets or sets the glyph name, or null when the glyph has none.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the CID of the glyph in a CID-keyed font.</summary>
        public int? Cid { get; set; }

        /// <summary>
        /// Gets or sets the matrix from glyph space to ems of this glyph, when it differs from the font matrix.
        /// The outline is in the glyph space of this matrix.
        /// </summary>
        public double[]? Matrix { get; set; }

        /// <summary>Gets or sets the advance width in the units of the font matrix, or null when the font has none.</summary>
        public double? Advance { get; set; }

        /// <summary>Gets or sets the outline as path commands: M, L, Q, C and Z.</summary>
        public string Outline { get; set; } = string.Empty;
    }
}
