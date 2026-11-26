// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PinataLabs.Typography;

/// <summary>
/// Defines a group of type faces having a similar basic design and certain
/// variations in styles.
/// </summary>
public struct FontFamily : IEquatable<FontFamily>
{
    private readonly IReadOnlyFontMetricsCollection collection;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontFamily"/> struct.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="culture">The culture the family was extracted against</param>
    internal FontFamily(string name, IReadOnlyFontMetricsCollection collection, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(collection);

        this.collection = collection;
        this.Name = name;
        this.Culture = culture;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the culture this instance was extracted against.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets the faces of the family, in the order in which they were added to the collection.
    /// The list is a snapshot, and a face that is added to the collection later is not in it.
    /// </summary>
    /// <exception cref="FontException">This is the default <see cref="FontFamily"/>.</exception>
    public IReadOnlyList<FontMetrics> Faces
    {
        get
        {
            if (this == default)
            {
                FontsThrowHelper.ThrowDefaultInstance();
            }

            return this.collection.GetAllMetrics(this.Name, this.Culture);
        }
    }

    /// <summary>
    /// Compares two <see cref="FontFamily"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="FontFamily"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="FontFamily"/> on the right side of the operand.</param>
    /// <returns>
    /// <see langword="true"/> if the current left is equal to the <paramref name="right"/>
    /// parameter; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator ==(FontFamily left, FontFamily right)
        => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="FontFamily"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="FontFamily"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="FontFamily"/> on the right side of the operand.</param>
    /// <returns>
    /// <see langword="true"/> if the current left is unequal to the <paramref name="right"/>
    /// parameter; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator !=(FontFamily left, FontFamily right)
        => !(left == right);

    /// <summary>
    /// Create a new instance of the <see cref="Font" /> for the named font family.
    /// </summary>
    /// <param name="size">The size of the font in PT units.</param>
    /// <param name="style">The font style. The default value is <see cref="FontStyle.Regular"/>.</param>
    /// <returns>The new <see cref="Font" />.</returns>
    public Font CreateFont(float size, FontStyle style = default)
    {
        if (this == default)
        {
            FontsThrowHelper.ThrowDefaultInstance();
        }

        return new Font(this, size, style);
    }

    /// <summary>
    /// Gets the collection of filesystem paths to the font family sources.
    /// </summary>
    /// <param name="paths">
    /// When this method returns, contains the filesystem paths to the font family sources,
    /// if the path exists; otherwise, an empty value for the type of the paths parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if the <see cref="FontFamily" /> was created via filesystem paths; otherwise, <see langword="false" />.
    /// </returns>
    public bool TryGetPaths(out IEnumerable<string> paths)
    {
        if (this == default)
        {
            FontsThrowHelper.ThrowDefaultInstance();
        }

        var filePaths = new List<string>();
        foreach (FontMetrics face in this.Faces)
        {
            if (face is FileFontMetrics fileMetrics)
            {
                filePaths.Add(fileMetrics.Path);
            }
        }

        paths = filePaths;
        return filePaths.Count > 0;
    }

    /// <summary>
    /// Gets the face of the family that best matches a style, as CSS Fonts Level 4 section 5.2
    /// "Matching font styles" does. It looks for a face of the requested width first, then of the requested slope,
    /// then of the requested weight. When no face has the value that is asked for, the search goes to the nearest
    /// value, in the direction that CSS gives for the part:
    /// <list type="bullet">
    /// <item>A width of 100% or less tries narrower faces first, and a wider request tries wider faces first.</item>
    /// <item>
    /// An italic request tries italic faces, then oblique faces, then upright faces.
    /// An oblique request tries oblique faces, then italic faces, then upright faces.
    /// An upright request tries upright faces, then oblique faces, then italic faces.
    /// </item>
    /// <item>
    /// A weight between 400 and 500 tries the weights up to 500 first, then lighter weights, then heavier weights.
    /// A weight below 400 tries lighter weights first, and a weight above 500 tries heavier weights first.
    /// </item>
    /// </list>
    /// The library does not make a bold or an oblique face when the family has none. A renderer that wants one
    /// compares the requested style with <see cref="FontDescription.StyleRange"/> of the face.
    /// </summary>
    /// <param name="style">The style to match.</param>
    /// <returns>
    /// The best face. Of the faces that match equally well, the one that was added first.
    /// </returns>
    /// <exception cref="FontException">This is the default <see cref="FontFamily"/>, or it has no faces.</exception>
    public FontMetrics MatchFace(FontStyle style)
    {
        IReadOnlyList<FontMetrics> faces = this.Faces;
        FontStyleRange[] ranges = new FontStyleRange[faces.Count];
        for (int i = 0; i < ranges.Length; i++)
        {
            ranges[i] = faces[i].Description.StyleRange;
        }

        int match = FontStyleMatcher.Match(ranges, style);
        if (match < 0)
        {
            throw new FontException($"The font family '{this.Name}' has no faces.");
        }

        return faces[match];
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is FontFamily family && this.Equals(family);

    /// <inheritdoc/>
    public bool Equals(FontFamily other)
    {
        StringComparer comparer = StringComparerHelpers.GetCaseInsensitiveStringComparer(this.Culture);
        return comparer.Equals(this.Name, other.Name)
            && EqualityComparer<CultureInfo>.Default.Equals(this.Culture, other.Culture)
            && EqualityComparer<IReadOnlyFontMetricsCollection>.Default.Equals(this.collection, other.collection);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.collection, this.Name, this.Culture);

    /// <inheritdoc/>
    public override string ToString() => this.Name;
}
