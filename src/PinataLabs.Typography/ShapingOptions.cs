// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// Describes the run of text that <see cref="TextShaper.Shape"/> shapes: its script, language and direction, and the
/// features that the caller turns on or off. A run is text of one script and one direction, in one font.
/// </summary>
public sealed class ShapingOptions
{
    private IReadOnlyList<ShapingFeature> features = [];

    /// <summary>
    /// Gets the script of the run. The shaper and the lookups of the font follow it, as they follow the script of a
    /// HarfBuzz buffer, so a caller that itemizes text names it. When it is <see langword="null"/>, each character has
    /// the script that Unicode gives it, and the characters of no script of their own, such as spaces and digits, join
    /// the script next to them. <see cref="ScriptClass.Common"/>, <see cref="ScriptClass.Inherited"/> and
    /// <see cref="ScriptClass.Unknown"/> count as no script.
    /// </summary>
    public ScriptClass? Script { get; init; }

    /// <summary>
    /// Gets the tag of the OpenType language system of the run, such as <c>Tag.Parse("TRK ")</c> for Turkish. The
    /// lookups of the language system of the font for the script apply, and those of its default language system when it
    /// has none with this tag. When it is <see langword="null"/>, the default language system applies.
    /// </summary>
    public Tag? Language { get; init; }

    /// <summary>
    /// Gets the direction of the run. The characters are all of this direction: the run is not split by the
    /// Unicode Bidirectional Algorithm, so a caller that has text of both directions shapes each run on its own.
    /// <see cref="TextDirection.Auto"/>, the default, takes the direction of the first character that has one. A run with
    /// no such character, such as digits and brackets, takes the direction of <see cref="Script"/>, as HarfBuzz does for
    /// a buffer, and is left to right when it has no script. <see cref="TextShaper.Shape"/> throws for any other value.
    /// </summary>
    public TextDirection Direction { get; init; } = TextDirection.Auto;

    /// <summary>
    /// Gets the features to turn on or off. A feature that is not listed is as the shaper of the script plans it. It is
    /// empty by default and cannot be <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<ShapingFeature> Features
    {
        get => this.features;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            this.features = value;
        }
    }
}
