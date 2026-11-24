// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The features of a shaping stage that reference one lookup.
/// The lookup applies to a glyph that has any of them enabled.
/// </summary>
internal readonly struct LookupFeatures
{
    private readonly Tag[]? tags;
    private readonly int offset;
    private readonly int count;

    public LookupFeatures(Tag[] tags, int offset, int count)
    {
        this.tags = tags;
        this.offset = offset;
        this.count = count;
    }

    public bool AnyEnabledOn(List<TagEntry> glyphFeatures)
    {
        for (int i = 0; i < glyphFeatures.Count; i++)
        {
            TagEntry entry = glyphFeatures[i];
            if (!entry.Enabled)
            {
                continue;
            }

            for (int j = 0; j < this.count; j++)
            {
                if (this.tags![this.offset + j] == entry.Tag)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
