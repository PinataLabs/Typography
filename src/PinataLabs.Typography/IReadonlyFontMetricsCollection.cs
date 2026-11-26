// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PinataLabs.Typography;

/// <summary>
/// Represents a readonly collection of font metrics.
/// The interface uses compiler pattern matching to provide enumeration capabilities.
/// </summary>
internal interface IReadOnlyFontMetricsCollection
{
    /// <summary>
    /// Gets the collection of available font metrics for a given culture and font family name.
    /// </summary>
    /// <param name="name">The font family name.</param>
    /// <param name="culture">The culture to use when searching for a match.</param>
    /// <returns>The font metrics of the family, in the order in which they were added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/></exception>
    IReadOnlyList<FontMetrics> GetAllMetrics(string name, CultureInfo culture);

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    IEnumerator<FontMetrics> GetEnumerator();
}
