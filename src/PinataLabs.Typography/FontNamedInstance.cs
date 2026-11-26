// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography;

/// <summary>
/// A variation instance that the font lists in its <c>fvar</c> table with a name, such as "SemiBold".
/// To draw it, pass <see cref="Variations"/> to <see cref="FontFamily.CreateFont"/>.
/// </summary>
public sealed class FontNamedInstance
{
    private readonly NameTable nameTable;
    private readonly ushort nameId;

    internal FontNamedInstance(ushort nameId, NameTable nameTable, IReadOnlyList<FontVariation> variations, NormalizedCoordinates coordinates)
    {
        this.nameId = nameId;
        this.nameTable = nameTable;
        this.Variations = variations;
        this.Coordinates = coordinates;
    }

    /// <summary>
    /// Gets the values of the instance on every axis of the font, in the order of the axes in
    /// <see cref="FontMetrics.VariationAxes"/>, in user coordinates.
    /// </summary>
    public IReadOnlyList<FontVariation> Variations { get; }

    // The normalised coordinates of the instance, with the 'avar' mapping applied.
    internal NormalizedCoordinates Coordinates { get; }

    /// <summary>
    /// Gets the subfamily name of the instance, from the <c>name</c> table entry that <c>fvar</c> gives it.
    /// </summary>
    /// <param name="culture">The culture to load the name in.</param>
    /// <returns>The name, or an empty string when the font has no such name.</returns>
    public string Name(CultureInfo culture) => this.nameTable.GetNameById(culture, this.nameId);
}
