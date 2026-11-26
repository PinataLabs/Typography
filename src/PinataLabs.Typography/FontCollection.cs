// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PinataLabs.Typography.Tables;

namespace PinataLabs.Typography;

/// <summary>
/// Represents a collection of font families.
/// </summary>
public sealed class FontCollection : IFontCollection, IFontMetricsCollection
{
    private static readonly StringComparer GroupComparer = StringComparerHelpers.GetCaseInsensitiveStringComparer(CultureInfo.InvariantCulture);

    private readonly HashSet<string> searchDirectories = [];
    private readonly HashSet<FontMetrics> metricsCollection = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="FontCollection"/> class.
    /// </summary>
    public FontCollection()
        : this([])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FontCollection"/> class.
    /// </summary>
    /// <param name="searchDirectories">The collection of directories used to search for font families.</param>
    /// <remarks>
    /// Use this constructor instead of the parameterless constructor if the fonts added to that collection
    /// are actually added after searching inside physical file system directories. The message of the
    /// <see cref="FontFamilyNotFoundException"/> will include the searched directories.
    /// </remarks>
    internal FontCollection(IReadOnlyCollection<string> searchDirectories)
    {
        ArgumentNullException.ThrowIfNull(searchDirectories);
        foreach (string? dir in searchDirectories)
        {
            this.searchDirectories.Add(dir);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<FontFamily> Families => this.FamiliesByCultureImpl(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public FontFamily Add(string path)
        => this.Add(path, out _);

    /// <inheritdoc/>
    public FontFamily Add(string path, out FontDescription description)
        => this.AddImpl(path, CultureInfo.InvariantCulture, out description);

    /// <inheritdoc/>
    public FontFamily Add(Stream stream)
        => this.Add(stream, out _);

    /// <inheritdoc/>
    public FontFamily Add(Stream stream, out FontDescription description)
        => this.AddImpl(stream, CultureInfo.InvariantCulture, out description);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(string path)
        => this.AddCollection(path, out _);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(string path, out IEnumerable<FontDescription> descriptions)
        => this.AddCollectionImpl(path, CultureInfo.InvariantCulture, out descriptions);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(Stream stream)
        => this.AddCollection(stream, out _);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(Stream stream, out IEnumerable<FontDescription> descriptions)
        => this.AddCollectionImpl(stream, CultureInfo.InvariantCulture, out descriptions);

    /// <inheritdoc/>
    public FontFamily Get(string name)
        => this.GetImpl(name, CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public bool TryGet(string name, out FontFamily family)
        => this.TryGetImpl(name, CultureInfo.InvariantCulture, out family);

    /// <inheritdoc/>
    public FontFamily Add(string path, CultureInfo culture)
        => this.AddImpl(path, culture, out _);

    /// <inheritdoc/>
    public FontFamily Add(string path, CultureInfo culture, out FontDescription description)
        => this.AddImpl(path, culture, out description);

    /// <inheritdoc/>
    public FontFamily Add(Stream stream, CultureInfo culture)
        => this.AddImpl(stream, culture, out _);

    /// <inheritdoc/>
    public FontFamily Add(Stream stream, CultureInfo culture, out FontDescription description)
        => this.AddImpl(stream, culture, out description);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(string path, CultureInfo culture)
        => this.AddCollection(path, culture, out _);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(
        string path,
        CultureInfo culture,
        out IEnumerable<FontDescription> descriptions)
        => this.AddCollectionImpl(path, culture, out descriptions);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(Stream stream, CultureInfo culture)
        => this.AddCollection(stream, culture, out _);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> AddCollection(
        Stream stream,
        CultureInfo culture,
        out IEnumerable<FontDescription> descriptions)
        => this.AddCollectionImpl(stream, culture, out descriptions);

    /// <inheritdoc/>
    public IEnumerable<FontFamily> GetByCulture(CultureInfo culture)
        => this.FamiliesByCultureImpl(culture);

    /// <inheritdoc/>
    public FontFamily Get(string name, CultureInfo culture)
        => this.GetImpl(name, culture);

    /// <inheritdoc/>
    public bool TryGet(string name, CultureInfo culture, out FontFamily family)
        => this.TryGetImpl(name, culture, out family);

    /// <inheritdoc/>
    FontFamily IFontMetricsCollection.AddMetrics(FontMetrics metrics, CultureInfo culture)
    {
        ((IFontMetricsCollection)this).AddMetrics(metrics);
        return new FontFamily(this.GroupName(metrics, culture), this, culture);
    }

    /// <inheritdoc/>
    void IFontMetricsCollection.AddMetrics(FontMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.Description is null)
        {
            throw new ArgumentException($"{nameof(FontMetrics)} must have a Description.", nameof(metrics));
        }

        lock (this.metricsCollection)
        {
            this.metricsCollection.Add(metrics);
        }
    }

    /// <inheritdoc/>
    IReadOnlyList<FontMetrics> IReadOnlyFontMetricsCollection.GetAllMetrics(string name, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(name);
        StringComparer comparer = StringComparerHelpers.GetCaseInsensitiveStringComparer(culture);

        // A face is named in the culture by the language of its names, and faces of one family can have names
        // in different languages. So the name finds a face, and the family of that face is every face with its group.
        FontMetrics[] all = this.Snapshot();
        HashSet<string> groups = new(GroupComparer);
        foreach (FontMetrics metrics in all)
        {
            if (comparer.Equals(metrics.Description.FontFamily(culture), name))
            {
                groups.Add(metrics.Description.FontFamilyInvariantCulture);
            }
        }

        return all
            .Where(x => groups.Contains(x.Description.FontFamilyInvariantCulture))
            .ToArray();
    }

    /// <inheritdoc/>
    IEnumerator<FontMetrics> IReadOnlyFontMetricsCollection.GetEnumerator()
        => ((IEnumerable<FontMetrics>)this.Snapshot()).GetEnumerator();

    internal void AddSearchDirectories(IEnumerable<string> directories)
    {
        foreach (string? directory in directories)
        {
            this.searchDirectories.Add(directory);
        }
    }

    // A reader gets the faces as they were at one moment. AddMetrics changes the set under the same lock.
    private FontMetrics[] Snapshot()
    {
        lock (this.metricsCollection)
        {
            return [.. this.metricsCollection];
        }
    }

    private FontFamily AddImpl(string path, CultureInfo culture, out FontDescription description)
    {
        var instance = new FileFontMetrics(path);
        description = instance.Description;
        return ((IFontMetricsCollection)this).AddMetrics(instance, culture);
    }

    private FontFamily AddImpl(Stream stream, CultureInfo culture, out FontDescription description)
    {
        var metrics = StreamFontMetrics.LoadFont(stream);
        description = metrics.Description;

        return ((IFontMetricsCollection)this).AddMetrics(metrics, culture);
    }

    private IEnumerable<FontFamily> AddCollectionImpl(
        string path,
        CultureInfo culture,
        out IEnumerable<FontDescription> descriptions)
    {
        FileFontMetrics[] fonts = FileFontMetrics.LoadFontCollection(path);

        var description = new FontDescription[fonts.Length];
        var families = new HashSet<FontFamily>();
        for (int i = 0; i < fonts.Length; i++)
        {
            description[i] = fonts[i].Description;
            FontFamily family = ((IFontMetricsCollection)this).AddMetrics(fonts[i], culture);
            families.Add(family);
        }

        descriptions = description;
        return families;
    }

    private IEnumerable<FontFamily> AddCollectionImpl(
        Stream stream,
        CultureInfo culture,
        out IEnumerable<FontDescription> descriptions)
    {
        long startPos = stream.Position;
        var reader = new BigEndianBinaryReader(stream, true);
        var ttcHeader = TtcHeader.Read(reader);
        var result = new List<FontDescription>((int)ttcHeader.NumFonts);
        var installedFamilies = new HashSet<FontFamily>();
        for (int i = 0; i < ttcHeader.NumFonts; ++i)
        {
            stream.Position = startPos + ttcHeader.OffsetTable[i];
            var instance = StreamFontMetrics.LoadFont(stream);
            installedFamilies.Add(((IFontMetricsCollection)this).AddMetrics(instance, culture));
            FontDescription fontDescription = instance.Description;
            result.Add(fontDescription);
        }

        descriptions = result;
        return installedFamilies;
    }

    // Faces are grouped by the invariant family name. The name in a culture depends on which languages
    // the names of a face have, so grouping by it would split a family whose faces have different languages.
    private IEnumerable<FontFamily> FamiliesByCultureImpl(CultureInfo culture)
    {
        HashSet<string> groups = new(GroupComparer);
        List<FontFamily> families = [];
        foreach (FontMetrics metrics in this.Snapshot())
        {
            if (groups.Add(metrics.Description.FontFamilyInvariantCulture))
            {
                families.Add(new FontFamily(metrics.Description.FontFamily(culture), this, culture));
            }
        }

        return families;
    }

    // The name of a family in a culture is the name of the first face of its group, whichever face a name found.
    private string GroupName(FontMetrics metrics, CultureInfo culture)
    {
        string group = metrics.Description.FontFamilyInvariantCulture;
        FontMetrics first = this.Snapshot()
            .First(x => GroupComparer.Equals(x.Description.FontFamilyInvariantCulture, group));
        return first.Description.FontFamily(culture);
    }

    private bool TryGetImpl(string name, CultureInfo culture, out FontFamily family)
    {
        ArgumentNullException.ThrowIfNull(name);
        StringComparer comparer = StringComparerHelpers.GetCaseInsensitiveStringComparer(culture);

        FontMetrics? match = this.Snapshot()
            .FirstOrDefault(x => comparer.Equals(name, x.Description.FontFamily(culture)));

        if (match != null)
        {
            family = new FontFamily(this.GroupName(match, culture), this, culture);
            return true;
        }

        family = default;
        return false;
    }

    private FontFamily GetImpl(string name, CultureInfo culture)
    {
        if (this.TryGetImpl(name, culture, out FontFamily family))
        {
            return family;
        }

        throw new FontFamilyNotFoundException(name, this.searchDirectories);
    }
}
