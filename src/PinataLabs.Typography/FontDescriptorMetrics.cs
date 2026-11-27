// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// The values that a PDF font descriptor is written from, in font design units (<see cref="FontMetrics.UnitsPerEm"/>
/// to the em), each with the table field it came from.
/// </summary>
public sealed class FontDescriptorMetrics
{
    internal FontDescriptorMetrics(
        short ascent,
        MetricSource ascentSource,
        short descent,
        MetricSource descentSource,
        short capHeight,
        MetricSource capHeightSource,
        short xHeight,
        MetricSource xHeightSource,
        float italicAngle,
        FontBoundingBox boundingBox,
        FontDescriptorFlags flags)
    {
        this.Ascent = ascent;
        this.AscentSource = ascentSource;
        this.Descent = descent;
        this.DescentSource = descentSource;
        this.CapHeight = capHeight;
        this.CapHeightSource = capHeightSource;
        this.XHeight = xHeight;
        this.XHeightSource = xHeightSource;
        this.ItalicAngle = italicAngle;
        this.BoundingBox = boundingBox;
        this.Flags = flags;
    }

    /// <summary>
    /// Gets the ascent, above the baseline.
    /// </summary>
    public short Ascent { get; }

    /// <summary>
    /// Gets where <see cref="Ascent"/> came from.
    /// </summary>
    public MetricSource AscentSource { get; }

    /// <summary>
    /// Gets the descent, which is negative below the baseline.
    /// </summary>
    public short Descent { get; }

    /// <summary>
    /// Gets where <see cref="Descent"/> came from.
    /// </summary>
    public MetricSource DescentSource { get; }

    /// <summary>
    /// Gets the height of a flat capital letter above the baseline.
    /// </summary>
    public short CapHeight { get; }

    /// <summary>
    /// Gets where <see cref="CapHeight"/> came from.
    /// </summary>
    public MetricSource CapHeightSource { get; }

    /// <summary>
    /// Gets the height of a flat lowercase letter above the baseline, or 0 when the font gives none.
    /// </summary>
    public short XHeight { get; }

    /// <summary>
    /// Gets where <see cref="XHeight"/> came from.
    /// </summary>
    public MetricSource XHeightSource { get; }

    /// <summary>
    /// Gets the italic angle in counter-clockwise degrees from the vertical. It is negative for text that leans to the right.
    /// </summary>
    public float ItalicAngle { get; }

    /// <summary>
    /// Gets where <see cref="ItalicAngle"/> came from, which is always <see cref="MetricSource.PostItalicAngle"/>.
    /// </summary>
    public MetricSource ItalicAngleSource => MetricSource.PostItalicAngle;

    /// <summary>
    /// Gets the box that holds every glyph of the face, as its <c>head</c> table gives it.
    /// </summary>
    public FontBoundingBox BoundingBox { get; }

    /// <summary>
    /// Gets where <see cref="BoundingBox"/> came from, which is always <see cref="MetricSource.HeadBounds"/>.
    /// </summary>
    public MetricSource BoundingBoxSource => MetricSource.HeadBounds;

    /// <summary>
    /// Gets the flags that the tables of the font state. See <see cref="FontDescriptorFlags"/>.
    /// </summary>
    public FontDescriptorFlags Flags { get; }
}
