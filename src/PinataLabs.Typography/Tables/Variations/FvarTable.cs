// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.General.Name;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'fvar' table: the variation axes of a variable font and its named instances, with the 'avar' segment maps that
/// belong to the axes. It also holds the rules that turn a requested style, a size and explicit variations into
/// user coordinates, and user coordinates into <see cref="NormalizedCoordinates"/>.
/// https://learn.microsoft.com/typography/opentype/spec/fvar
/// </summary>
/// <remarks>
/// A table that is malformed makes <see cref="Load(FontReader)"/> give <see langword="null"/>, so that the font loads as a
/// static font. A malformed 'avar' table, or one that has a different number of axes, counts as absent.
/// </remarks>
internal sealed class FvarTable : Table
{
    internal const string TableName = "fvar";

    private const int HeaderSize = 16;
    private const int AxisRecordSize = 20;
    private const int HiddenAxisFlag = 1;
    private const double Fixed16Scale = 65536D;
    private const double F2Dot14Scale = 16384D;

    // The slant that CSS gives an italic request on a font that has no italic axis.
    private const double ItalicSlant = -14D;

    internal static readonly Tag WeightTag = Tag.Parse("wght");
    internal static readonly Tag WidthTag = Tag.Parse("wdth");
    internal static readonly Tag ItalicTag = Tag.Parse("ital");
    internal static readonly Tag SlantTag = Tag.Parse("slnt");
    internal static readonly Tag OpticalSizeTag = Tag.Parse("opsz");

    private readonly FontVariationAxis[] axes;
    private readonly FontNamedInstance[] namedInstances;
    private readonly AvarTable? avar;
    private readonly bool hasItalicAxis;

    private FvarTable(FontVariationAxis[] axes, AvarTable? avar, ushort[] nameIds, double[][] instanceCoordinates, NameTable nameTable)
    {
        this.axes = axes;
        this.avar = avar;
        this.Axes = Array.AsReadOnly(axes);
        foreach (FontVariationAxis axis in axes)
        {
            this.hasItalicAxis |= axis.Tag == ItalicTag;
        }

        double[] defaults = new double[axes.Length];
        for (int i = 0; i < axes.Length; i++)
        {
            defaults[i] = axes[i].ExactDefault;
        }

        this.DefaultCoordinates = this.Normalize(defaults);
        this.DefaultVariations = this.CreateVariations(defaults);

        this.namedInstances = new FontNamedInstance[nameIds.Length];
        for (int i = 0; i < nameIds.Length; i++)
        {
            for (int a = 0; a < axes.Length; a++)
            {
                instanceCoordinates[i][a] = axes[a].Clamp(instanceCoordinates[i][a]);
            }

            this.namedInstances[i] = new FontNamedInstance(
                nameIds[i],
                nameTable,
                this.CreateVariations(instanceCoordinates[i]),
                this.Normalize(instanceCoordinates[i]));
        }

        this.NamedInstances = Array.AsReadOnly(this.namedInstances);
    }

    /// <summary>
    /// Gets the axes of the font, in the order of the table.
    /// </summary>
    public IReadOnlyList<FontVariationAxis> Axes { get; }

    /// <summary>
    /// Gets the named instances of the font, in the order of the table.
    /// </summary>
    public IReadOnlyList<FontNamedInstance> NamedInstances { get; }

    /// <summary>
    /// Gets the coordinates of the default instance, which are 0 on every axis.
    /// </summary>
    public NormalizedCoordinates DefaultCoordinates { get; }

    /// <summary>
    /// Gets the default value of every axis, in the order of the axes.
    /// </summary>
    public IReadOnlyList<FontVariation> DefaultVariations { get; }

    /// <summary>
    /// Loads the 'fvar' table, with the 'avar' table that belongs to it.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none, or it is malformed.</returns>
    public static FvarTable? Load(FontReader reader)
    {
        byte[]? data = reader.TryReadTableBytes(TableName);
        return data is null ? null : Parse(data, reader.GetTable<NameTable>(), reader.TryGetTable<AvarTable>());
    }

    /// <summary>
    /// Reads an 'fvar' table.
    /// </summary>
    /// <param name="data">The bytes of the table.</param>
    /// <param name="nameTable">The 'name' table, which holds the names of the axes and the instances.</param>
    /// <param name="avar">The 'avar' table, if the font has one. A table for a different number of axes is not used.</param>
    /// <returns>The table, or <see langword="null"/> when it is malformed or has no axes.</returns>
    public static FvarTable? Parse(ReadOnlySpan<byte> data, NameTable nameTable, AvarTable? avar)
    {
        // uint16 majorVersion, uint16 minorVersion, Offset16 axesArrayOffset, uint16 reserved,
        // uint16 axisCount, uint16 axisSize, uint16 instanceCount, uint16 instanceSize.
        if (data.Length < HeaderSize)
        {
            return null;
        }

        int majorVersion = BinaryPrimitives.ReadUInt16BigEndian(data);
        int axesOffset = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(4));
        int axisCount = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(8));
        int axisSize = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(10));
        int instanceCount = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(12));
        int instanceSize = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(14));
        if (majorVersion != 1 || axisCount == 0 || axisSize != AxisRecordSize)
        {
            return null;
        }

        // The instance records follow the axis records. An instance has a name ID, flags and a coordinate for each
        // axis, and it can end with the name ID of a PostScript name.
        long instancesOffset = axesOffset + ((long)axisCount * AxisRecordSize);
        int minimumInstanceSize = 4 + (4 * axisCount);
        if (instancesOffset > data.Length
            || (instanceCount > 0 && (instanceSize < minimumInstanceSize || instancesOffset + ((long)instanceCount * instanceSize) > data.Length)))
        {
            return null;
        }

        FontVariationAxis[] axes = new FontVariationAxis[axisCount];
        for (int i = 0; i < axisCount; i++)
        {
            ReadOnlySpan<byte> record = data.Slice(axesOffset + (i * AxisRecordSize), AxisRecordSize);
            double minimum = BinaryPrimitives.ReadInt32BigEndian(record.Slice(4)) / Fixed16Scale;
            double defaultValue = BinaryPrimitives.ReadInt32BigEndian(record.Slice(8)) / Fixed16Scale;
            double maximum = BinaryPrimitives.ReadInt32BigEndian(record.Slice(12)) / Fixed16Scale;
            if (minimum > defaultValue || defaultValue > maximum)
            {
                return null;
            }

            axes[i] = new FontVariationAxis(
                new Tag(BinaryPrimitives.ReadUInt32BigEndian(record)),
                minimum,
                defaultValue,
                maximum,
                (BinaryPrimitives.ReadUInt16BigEndian(record.Slice(16)) & HiddenAxisFlag) != 0,
                BinaryPrimitives.ReadUInt16BigEndian(record.Slice(18)),
                nameTable);
        }

        ushort[] nameIds = new ushort[instanceCount];
        double[][] coordinates = new double[instanceCount][];
        for (int i = 0; i < instanceCount; i++)
        {
            ReadOnlySpan<byte> record = data.Slice((int)instancesOffset + (i * instanceSize), instanceSize);
            nameIds[i] = BinaryPrimitives.ReadUInt16BigEndian(record);
            coordinates[i] = new double[axisCount];
            for (int axis = 0; axis < axisCount; axis++)
            {
                coordinates[i][axis] = BinaryPrimitives.ReadInt32BigEndian(record.Slice(4 + (4 * axis))) / Fixed16Scale;
            }
        }

        return new FvarTable(axes, avar?.AxisCount == axisCount ? avar : null, nameIds, coordinates, nameTable);
    }

    /// <summary>
    /// Gets the first axis with a tag.
    /// </summary>
    /// <param name="tag">The tag of the axis.</param>
    /// <param name="axis">The axis.</param>
    /// <returns><see langword="true"/> if the font has such an axis.</returns>
    public bool TryGetAxis(Tag tag, [NotNullWhen(true)] out FontVariationAxis? axis)
    {
        foreach (FontVariationAxis candidate in this.axes)
        {
            if (candidate.Tag == tag)
            {
                axis = candidate;
                return true;
            }
        }

        axis = null;
        return false;
    }

    /// <summary>
    /// Gets the user coordinates that a font draws for a request: the style sets <c>wght</c>, <c>wdth</c>, <c>ital</c>
    /// and <c>slnt</c>, the size in points sets <c>opsz</c>, and then the explicit variations set any axis. Every value is
    /// clamped to the range of its axis, and an axis that nothing sets has its default.
    /// </summary>
    /// <param name="style">The requested style.</param>
    /// <param name="size">The size of the font in points.</param>
    /// <param name="variations">The explicit variations. For an axis that appears more than once, the last value wins.</param>
    /// <returns>One user coordinate for each axis, in the order of the axes.</returns>
    public double[] CreateUserCoordinates(FontStyle style, float size, ReadOnlySpan<FontVariation> variations)
    {
        double[] coordinates = new double[this.axes.Length];
        for (int i = 0; i < coordinates.Length; i++)
        {
            FontVariationAxis axis = this.axes[i];
            double value = axis.ExactDefault;
            Tag tag = axis.Tag;
            if (tag == WeightTag)
            {
                value = style.Weight.Value;
            }
            else if (tag == WidthTag)
            {
                value = style.Width.Percent;
            }
            else if (tag == ItalicTag)
            {
                value = style.Slope.Kind == FontSlopeKind.Italic ? 1 : 0;
            }
            else if (tag == SlantTag)
            {
                value = style.Slope.Kind switch
                {
                    FontSlopeKind.Oblique => -style.Slope.ObliqueAngle,
                    FontSlopeKind.Italic when !this.hasItalicAxis => ItalicSlant,
                    _ => 0,
                };
            }
            else if (tag == OpticalSizeTag)
            {
                value = size;
            }

            foreach (FontVariation variation in variations)
            {
                if (variation.Axis == tag)
                {
                    value = variation.Value;
                }
            }

            coordinates[i] = axis.Clamp(value);
        }

        return coordinates;
    }

    /// <summary>
    /// Normalises user coordinates as steps 1 to 5 of "Coordinate Scales and Normalization" in the Font Variations
    /// Overview say: clamp each coordinate to the range of its axis, map it to -1 at the minimum, 0 at the default and 1
    /// at the maximum, apply the 'avar' segment map of the axis, clamp to -1 to 1, and round to F2Dot14 with
    /// fontTools <c>floatToFixed</c>, which adds a half and takes the floor.
    /// </summary>
    /// <param name="userCoordinates">One user coordinate for each axis, in the order of the axes. An axis without one is at its default.</param>
    /// <returns>The normalised coordinates.</returns>
    public NormalizedCoordinates Normalize(ReadOnlySpan<double> userCoordinates)
    {
        short[] values = new short[this.axes.Length];
        for (int i = 0; i < values.Length; i++)
        {
            FontVariationAxis axis = this.axes[i];
            double normalised = axis.Normalize(i < userCoordinates.Length ? userCoordinates[i] : axis.ExactDefault);
            if (this.avar is not null)
            {
                normalised = this.avar.Map(i, normalised);
            }

            normalised = Math.Max(-1D, Math.Min(1D, normalised));
            values[i] = (short)Math.Floor((normalised * F2Dot14Scale) + 0.5);
        }

        return new NormalizedCoordinates(values);
    }

    /// <summary>
    /// Pairs user coordinates with the tags of the axes.
    /// </summary>
    /// <param name="userCoordinates">One user coordinate for each axis, in the order of the axes.</param>
    /// <returns>The variations, in the order of the axes.</returns>
    public IReadOnlyList<FontVariation> CreateVariations(ReadOnlySpan<double> userCoordinates)
    {
        FontVariation[] variations = new FontVariation[this.axes.Length];
        for (int i = 0; i < variations.Length; i++)
        {
            variations[i] = new FontVariation(this.axes[i].Tag, (float)userCoordinates[i]);
        }

        return Array.AsReadOnly(variations);
    }

    /// <summary>
    /// Finds the named instance at some coordinates.
    /// </summary>
    /// <param name="coordinates">The normalised coordinates.</param>
    /// <param name="instance">The first named instance whose coordinates equal <paramref name="coordinates"/>.</param>
    /// <returns><see langword="true"/> if the font has a named instance there.</returns>
    public bool TryFindNamedInstance(NormalizedCoordinates coordinates, [NotNullWhen(true)] out FontNamedInstance? instance)
    {
        foreach (FontNamedInstance candidate in this.namedInstances)
        {
            if (candidate.Coordinates == coordinates)
            {
                instance = candidate;
                return true;
            }
        }

        instance = null;
        return false;
    }
}
