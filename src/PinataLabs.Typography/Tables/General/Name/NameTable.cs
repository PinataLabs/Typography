// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PinataLabs.Typography.Utilities;
using PinataLabs.Typography.WellKnownIds;

namespace PinataLabs.Typography.Tables.General.Name;

internal class NameTable : Table
{
    internal const string TableName = "name";
    private readonly NameRecord[] names;

    internal NameTable(NameRecord[] names, string[] languages)
        => this.names = names;

    /// <summary>
    /// Gets the name of the font.
    /// </summary>
    /// <value>
    /// The name of the font.
    /// </value>
    public string Id(CultureInfo culture)
        => this.GetNameById(culture, KnownNameIds.UniqueFontID);

    /// <summary>
    /// Gets the name of the font.
    /// </summary>
    /// <value>
    /// The name of the font.
    /// </value>
    public string FontName(CultureInfo culture)
        => this.GetNameById(culture, KnownNameIds.FullFontName);

    /// <summary>
    /// Gets the name of the font.
    /// </summary>
    /// <value>
    /// The name of the font.
    /// </value>
    public string FontFamilyName(CultureInfo culture)
        => this.GetNameById(culture, KnownNameIds.FontFamilyName);

    /// <summary>
    /// Gets the typographic family name, name ID 16, or the family name of name ID 1 when the font has no ID 16.
    /// </summary>
    /// <param name="culture">The culture to load the name in.</param>
    /// <returns>The typographic family name.</returns>
    /// <remarks>
    /// A name in the language of <paramref name="culture"/> beats a name in another language, and in one language
    /// ID 16 beats ID 1. So a font that has ID 16 only in English and ID 1 in Japanese gives the Japanese name for ja-JP.
    /// Without a name in that language, the order is the same as <see cref="GetNameById(CultureInfo, KnownNameIds)"/>:
    /// US English, the first Windows name, then the first name, with ID 16 before ID 1.
    /// </remarks>
    public string TypographicFamilyName(CultureInfo culture)
    {
        int languageId = culture.LCID;
        return NonEmpty(this.FindInLanguage(KnownNameIds.TypographicFamilyName, languageId))
            ?? NonEmpty(this.FindInLanguage(KnownNameIds.FontFamilyName, languageId))
            ?? NonEmpty(this.FindInAnyLanguage(KnownNameIds.TypographicFamilyName))
            ?? NonEmpty(this.FindInAnyLanguage(KnownNameIds.FontFamilyName))
            ?? string.Empty;
    }

    /// <summary>
    /// Gets the name of the font.
    /// </summary>
    /// <value>
    /// The name of the font.
    /// </value>
    public string FontSubFamilyName(CultureInfo culture)
        => this.GetNameById(culture, KnownNameIds.FontSubfamilyName);

    public string GetNameById(CultureInfo culture, KnownNameIds nameId)
        => (this.FindInLanguage(nameId, culture.LCID) ?? this.FindInAnyLanguage(nameId))?.Value ?? string.Empty;

    public string GetNameById(CultureInfo culture, ushort nameId)
        => this.GetNameById(culture, (KnownNameIds)nameId);

    private static string? NonEmpty(NameRecord? record)
    {
        string? value = record?.Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    // The first Windows record of the name in a language.
    private NameRecord? FindInLanguage(KnownNameIds nameId, int languageId)
    {
        foreach (NameRecord name in this.names)
        {
            if (name.NameID == nameId && name.Platform == PlatformIDs.Windows && name.LanguageID == languageId)
            {
                return name;
            }
        }

        return null;
    }

    // The record of the name when the language has none: US English, else the first Windows record, else the first.
    private NameRecord? FindInAnyLanguage(KnownNameIds nameId)
    {
        NameRecord? usaVersion = null;
        NameRecord? firstWindows = null;
        NameRecord? first = null;
        foreach (NameRecord name in this.names)
        {
            if (name.NameID != nameId)
            {
                continue;
            }

            first ??= name;
            if (name.Platform == PlatformIDs.Windows)
            {
                firstWindows ??= name;
                if (name.LanguageID == 0x0409)
                {
                    usaVersion ??= name;
                }
            }
        }

        return usaVersion ?? firstWindows ?? first;
    }

    public static NameTable? Load(FontReader fontReader)
    {
        if (!fontReader.TryGetReaderAtTablePosition(TableName, out BigEndianBinaryReader? binaryReader))
        {
            throw new InvalidFontTableException($"Table '{TableName}' is missing", TableName);
        }

        using (binaryReader)
        {
            // Move to start of table.
            return Load(binaryReader);
        }
    }

    public static NameTable Load(BigEndianBinaryReader reader)
    {
        var strings = new List<StringLoader>();
        ushort format = reader.ReadUInt16();
        ushort nameCount = reader.ReadUInt16();
        ushort stringOffset = reader.ReadUInt16();

        var names = new NameRecord[nameCount];

        for (int i = 0; i < nameCount; i++)
        {
            names[i] = NameRecord.Read(reader);
            StringLoader? sr = names[i].StringReader;
            if (sr is not null)
            {
                strings.Add(sr);
            }
        }

        StringLoader[]? langs = [];
        if (format == 1)
        {
            // Format 1 adds language data.
            ushort langCount = reader.ReadUInt16();
            langs = new StringLoader[langCount];

            for (int i = 0; i < langCount; i++)
            {
                langs[i] = StringLoader.Create(reader);
                strings.Add(langs[i]);
            }
        }

        foreach (StringLoader readable in strings)
        {
            int readableStartOffset = stringOffset + readable.Offset;

            reader.Seek(readableStartOffset, SeekOrigin.Begin);

            readable.LoadValue(reader);
        }

        string[] langNames = langs?.Select(x => x.Value).ToArray() ?? [];

        return new NameTable(names, langNames);
    }
}
