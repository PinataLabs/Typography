// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// Defines a particular format for text, including font face, size, and style attributes.
/// This class cannot be inherited.
/// </summary>
public sealed class Font
{
    private readonly Lazy<FontMetrics> metrics;
    private readonly Lazy<string> fontName;
    private readonly FontVariation[] variations;

    /// <summary>
    /// Initializes a new instance of the <see cref="Font"/> class.
    /// </summary>
    /// <param name="family">The font family.</param>
    /// <param name="size">The size of the font in PT units.</param>
    /// <param name="style">The font style. The default value is <see cref="FontStyle.Regular"/>.</param>
    /// <param name="variations">
    /// The values for the axes of a variable font, which win over the values that the style and the size give.
    /// A font ignores a variation for an axis that it lacks, clamps a value to the range of its axis, and takes the
    /// last value for an axis that appears more than once. A static font ignores them all.
    /// </param>
    public Font(FontFamily family, float size, FontStyle style = default, params ReadOnlySpan<FontVariation> variations)
    {
        if (family == default)
        {
            throw new ArgumentException("Cannot use the default value type instance to create a font.", nameof(family));
        }

        this.Family = family;
        this.Style = style;
        this.Size = size;
        this.variations = RemoveRepeatedAxes(variations);
        this.Variations = this.variations.Length == 0 ? this.variations : Array.AsReadOnly(this.variations);
        this.metrics = new Lazy<FontMetrics>(this.LoadMetrics);
        this.fontName = new Lazy<string>(this.LoadFontName);
    }

    /// <summary>
    /// Gets the family.
    /// </summary>
    public FontFamily Family { get; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name => this.fontName.Value;

    /// <summary>
    /// Gets the size of the font in PT units.
    /// </summary>
    public float Size { get; }

    /// <summary>
    /// Gets the metrics that the font draws with. It is the face that matches <see cref="Style"/>, as
    /// <see cref="FontFamily.MatchFace(FontStyle)"/> picks it, or for a variable face the variation instance that the
    /// style, the size and <see cref="Variations"/> ask for. Fonts that ask for the same coordinates share one instance.
    /// <see cref="FontMetrics.Variations"/> gives the axis values of the instance.
    /// </summary>
    public FontMetrics FontMetrics => this.metrics.Value;

    /// <summary>
    /// Gets the style that was asked for. The face that the font draws can differ from it:
    /// <see cref="FontMetrics"/> is the face that matches the style best.
    /// </summary>
    public FontStyle Style { get; }

    /// <summary>
    /// Gets the variations that were asked for, in the order of their first appearance. An axis that was given more than
    /// once appears once, with its last value. A variation for an axis that the face lacks stays in the list.
    /// </summary>
    public IReadOnlyList<FontVariation> Variations { get; }

    /// <summary>
    /// Gets the filesystem path to the font family source.
    /// </summary>
    /// <param name="path">
    /// When this method returns, contains the filesystem path to the font family source,
    /// if the path exists; otherwise, the default value for the type of the path parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if the <see cref="Font" /> was created via a filesystem path; otherwise, <see langword="false" />.
    /// </returns>
    public bool TryGetPath([NotNullWhen(true)] out string? path)
    {
        if (this == default)
        {
            FontsThrowHelper.ThrowDefaultInstance();
        }

        // The path belongs to the face, so it is read without loading the font to make a variation instance.
        if (this.Family.MatchFace(this.Style) is FileFontMetrics fileMetrics)
        {
            path = fileMetrics.Path;
            return true;
        }

        path = null;
        return false;
    }

    /// <summary>
    /// Creates a font of the same family and style at another size.
    /// </summary>
    /// <param name="size">The size of the new font in PT units.</param>
    /// <returns>The new <see cref="Font"/>.</returns>
    public Font WithSize(float size)
        => new(this.Family, size, this.Style, this.variations);

    /// <summary>
    /// Creates a font of the same family and size in another style.
    /// </summary>
    /// <param name="style">The style of the new font.</param>
    /// <returns>The new <see cref="Font"/>.</returns>
    public Font WithStyle(FontStyle style)
        => new(this.Family, this.Size, style, this.variations);

    /// <summary>
    /// Creates a font of the same family, size and style with other variations. They replace the variations of this font.
    /// </summary>
    /// <param name="variations">The variations of the new font.</param>
    /// <returns>The new <see cref="Font"/>.</returns>
    public Font WithVariations(params ReadOnlySpan<FontVariation> variations)
        => new(this.Family, this.Size, this.Style, variations);

    /// <summary>
    /// Gets the glyphs for the given codepoint.
    /// </summary>
    /// <param name="codePoint">The code point of the character.</param>
    /// <param name="glyphs">
    /// When this method returns, contains the glyphs for the given codepoint if the glyphs
    /// are found; otherwise the default value. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the face contains glyphs for the specified codepoint; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetGlyphs(CodePoint codePoint, [NotNullWhen(true)] out IReadOnlyList<Glyph>? glyphs)
        => this.TryGetGlyphs(codePoint, TextAttributes.None, ColorFontSupport.None, out glyphs);

    /// <summary>
    /// Gets the glyphs for the given codepoint.
    /// </summary>
    /// <param name="codePoint">The code point of the character.</param>
    /// <param name="support">Options for enabling color font support during layout and rendering.</param>
    /// <param name="glyphs">
    /// When this method returns, contains the glyphs for the given codepoint and color support if the glyphs
    /// are found; otherwise the default value. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the face contains glyphs for the specified codepoint; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetGlyphs(CodePoint codePoint, ColorFontSupport support, [NotNullWhen(true)] out IReadOnlyList<Glyph>? glyphs)
        => this.TryGetGlyphs(codePoint, TextAttributes.None, support, out glyphs);

    /// <summary>
    /// Gets the glyphs for the given codepoint.
    /// </summary>
    /// <param name="codePoint">The code point of the character.</param>
    /// <param name="textAttributes">The text attributes to apply to the glyphs.</param>
    /// <param name="support">Options for enabling color font support during layout and rendering.</param>
    /// <param name="glyphs">
    /// When this method returns, contains the glyphs for the given codepoint, attributes, and color support if the glyphs
    /// are found; otherwise the default value. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the face contains glyphs for the specified codepoint; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetGlyphs(
        CodePoint codePoint,
        TextAttributes textAttributes,
        ColorFontSupport support,
        [NotNullWhen(true)] out IReadOnlyList<Glyph>? glyphs)
        => this.TryGetGlyphs(codePoint, textAttributes, TextDecorations.None, LayoutMode.HorizontalTopBottom, support, out glyphs);

    /// <summary>
    /// Gets the glyphs for the given codepoint.
    /// </summary>
    /// <param name="codePoint">The code point of the character.</param>
    /// <param name="textAttributes">The text attributes to apply to the glyphs.</param>
    /// <param name="layoutMode">The layout mode to apply to thte glyphs.</param>
    /// <param name="support">Options for enabling color font support during layout and rendering.</param>
    /// <param name="glyphs">
    /// When this method returns, contains the glyphs for the given codepoint, attributes, and color support if the glyphs
    /// are found; otherwise the default value. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the face contains glyphs for the specified codepoint; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetGlyphs(
        CodePoint codePoint,
        TextAttributes textAttributes,
        LayoutMode layoutMode,
        ColorFontSupport support,
        [NotNullWhen(true)] out IReadOnlyList<Glyph>? glyphs)
        => this.TryGetGlyphs(codePoint, textAttributes, TextDecorations.None, layoutMode, support, out glyphs);

    /// <summary>
    /// Gets the glyphs for the given codepoint.
    /// </summary>
    /// <param name="codePoint">The code point of the character.</param>
    /// <param name="textAttributes">The text attributes to apply to the glyphs.</param>
    /// <param name="textDecorations">The text decorations to apply to the glyphs.</param>
    /// <param name="layoutMode">The layout mode to apply to thte glyphs.</param>
    /// <param name="support">Options for enabling color font support during layout and rendering.</param>
    /// <param name="glyphs">
    /// When this method returns, contains the glyphs for the given codepoint, attributes, and color support if the glyphs
    /// are found; otherwise the default value. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the face contains glyphs for the specified codepoint; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetGlyphs(
        CodePoint codePoint,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        LayoutMode layoutMode,
        ColorFontSupport support,
        [NotNullWhen(true)] out IReadOnlyList<Glyph>? glyphs)
    {
        TextRun textRun = new() { Start = 0, End = 1, Font = this, TextAttributes = textAttributes, TextDecorations = textDecorations };
        if (this.FontMetrics.TryGetGlyphMetrics(codePoint, textAttributes, textDecorations, layoutMode, support, out IReadOnlyList<GlyphMetrics>? metrics))
        {
            List<Glyph> g = [];
            foreach (GlyphMetrics metric in metrics)
            {
                g.Add(new(metric.CloneForRendering(textRun, codePoint), this.Size));
            }

            glyphs = g;
            return true;
        }

        glyphs = default;
        return false;
    }

    /// <summary>
    /// Gets the amount, in px units, the <paramref name="current"/> glyph should be offset if it is proceeded by
    /// the <paramref name="previous"/> glyph.
    /// </summary>
    /// <param name="previous">The previous glyph.</param>
    /// <param name="current">The current glyph.</param>
    /// <param name="dpi">The DPI (Dots Per Inch) to render/measure the kerning offset at.</param>
    /// <param name="vector">
    /// When this method returns, contains the offset, in font units, that should be applied to the
    /// <paramref name="current"/> glyph, if the offset is found; otherwise the default vector value.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the face contains and offset for the glyph combination; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetKerningOffset(Glyph previous, Glyph current, float dpi, out Vector2 vector)
    {
        if (this.FontMetrics.TryGetKerningOffset(previous.GlyphMetrics.GlyphId, current.GlyphMetrics.GlyphId, out vector))
        {
            // Scale the result
            Vector2 scale = new Vector2(this.Size * dpi) / current.GlyphMetrics.ScaleFactor;
            vector *= scale;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a font of another family with the size, the style and the variations of this font.
    /// </summary>
    /// <param name="family">The family of the new font.</param>
    /// <returns>The new <see cref="Font"/>.</returns>
    internal Font WithFamily(FontFamily family)
        => new(family, this.Size, this.Style, this.variations);

    // A variation that repeats an axis replaces the earlier one in its place.
    private static FontVariation[] RemoveRepeatedAxes(ReadOnlySpan<FontVariation> variations)
    {
        if (variations.IsEmpty)
        {
            return [];
        }

        FontVariation[] distinct = new FontVariation[variations.Length];
        int count = 0;
        foreach (FontVariation variation in variations)
        {
            int index = 0;
            while (index < count && distinct[index].Axis != variation.Axis)
            {
                index++;
            }

            distinct[index] = variation;
            count = Math.Max(count, index + 1);
        }

        return count == distinct.Length ? distinct : distinct[..count];
    }

    // The face that matches the style, or for a variable face the instance that the style, the size and the variations give.
    private FontMetrics LoadMetrics()
    {
        FontMetrics face = this.Family.MatchFace(this.Style);
        FvarTable? fvar = face.Description.Fvar;
        if (fvar is null)
        {
            return face;
        }

        double[] userCoordinates = fvar.CreateUserCoordinates(this.Style, this.Size, this.variations);
        return face.GetInstance(fvar.Normalize(userCoordinates), userCoordinates);
    }

    // A face has the name that its name table gives it. An instance that the font names is the family and that name,
    // as in "Teko SemiBold".
    private string LoadFontName()
    {
        FontMetrics metrics = this.metrics.Value;
        CultureInfo culture = this.Family.Culture;
        if (metrics.Description.Fvar is FvarTable fvar && fvar.TryFindNamedInstance(metrics.Coordinates, out FontNamedInstance? named))
        {
            string subfamily = named.Name(culture);
            if (subfamily.Length > 0)
            {
                return this.Family.Name + " " + subfamily;
            }
        }

        return metrics.Description.FontName(culture);
    }
}
