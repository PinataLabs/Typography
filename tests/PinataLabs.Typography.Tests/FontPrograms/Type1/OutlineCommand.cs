// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Linq;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

/// <summary>
/// One path command: M, L, Q, C or Z, with its coordinates.
/// </summary>
internal readonly record struct OutlineCommand(char Operator, float[] Coordinates)
{
    public override string ToString()
        => string.Join(" ", this.Coordinates.Select(x => x.ToString(CultureInfo.InvariantCulture)).Prepend(this.Operator.ToString()));
}
