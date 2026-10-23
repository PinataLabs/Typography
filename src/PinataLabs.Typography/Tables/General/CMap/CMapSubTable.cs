// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.WellKnownIds;

namespace PinataLabs.Typography.Tables.General.CMap;

internal abstract class CMapSubTable
{
    public CMapSubTable()
    {
    }

    public CMapSubTable(PlatformIDs platform, ushort encoding, ushort format)
    {
        this.Platform = platform;
        this.Encoding = encoding;
        this.Format = format;
    }

    public ushort Format { get; }

    public PlatformIDs Platform { get; }

    public ushort Encoding { get; }

    public abstract bool TryGetGlyphId(CodePoint codePoint, out ushort glyphId);

    public abstract IEnumerable<int> GetAvailableCodePoints();
}
