// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GPos
{
    internal class NotImplementedSubTable : LookupSubTable
    {
        public NotImplementedSubTable()
            : base(default)
        {
        }

        public override bool TryUpdatePosition(
            FontMetrics fontMetrics,
            GPosTable table,
            GlyphPositioningCollection collection,
            Tag feature,
            int index,
            int count)
            => false;
    }
}
