// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// Splits the glyphs of a collection into the runs that a caller of HarfBuzz shapes in a buffer each: runs of one script,
/// one direction, in vertical mixed text one orientation, and, for positioning, one font. A character of the scripts
/// Common, Inherited or Unknown joins the script before it, or the script after it at the start of the text. A caller
/// itemizes text by script first, so each piece of a script run keeps the script of the run, also a piece of digits that
/// runs the other way or of characters that a fallback font draws.
/// </summary>
/// <remarks>
/// <para>
/// Every glyph of a grapheme cluster keeps the orientation of the first glyph of the cluster, as in CSS: a mark that of
/// its base, and the ZWJ and the emoji of an emoji ZWJ sequence that of the first emoji. The grapheme clusters are those
/// of <see cref="GlyphShapingData.Grapheme"/>, as font fallback takes them too. GSUB gives each glyph that orientation,
/// <see cref="GlyphShapingData.IsInUprightRun"/>, before it shapes the runs, so a mark after a ligature of two grapheme
/// clusters, a glyph that a shaper moves, and a mark of a fallback font keep the orientation of their grapheme cluster.
/// A glyph of another font ends a run, also a default-ignorable one.
/// </para>
/// <para>
/// The end of a run is kept as the number of glyphs after it, as <see cref="IGlyphShapingCollection.RunEnd"/> is, so a
/// substitution that adds or removes glyphs of the current run moves it, and the next run starts after the glyphs that
/// the substitution left.
/// </para>
/// </remarks>
internal struct ShapingRunEnumerator
{
    private readonly IGlyphShapingCollection collection;
    private readonly GlyphPositioningCollection? fonts;
    private int glyphsAfterRun;
    private int glyphsAfterScriptRun;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShapingRunEnumerator"/> struct, before the first run.
    /// </summary>
    /// <param name="collection">The glyphs to split.</param>
    public ShapingRunEnumerator(IGlyphShapingCollection collection)
        : this(collection, null)
    {
    }

    private ShapingRunEnumerator(IGlyphShapingCollection collection, GlyphPositioningCollection? fonts)
    {
        this.collection = collection;
        this.fonts = fonts;
        this.glyphsAfterRun = collection.Count;
        this.glyphsAfterScriptRun = collection.Count;
    }

    /// <summary>
    /// Gets the index of the first glyph of the run.
    /// </summary>
    public int Start { get; private set; }

    /// <summary>
    /// Gets the index after the last glyph of the run.
    /// </summary>
    public readonly int End => this.collection.Count - this.glyphsAfterRun;

    /// <summary>
    /// Gets the number of glyphs of the run.
    /// </summary>
    public readonly int Count => this.End - this.Start;

    /// <summary>
    /// Gets the script of the run.
    /// </summary>
    public ScriptClass Script { get; private set; }

    /// <summary>
    /// Gets the runs that a font positions: each run also has the glyphs of one font. HarfBuzz shapes the glyphs of each
    /// font in a buffer of their own, so a lookup of one font does not see the glyphs of another.
    /// </summary>
    /// <param name="collection">The glyphs to split.</param>
    /// <returns>The enumerator, before the first run.</returns>
    public static ShapingRunEnumerator ByFont(GlyphPositioningCollection collection) => new(collection, collection);

    /// <summary>
    /// Moves to the run after the current one.
    /// </summary>
    /// <returns><see langword="true"/> if there is a run after the current one.</returns>
    public bool MoveNext()
    {
        int start = this.End;
        if (start >= this.collection.Count)
        {
            return false;
        }

        if (start >= this.collection.Count - this.glyphsAfterScriptRun)
        {
            this.glyphsAfterScriptRun = this.collection.Count - this.GetScriptRunEnd(start);
        }

        this.Start = start;
        this.glyphsAfterRun = this.collection.Count - this.GetRunEnd(start, this.collection.Count - this.glyphsAfterScriptRun);
        return true;
    }

    private readonly int GetRunEnd(int start, int scriptRunEnd)
    {
        GlyphShapingData first = this.collection[start];
        FontMetrics? font = this.fonts?.GetMetrics(start).FontMetrics;
        int end = start + 1;
        while (end < scriptRunEnd)
        {
            GlyphShapingData data = this.collection[end];
            if (data.Direction != first.Direction
                || data.IsInUprightRun != first.IsInUprightRun
                || (font is not null && this.fonts!.GetMetrics(end).FontMetrics != font))
            {
                break;
            }

            end++;
        }

        return end;
    }

    private int GetScriptRunEnd(int start)
    {
        TextOptions options = this.collection.TextOptions;
        ScriptClass script = options.GetScriptClass(this.collection[start].CodePoint);
        int end = start + 1;
        while (end < this.collection.Count)
        {
            ScriptClass next = options.GetScriptClass(this.collection[end].CodePoint);
            if (next != script && HasOwnScript(script) && HasOwnScript(next))
            {
                break;
            }

            if (!HasOwnScript(script))
            {
                script = next;
            }

            end++;
        }

        this.Script = script;
        return end;
    }

    private static bool HasOwnScript(ScriptClass script)
        => script is not ScriptClass.Common and not ScriptClass.Unknown and not ScriptClass.Inherited;
}
