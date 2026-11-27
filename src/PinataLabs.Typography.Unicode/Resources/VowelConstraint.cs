// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Unicode.Resources;

/// <summary>
/// A sequence of <c>IndicShapingInvalidCluster.txt</c> that HarfBuzz breaks with a dotted circle, so that an independent
/// vowel and the sign after it do not look like another vowel.
/// </summary>
/// <param name="CodePoints">The code points of the sequence.</param>
/// <param name="CircleIndex">The index of the code point that the dotted circle goes before.</param>
internal readonly record struct VowelConstraint(int[] CodePoints, int CircleIndex);
