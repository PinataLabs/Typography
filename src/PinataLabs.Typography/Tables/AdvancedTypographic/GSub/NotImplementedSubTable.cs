// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GSub;

internal class NotImplementedSubTable : LookupSubTable
{
    public NotImplementedSubTable()
        : base(default)
    {
    }

    public override bool TrySubstitution(
        FontMetrics fontMetrics,
        GSubTable table,
        GlyphSubstitutionCollection collection,
        Tag feature,
        int index,
        int count)
        => false;
}
