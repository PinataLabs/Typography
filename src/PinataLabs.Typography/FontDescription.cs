// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.IO;
using PinataLabs.Typography.Tables;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tables.General.Post;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.WellKnownIds;

namespace PinataLabs.Typography;

/// <summary>
/// Provides basic descriptive metadata for the font.
/// </summary>
public class FontDescription
{
    private readonly NameTable nameTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontDescription" /> class.
    /// </summary>
    /// <param name="nameTable">The name table.</param>
    /// <param name="os2">The os2 table.</param>
    /// <param name="head">The head table.</param>
    /// <param name="italicAngle">The italic angle of the post table.</param>
    /// <param name="fvar">The fvar table, if the font is a variable font.</param>
    internal FontDescription(NameTable nameTable, OS2Table? os2, HeadTable? head, float italicAngle, FvarTable? fvar)
    {
        this.nameTable = nameTable;
        this.Fvar = fvar;
        this.StyleRange = FontStyleRange.FromTables(os2, head, italicAngle, fvar);

        this.FontNameInvariantCulture = this.FontName(CultureInfo.InvariantCulture);
        this.FontFamilyInvariantCulture = this.FontFamily(CultureInfo.InvariantCulture);
        this.FontSubFamilyNameInvariantCulture = this.FontSubFamilyName(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the styles that the face covers. A static face covers one weight, one width and one slope.
    /// A variable face covers the range of its <c>wght</c> and <c>wdth</c> axes, and the slopes that its <c>ital</c>
    /// and <c>slnt</c> axes reach.
    /// </summary>
    public FontStyleRange StyleRange { get; }

    // The axes and named instances of a variable face, or null for a static face.
    internal FvarTable? Fvar { get; }

    /// <summary>
    /// Gets the name of the font in the invariant culture.
    /// </summary>
    public string FontNameInvariantCulture { get; }

    /// <summary>
    /// Gets the name of the font family in the invariant culture, as <see cref="FontFamily(CultureInfo)"/> gives it.
    /// </summary>
    public string FontFamilyInvariantCulture { get; }

    /// <summary>
    /// Gets the font sub family in the invariant culture.
    /// </summary>
    public string FontSubFamilyNameInvariantCulture { get; }

    /// <summary>
    /// Gets the name of the font.
    /// </summary>
    /// <param name="culture">The culture to load metadata in.</param>
    /// <returns>The font name.</returns>
    public string FontName(CultureInfo culture) => this.nameTable.FontName(culture);

    /// <summary>
    /// Gets the name of the font family. This is the typographic family name, name ID 16, which groups
    /// every face of a family however many weights, widths and slopes it has. A font without name ID 16
    /// gives the legacy family name, name ID 1.
    /// </summary>
    /// <param name="culture">The culture to load metadata in.</param>
    /// <returns>The font family name.</returns>
    public string FontFamily(CultureInfo culture) => this.nameTable.TypographicFamilyName(culture);

    /// <summary>
    /// Gets the legacy name of the font family, name ID 1, which groups up to four faces:
    /// regular, italic, bold and bold italic. For a font that has no name ID 16 it is the same as
    /// <see cref="FontFamily(CultureInfo)"/>.
    /// </summary>
    /// <param name="culture">The culture to load metadata in.</param>
    /// <returns>The legacy font family name.</returns>
    public string LegacyFontFamily(CultureInfo culture) => this.nameTable.FontFamilyName(culture);

    /// <summary>
    /// Gets the font sub family.
    /// </summary>
    /// <param name="culture">The culture to load metadata in.</param>
    /// <returns>The font sub family name.</returns>
    public string FontSubFamilyName(CultureInfo culture) => this.nameTable.FontSubFamilyName(culture);

    /// <summary>
    /// Gets the name matching the given culture and id.
    /// If <see cref="CultureInfo.InvariantCulture"/> is passed this method will return the first name matching the id.
    /// </summary>
    /// <param name="culture">The culture to load metadata in.</param>
    /// <param name="nameId">The name id to match.</param>
    /// <returns>The <see cref="string"/> name.</returns>
    public string GetNameById(CultureInfo culture, KnownNameIds nameId) => this.nameTable.GetNameById(culture, nameId);

    /// <summary>
    /// Reads a <see cref="FontDescription"/> from the specified stream.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>a <see cref="FontDescription"/>.</returns>
    public static FontDescription LoadDescription(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream fs = File.OpenRead(path);
        var reader = new FontReader(fs);
        return LoadDescription(reader);
    }

    /// <summary>
    /// Reads a <see cref="FontDescription"/> from the specified stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>a <see cref="FontDescription"/>.</returns>
    public static FontDescription LoadDescription(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Only read the name tables.
        var reader = new FontReader(stream);

        return LoadDescription(reader);
    }

    /// <summary>
    /// Reads a <see cref="FontDescription" /> from the specified stream.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>
    /// a <see cref="FontDescription" />.
    /// </returns>
    internal static FontDescription LoadDescription(FontReader reader)
    {
        DebugGuard.NotNull(reader, nameof(reader));

        // NOTE: These fields are read in their optimized order
        // https://docs.microsoft.com/en-gb/typography/opentype/spec/recom#optimized-table-ordering
        HeadTable? head = reader.TryGetTable<HeadTable>();
        OS2Table? os2 = reader.TryGetTable<OS2Table>();
        NameTable nameTable = reader.GetTable<NameTable>();
        float italicAngle = PostTable.ReadItalicAngle(reader);
        FvarTable? fvar = reader.TryGetTable<FvarTable>();

        return new FontDescription(nameTable, os2, head, italicAngle, fvar);
    }

    /// <summary>
    /// Reads all the <see cref="FontDescription"/>s from the file at the specified path (typically a .ttc file like simsun.ttc).
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>a <see cref="FontDescription"/>.</returns>
    public static FontDescription[] LoadFontCollectionDescriptions(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream fs = File.OpenRead(path);
        return LoadFontCollectionDescriptions(fs);
    }

    /// <summary>
    /// Reads all the <see cref="FontDescription"/>s from the specified stream (typically a .ttc file like simsun.ttc).
    /// </summary>
    /// <param name="stream">The stream to read the font collection from.</param>
    /// <returns>a <see cref="FontDescription"/>.</returns>
    public static FontDescription[] LoadFontCollectionDescriptions(Stream stream)
    {
        long startPos = stream.Position;
        var reader = new BigEndianBinaryReader(stream, true);
        var ttcHeader = TtcHeader.Read(reader);

        var result = new FontDescription[(int)ttcHeader.NumFonts];
        for (int i = 0; i < ttcHeader.NumFonts; ++i)
        {
            stream.Position = startPos + ttcHeader.OffsetTable[i];
            var fontReader = new FontReader(stream);
            result[i] = LoadDescription(fontReader);
        }

        return result;
    }
}
