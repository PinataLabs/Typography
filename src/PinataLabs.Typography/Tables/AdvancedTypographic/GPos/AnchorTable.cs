// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using PinataLabs.Typography.Tables.TrueType;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GPos;

[DebuggerDisplay("X: {XCoordinate}, Y: {YCoordinate}")]
internal abstract class AnchorTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnchorTable"/> class.
    /// </summary>
    /// <param name="xCoordinate">The horizontal value, in design units.</param>
    /// <param name="yCoordinate">The vertical value, in design units.</param>
    protected AnchorTable(short xCoordinate, short yCoordinate)
    {
        this.XCoordinate = xCoordinate;
        this.YCoordinate = yCoordinate;
    }

    /// <summary>
    /// Gets the horizontal value, in design units.
    /// </summary>
    protected short XCoordinate { get; }

    /// <summary>
    /// Gets the vertical value, in design units.
    /// </summary>
    protected short YCoordinate { get; }

    public abstract AnchorXY GetAnchor(FontMetrics fontMetrics, GlyphShapingData data, GlyphPositioningCollection collection);

    /// <summary>
    /// Loads the anchor table.
    /// </summary>
    /// <param name="reader">The big endian binary reader.</param>
    /// <param name="offset">The offset to the beginning of the anchor table.</param>
    /// <returns>The anchor table.</returns>
    public static AnchorTable Load(BigEndianBinaryReader reader, long offset)
    {
        reader.Seek(offset, SeekOrigin.Begin);
        ushort anchorFormat = reader.ReadUInt16();

        return anchorFormat switch
        {
            1 => AnchorFormat1.Load(reader),
            2 => AnchorFormat2.Load(reader),
            3 => AnchorFormat3.LoadWithVariations(reader, offset),
            _ => throw new InvalidFontFileException($"anchorFormat identifier {anchorFormat} is invalid. Should be '1', '2' or '3'.")
        };
    }

    internal sealed class AnchorFormat1 : AnchorTable
    {
        public AnchorFormat1(short xCoordinate, short yCoordinate)
            : base(xCoordinate, yCoordinate)
        {
        }

        public static AnchorFormat1 Load(BigEndianBinaryReader reader)
        {
            // +--------------+------------------------+------------------------------------------------+
            // | Type         | Name                   | Description                                    |
            // +==============+========================+================================================+
            // | uint16       | anchorFormat           | Format identifier, = 1                         |
            // +--------------+------------------------+------------------------------------------------+
            // | int16        | xCoordinate            | Horizontal value, in design units.             |
            // +--------------+------------------------+------------------------------------------------+
            // | int16        | yCoordinate            | Vertical value, in design units.               |
            // +--------------+------------------------+------------------------------------------------+
            short xCoordinate = reader.ReadInt16();
            short yCoordinate = reader.ReadInt16();
            return new AnchorFormat1(xCoordinate, yCoordinate);
        }

        public override AnchorXY GetAnchor(FontMetrics fontMetrics, GlyphShapingData data, GlyphPositioningCollection collection)
            => new(this.XCoordinate, this.YCoordinate);
    }

    internal sealed class AnchorFormat2 : AnchorTable
    {
        private readonly ushort anchorPointIndex;

        public AnchorFormat2(short xCoordinate, short yCoordinate, ushort anchorPointIndex)
            : base(xCoordinate, yCoordinate) => this.anchorPointIndex = anchorPointIndex;

        public static AnchorFormat2 Load(BigEndianBinaryReader reader)
        {
            // +--------------+------------------------+------------------------------------------------+
            // | Type         | Name                   | Description                                    |
            // +==============+========================+================================================+
            // | uint16       | anchorFormat           | Format identifier, = 2                         |
            // +--------------+------------------------+------------------------------------------------+
            // | int16        | xCoordinate            | Horizontal value, in design units.             |
            // +--------------+------------------------+------------------------------------------------+
            // | int16        | yCoordinate            | Vertical value, in design units.               |
            // +--------------+------------------------+------------------------------------------------+
            // | uint16       + anchorPoint            | Index to glyph contour point.                  +
            // +--------------+------------------------+------------------------------------------------+
            short xCoordinate = reader.ReadInt16();
            short yCoordinate = reader.ReadInt16();
            ushort anchorPointIndex = reader.ReadUInt16();
            return new AnchorFormat2(xCoordinate, yCoordinate, anchorPointIndex);
        }

        public override AnchorXY GetAnchor(FontMetrics fontMetrics, GlyphShapingData data, GlyphPositioningCollection collection)
        {
            if (collection.TextOptions.HintingMode != HintingMode.None)
            {
                TextAttributes textAttributes = data.TextRun.TextAttributes;
                TextDecorations textDecorations = data.TextRun.TextDecorations;
                LayoutMode layoutMode = collection.TextOptions.LayoutMode;
                ColorFontSupport colorFontSupport = collection.TextOptions.ColorFontSupport;
                if (fontMetrics.TryGetGlyphMetrics(data.CodePoint, textAttributes, textDecorations, layoutMode, colorFontSupport, out IReadOnlyList<GlyphMetrics>? metrics))
                {
                    foreach (GlyphMetrics metric in metrics)
                    {
                        // TODO: What does HarfBuzz do here?
                        if (metric is not TrueTypeGlyphMetrics ttmetric)
                        {
                            break;
                        }

                        if (ttmetric.TryGetPoint(this.anchorPointIndex, out Vector2 point))
                        {
                            return new((short)point.X, (short)point.Y);
                        }
                    }
                }
            }

            return new(this.XCoordinate, this.YCoordinate);
        }
    }

    internal sealed class AnchorFormat3 : AnchorTable
    {
        private readonly VariationIndex xVariation;
        private readonly VariationIndex yVariation;

        public AnchorFormat3(short xCoordinate, short yCoordinate, VariationIndex xVariation, VariationIndex yVariation)
            : base(xCoordinate, yCoordinate)
        {
            this.xVariation = xVariation;
            this.yVariation = yVariation;
        }

        public static AnchorFormat3 LoadWithVariations(BigEndianBinaryReader reader, long anchorOffset)
        {
            // +--------------+------------------------+-----------------------------------------------------------+
            // | Type         | Name                   | Description                                               |
            // +==============+========================+===========================================================+
            // | uint16       | anchorFormat           | Format identifier, = 3                                    |
            // +--------------+------------------------+-----------------------------------------------------------+
            // | int16        | xCoordinate            | Horizontal value, in design units.                        |
            // +--------------+------------------------+-----------------------------------------------------------+
            // | int16        | yCoordinate            | Vertical value, in design units.                          |
            // +--------------+------------------------+-----------------------------------------------------------+
            // | uint16       + anchorPoint            | Index to glyph contour point.                             +
            // +--------------+------------------------+-----------------------------------------------------------+
            // | Offset16     | xDeviceOffset          + Offset to Device table (non-variable font) /              |
            // |              |                        | VariationIndex table (variable font) for X coordinate,    |
            // |              |                        | from beginning of Anchor table (may be NULL)              |
            // +--------------+------------------------+-----------------------------------------------------------+
            // | Offset16     | yDeviceOffset          + Offset to Device table (non-variable font) /              |
            // |              |                        | VariationIndex table (variable font) for Y coordinate,    |
            // |              |                        | from beginning of Anchor table (may be NULL)              |
            // +--------------+------------------------+-----------------------------------------------------------+
            short xCoordinate = reader.ReadInt16();
            short yCoordinate = reader.ReadInt16();
            ushort xDeviceOffset = reader.ReadOffset16();
            ushort yDeviceOffset = reader.ReadOffset16();

            // Only a VariationIndex table has a use. A Device table holds deltas for sizes of a font that does not vary.
            return new AnchorFormat3(
                xCoordinate,
                yCoordinate,
                DeviceTable.LoadVariationIndex(reader, anchorOffset, xDeviceOffset),
                DeviceTable.LoadVariationIndex(reader, anchorOffset, yDeviceOffset));
        }

        public override AnchorXY GetAnchor(FontMetrics fontMetrics, GlyphShapingData data, GlyphPositioningCollection collection)
            => new(this.XCoordinate + fontMetrics.GetDeviceDelta(this.xVariation), this.YCoordinate + fontMetrics.GetDeviceDelta(this.yVariation));
    }
}
