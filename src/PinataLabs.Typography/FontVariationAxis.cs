// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.General.Name;

namespace PinataLabs.Typography;

/// <summary>
/// A variation axis of a variable font, as its <c>fvar</c> table lists it: a tag such as <c>wght</c>, and a minimum,
/// a default and a maximum value in the user coordinates of the axis.
/// </summary>
public sealed class FontVariationAxis
{
    private readonly NameTable nameTable;
    private readonly ushort nameId;

    // The values are 16.16 fixed-point numbers in the font, which a double holds exactly.
    // The normalisation of a coordinate uses them, so that it rounds as fontTools does.
    private readonly double minimum;
    private readonly double defaultValue;
    private readonly double maximum;

    internal FontVariationAxis(Tag tag, double minimum, double defaultValue, double maximum, bool isHidden, ushort nameId, NameTable nameTable)
    {
        this.Tag = tag;
        this.minimum = minimum;
        this.defaultValue = defaultValue;
        this.maximum = maximum;
        this.IsHidden = isHidden;
        this.nameId = nameId;
        this.nameTable = nameTable;
    }

    /// <summary>
    /// Gets the tag of the axis, such as <c>wght</c>.
    /// </summary>
    public Tag Tag { get; }

    /// <summary>
    /// Gets the smallest value of the axis, in user coordinates.
    /// </summary>
    public float MinValue => (float)this.minimum;

    /// <summary>
    /// Gets the value of the axis in the default instance, in user coordinates.
    /// </summary>
    public float DefaultValue => (float)this.defaultValue;

    /// <summary>
    /// Gets the largest value of the axis, in user coordinates.
    /// </summary>
    public float MaxValue => (float)this.maximum;

    /// <summary>
    /// Gets a value indicating whether the font marks the axis as hidden, which means that an application
    /// does not show the axis to the user. The <c>fvar</c> flag <c>HIDDEN_AXIS</c> sets it.
    /// </summary>
    public bool IsHidden { get; }

    // The default of the axis, as the exact number from the font.
    internal double ExactDefault => this.defaultValue;

    /// <summary>
    /// Gets the name of the axis, from the <c>name</c> table entry that <c>fvar</c> gives it.
    /// </summary>
    /// <param name="culture">The culture to load the name in.</param>
    /// <returns>The name, or an empty string when the font has no such name.</returns>
    public string Name(CultureInfo culture) => this.nameTable.GetNameById(culture, this.nameId);

    // Clamps a user coordinate to the range of the axis. A value that is not a number gives the default.
    internal double Clamp(double value)
    {
        if (double.IsNaN(value))
        {
            return this.defaultValue;
        }

        return Math.Max(this.minimum, Math.Min(this.maximum, value));
    }

    // Maps a user coordinate to -1 at the minimum, 0 at the default and 1 at the maximum, as steps 1 and 2 of the
    // normalisation in the Font Variations Overview say, and as fontTools normalizeValue does. The result is not yet
    // mapped by 'avar'.
    internal double Normalize(double value)
    {
        value = this.Clamp(value);
#pragma warning disable S1244 // The specification maps the exact default value to 0.
        if (value == this.defaultValue || this.minimum == this.maximum)
#pragma warning restore S1244
        {
            return 0;
        }

        return value < this.defaultValue
            ? (value - this.defaultValue) / (this.defaultValue - this.minimum)
            : (value - this.defaultValue) / (this.maximum - this.defaultValue);
    }
}
