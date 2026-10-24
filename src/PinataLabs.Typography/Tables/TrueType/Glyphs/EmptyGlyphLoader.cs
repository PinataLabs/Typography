// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PinataLabs.Typography.Tables.TrueType.Glyphs;

internal class EmptyGlyphLoader : GlyphLoader
{
    // The loader that is resolving glyph 0 on the current thread. Glyph 0 can itself be empty,
    // and so resolve back through this loader; that nested call must use the fallback bounds.
    [ThreadStatic]
    private static EmptyGlyphLoader? resolving;

    private readonly Bounds fallbackEmptyBounds;

    // Boxed so the cached glyph is published with a single reference write.
    // A GlyphVector? field can be read torn by another thread.
    private StrongBox<GlyphVector>? glyph;

    public EmptyGlyphLoader(Bounds fallbackEmptyBounds)
        => this.fallbackEmptyBounds = fallbackEmptyBounds;

    public override GlyphVector CreateGlyph(GlyphTable table)
    {
        StrongBox<GlyphVector>? cached = Volatile.Read(ref this.glyph);
        if (cached is not null)
        {
            return cached.Value;
        }

        if (resolving == this)
        {
            return GlyphVector.Empty(this.fallbackEmptyBounds);
        }

        EmptyGlyphLoader? previous = resolving;
        resolving = this;
        Bounds bounds;
        try
        {
            bounds = table.GetGlyph(0).Bounds;
        }
        finally
        {
            resolving = previous;
        }

        StrongBox<GlyphVector> created = new(GlyphVector.Empty(bounds));
        return (Interlocked.CompareExchange(ref this.glyph, created, null) ?? created).Value;
    }
}
