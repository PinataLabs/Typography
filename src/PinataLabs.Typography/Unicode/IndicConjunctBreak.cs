// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// The Indic_Conjunct_Break property, which rule GB9c of UAX #29 uses.
/// <see href="https://www.unicode.org/reports/tr44/#Indic_Conjunct_Break"/>
/// </summary>
internal enum IndicConjunctBreak
{
    /// <summary>
    /// None
    /// </summary>
    None = 0,

    /// <summary>
    /// Linker
    /// </summary>
    Linker = 1,

    /// <summary>
    /// Consonant
    /// </summary>
    Consonant = 2,

    /// <summary>
    /// Extend
    /// </summary>
    Extend = 3,
}
