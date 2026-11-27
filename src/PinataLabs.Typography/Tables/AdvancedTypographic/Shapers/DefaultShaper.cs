// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Default shaper, which will be applied to all glyphs.
/// Based on fontkit: <see href="https://github.com/foliojs/fontkit/blob/master/src/opentype/shapers/DefaultShaper.js"/>
/// </summary>
internal class DefaultShaper : BaseShaper
{
    protected static readonly Tag RvnrTag = Tag.Parse("rvrn");

    protected static readonly Tag LtraTag = Tag.Parse("ltra");

    protected static readonly Tag LtrmTag = Tag.Parse("ltrm");

    protected static readonly Tag RtlaTag = Tag.Parse("rtla");

    protected static readonly Tag RtlmTag = Tag.Parse("rtlm");

    protected static readonly Tag FracTag = Tag.Parse("frac");

    protected static readonly Tag NumrTag = Tag.Parse("numr");

    protected static readonly Tag DnomTag = Tag.Parse("dnom");

    protected static readonly Tag CcmpTag = Tag.Parse("ccmp");

    protected static readonly Tag LoclTag = Tag.Parse("locl");

    protected static readonly Tag RligTag = Tag.Parse("rlig");

    protected static readonly Tag MarkTag = Tag.Parse("mark");

    protected static readonly Tag MkmkTag = Tag.Parse("mkmk");

    protected static readonly Tag CaltTag = Tag.Parse("calt");

    protected static readonly Tag CligTag = Tag.Parse("clig");

    protected static readonly Tag LigaTag = Tag.Parse("liga");

    protected static readonly Tag RcltTag = Tag.Parse("rclt");

    protected static readonly Tag CursTag = Tag.Parse("curs");

    protected static readonly Tag KernTag = Tag.Parse("kern");

    protected static readonly Tag DistTag = Tag.Parse("dist");

    protected static readonly Tag AbvmTag = Tag.Parse("abvm");

    protected static readonly Tag BlwmTag = Tag.Parse("blwm");

    protected static readonly Tag VertTag = Tag.Parse("vert");

    protected static readonly Tag VKernTag = Tag.Parse("vkrn");

    protected static readonly Tag VrtrTag = Tag.Parse("vrtr");

    protected static readonly Tag Vrt2Tag = Tag.Parse("vrt2");

    private static readonly CodePoint FractionSlash = new(0x2044);

    private static readonly CodePoint Slash = new(0x002F);

    private static readonly IReadOnlyDictionary<Tag, ManualJoiners> CommonManualJoiners = WithCommonManualJoiners();

    private static readonly Dictionary<Tag, bool> NoFeatureOverrides = [];

    private readonly List<ShapingStage> shapingStages = [];

    private readonly HashSet<Tag> plannedFeatures = [];

    private ShapingStage? openStage;

    private readonly KerningMode kerningMode;

    private readonly IReadOnlyList<Tag> featureTags;

    private readonly IReadOnlyDictionary<Tag, ManualJoiners> manualJoiners;

    private readonly IReadOnlyDictionary<Tag, bool> featureOverrides;

    internal DefaultShaper(ScriptClass script, TextOptions textOptions)
        : this(script, MarkZeroingMode.PostGpos, textOptions)
    {
    }

    protected DefaultShaper(ScriptClass script, MarkZeroingMode markZeroingMode, TextOptions textOptions)
        : this(script, markZeroingMode, textOptions, CommonManualJoiners)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShaper"/> class.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="markZeroingMode">When the advances of marks are zeroed.</param>
    /// <param name="textOptions">The text options.</param>
    /// <param name="manualJoiners">
    /// The joiners that the lookups of each feature do not skip, by the tag of the feature. A feature that it does not
    /// list skips them wherever HarfBuzz does by default. Make it with <see cref="WithCommonManualJoiners"/>.
    /// </param>
    /// <param name="featureOverrides">
    /// The features that the shaper turns on (<see langword="true"/>) or off (<see langword="false"/>) whatever the
    /// caller asks for, as the override_features function of a HarfBuzz shaper does. A feature turned on applies to
    /// every glyph, in horizontal and in vertical text.
    /// </param>
    protected DefaultShaper(
        ScriptClass script,
        MarkZeroingMode markZeroingMode,
        TextOptions textOptions,
        IReadOnlyDictionary<Tag, ManualJoiners> manualJoiners,
        IReadOnlyDictionary<Tag, bool>? featureOverrides = null)
    {
        this.ScriptClass = script;
        this.MarkZeroingMode = markZeroingMode;
        this.kerningMode = textOptions.KerningMode;
        this.featureTags = textOptions.FeatureTags;
        this.manualJoiners = manualJoiners;
        this.featureOverrides = featureOverrides ?? NoFeatureOverrides;
        this.DisabledFeatureTags = this.featureOverrides.Count == 0
            ? textOptions.DisabledFeatureTags
            : GetDisabledFeatureTags(textOptions.DisabledFeatureTags, this.featureOverrides);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<Tag> DisabledFeatureTags { get; }

    protected override void PlanFeatures(IGlyphShapingCollection collection, int index, int count)
    {
    }

    protected override void PlanPreprocessingFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // Add variation Features. They choose the glyphs that every later lookup sees.
        this.AddFeature(collection, index, count, RvnrTag);
        this.EndStage();

        // Add directional features.
        for (int i = index; i < index + count; i++)
        {
            GlyphShapingData shapingData = collection[i];

            if (shapingData.Direction == TextDirection.LeftToRight)
            {
                this.AddFeature(collection, i, 1, LtraTag);
                this.AddFeature(collection, i, 1, LtrmTag);
            }
            else
            {
                this.AddFeature(collection, i, 1, RtlaTag);
                this.AddFeature(collection, i, 1, RtlmTag);
            }
        }

        // TODO: Fractional feature should be assigned here but disabled.
        // They should then be enabled in AssignFeatures.
    }

    protected override void PlanPostprocessingFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // Add common features.
        this.AddFeature(collection, index, count, CcmpTag);
        this.AddFeature(collection, index, count, LoclTag);
        this.AddFeature(collection, index, count, RligTag);
        this.AddFeature(collection, index, count, MarkTag);
        this.AddFeature(collection, index, count, MkmkTag);

        // Every shaper of HarfBuzz applies these two, in horizontal and in vertical text, so a font whose only
        // script is DFLT can place the marks of a script that has a shaper of its own.
        this.AddFeature(collection, index, count, AbvmTag);
        this.AddFeature(collection, index, count, BlwmTag);

        // Add horizontal or vertical features. The choice is made per glyph so that, in vertical mixed
        // layouts, rotated glyphs keep their horizontal features while upright glyphs get `vert`.
        LayoutMode layoutMode = collection.TextOptions.LayoutMode;
        for (int i = index; i < index + count; i++)
        {
            if (!AdvancedTypographicUtils.IsVerticalGlyph(collection[i].CodePoint, layoutMode, collection[i].HasVerticalForm))
            {
                // Add horizontal features.
                this.AddFeature(collection, i, 1, CaltTag);
                this.AddFeature(collection, i, 1, CligTag);
                this.AddFeature(collection, i, 1, LigaTag);
                this.AddFeature(collection, i, 1, RcltTag);
                this.AddFeature(collection, i, 1, CursTag);
                this.AddFeature(collection, i, 1, KernTag);

                // Every shaper of HarfBuzz applies dist too, to horizontal text only.
                this.AddFeature(collection, i, 1, DistTag);

                // Rotated glyphs in vertical mixed layout get `vrtr`. Its glyphs are designed to be
                // rotated by the layout engine, so they are still rotated synthetically. See:
                // https://learn.microsoft.com/en-us/typography/opentype/spec/features_uz#tag-vrtr
                if (layoutMode.IsVerticalMixed())
                {
                    this.AddFeature(collection, i, 1, VrtrTag);
                }
            }
            else
            {
                // We only apply `vert` feature.See:
                // https://github.com/harfbuzz/harfbuzz/commit/d71c0df2d17f4590d5611239577a6cb532c26528
                // https://lists.freedesktop.org/archives/harfbuzz/2013-August/003490.html

                // We really want to find a 'vert' feature if there's any in the font, no
                // matter which script/langsys it is listed (or not) under.
                // See various bugs referenced from:
                // https://github.com/harfbuzz/harfbuzz/issues/63
                this.AddFeature(collection, i, 1, VertTag);
            }
        }

        // Add user defined features.
        foreach (Tag feature in this.featureTags)
        {
            // We've already dealt with fractional features.
            if (feature == FracTag || feature == NumrTag || feature == DnomTag)
            {
                continue;
            }

            // `vrt2` supplies pre-rotated glyphs. Glyphs that are rotated synthetically in vertical
            // mixed layout must not get it, or they would be rotated twice.
            if (feature == Vrt2Tag && layoutMode.IsVerticalMixed())
            {
                for (int i = index; i < index + count; i++)
                {
                    if (AdvancedTypographicUtils.IsVerticalGlyph(collection[i].CodePoint, layoutMode, collection[i].HasVerticalForm))
                    {
                        this.AddFeature(collection, i, 1, feature);
                    }
                }

                continue;
            }

            this.AddFeature(collection, index, count, feature);
        }

        foreach (KeyValuePair<Tag, bool> feature in this.featureOverrides)
        {
            if (feature.Value)
            {
                this.AddFeature(collection, index, count, feature.Key);
            }
        }
    }

    /// <inheritdoc />
    protected override void AssignFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // TODO: We shouldn't be relying on the feature list
        // User defined fractional features require special treatment.
        // https://docs.microsoft.com/en-us/typography/opentype/spec/features_fj#tag-frac
        if (this.HasFractions())
        {
            this.AssignFractionalFeatures(collection, index, count);
        }
    }

    /// <summary>
    /// Replaces each vowel sign of the range that has parts with the glyphs of its parts, as HarfBuzz decomposes such a
    /// sign when it normalizes the text. As there, a sign that the font has stays whole when the font lacks one of its
    /// parts. A sign that the font lacks is split all the same, where HarfBuzz keeps one .notdef, so that a fallback font
    /// draws each part where the shaper puts it, the part in front of the base before the base.
    /// </summary>
    /// <param name="collection">The glyphs of the text.</param>
    /// <param name="index">The zero-based index of the first glyph of the range.</param>
    /// <param name="count">The number of glyphs in the range.</param>
    /// <param name="decompositions">The parts of each vowel sign, the part in front of the base first.</param>
    protected static void SplitVowelSigns(IGlyphShapingCollection collection, int index, int count, Dictionary<int, int[]> decompositions)
    {
        if (collection is not GlyphSubstitutionCollection substitutionCollection)
        {
            return;
        }

        Span<ushort> ids = stackalloc ushort[16];
        for (int i = index + count - 1; i >= index; i--)
        {
            GlyphShapingData data = substitutionCollection[i];
            if (!decompositions.TryGetValue(data.CodePoint.Value, out int[]? parts))
            {
                continue;
            }

            FontMetrics fontMetrics = substitutionCollection.GetFontMetrics(data);
            bool hasParts = true;
            for (int j = 0; j < parts.Length; j++)
            {
                hasParts &= fontMetrics.TryGetGlyphId(new CodePoint(parts[j]), out ids[j]) && ids[j] != 0;
            }

            if (!hasParts && data.GlyphId != 0)
            {
                continue;
            }

            substitutionCollection.Replace(i, ids.Slice(0, parts.Length));
            for (int j = 0; j < parts.Length; j++)
            {
                substitutionCollection[i + j].CodePoint = new(parts[j]);
            }
        }
    }

    /// <summary>
    /// Plans a feature in the open stage and assigns it to the glyphs that are not upright in vertical text, as
    /// HarfBuzz applies <c>dist</c> to horizontal text only.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the first glyph.</param>
    /// <param name="count">The number of glyphs.</param>
    /// <param name="feature">The feature to assign.</param>
    protected void AddHorizontalFeature(IGlyphShapingCollection collection, int index, int count, Tag feature)
    {
        // A count of zero plans the feature in the open stage for a run that has only upright glyphs.
        this.AddFeature(collection, index, 0, feature);

        LayoutMode layoutMode = collection.TextOptions.LayoutMode;
        for (int i = index; i < index + count; i++)
        {
            if (!AdvancedTypographicUtils.IsVerticalGlyph(collection[i].CodePoint, layoutMode, collection[i].HasVerticalForm))
            {
                this.AddFeature(collection, i, 1, feature);
            }
        }
    }

    protected void AddFeature(
        IGlyphShapingCollection collection,
        int index,
        int count,
        Tag feature,
        bool enabled = true,
        Action<IGlyphShapingCollection, int, int>? preAction = null,
        Action<IGlyphShapingCollection, int, int>? postAction = null)
    {
        if (this.kerningMode == KerningMode.None)
        {
            if (feature == KernTag || feature == VKernTag)
            {
                return;
            }
        }

        int end = index + count;
        for (int i = index; i < end; i++)
        {
            collection.AddShapingFeature(i, new TagEntry(feature, enabled));
        }

        // A feature stays in the stage that first planned it.
        if (!this.plannedFeatures.Add(feature))
        {
            return;
        }

        if (this.openStage is null || preAction is not null)
        {
            this.openStage = new ShapingStage(preAction);
            this.shapingStages.Add(this.openStage);
        }

        this.openStage.AddFeature(this.Plan(feature));

        if (postAction is not null)
        {
            this.openStage.SetPostAction(postAction);
            this.openStage = null;
        }
    }

    /// <summary>
    /// Ends the current shaping stage. The features added next start a new stage,
    /// so their lookups are applied after all lookups of the features added before.
    /// </summary>
    protected void EndStage() => this.openStage = null;

    /// <summary>
    /// Gets a feature as the shaper plans it, with the joiners that its lookups do not skip.
    /// </summary>
    /// <param name="feature">The tag of the feature.</param>
    /// <returns>The planned feature.</returns>
    protected PlannedFeature Plan(Tag feature)
        => new(feature, this.manualJoiners.TryGetValue(feature, out ManualJoiners joiners) ? joiners : ManualJoiners.None, this.IsPerSyllable(feature));

    /// <summary>
    /// Gets a value indicating whether the GSUB lookups of a feature match only glyphs of the syllable of the glyph that
    /// they apply to.
    /// </summary>
    /// <param name="feature">The tag of the feature.</param>
    /// <returns><see langword="true"/> if the lookups match per syllable.</returns>
    protected virtual bool IsPerSyllable(Tag feature) => false;

    /// <summary>
    /// Gets the joiners that the lookups of each feature do not skip: the given joiners for the given features, and
    /// both joiners for mark and mkmk, as every shaper of HarfBuzz plans them.
    /// </summary>
    /// <param name="joiners">The joiners that the lookups of the features do not skip.</param>
    /// <param name="features">The features.</param>
    /// <returns>The joiners by the tag of the feature.</returns>
    protected static IReadOnlyDictionary<Tag, ManualJoiners> WithCommonManualJoiners(ManualJoiners joiners = ManualJoiners.None, params Tag[] features)
    {
        Dictionary<Tag, ManualJoiners> map = new() { [MarkTag] = ManualJoiners.Both, [MkmkTag] = ManualJoiners.Both };
        foreach (Tag feature in features)
        {
            map[feature] = joiners;
        }

        return map;
    }

    public override IReadOnlyList<ShapingStage> GetShapingStages() => this.shapingStages;

    // HarfBuzz overrides the features after it adds those of the caller, so the overrides win.
    private static List<Tag> GetDisabledFeatureTags(IReadOnlyList<Tag> disabledByCaller, IReadOnlyDictionary<Tag, bool> featureOverrides)
    {
        List<Tag> disabled = [];
        foreach (Tag feature in disabledByCaller)
        {
            if (!featureOverrides.ContainsKey(feature))
            {
                disabled.Add(feature);
            }
        }

        foreach (KeyValuePair<Tag, bool> feature in featureOverrides)
        {
            if (!feature.Value)
            {
                disabled.Add(feature.Key);
            }
        }

        return disabled;
    }

    private void AssignFractionalFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // Enable contextual fractions.
        for (int i = index; i < index + count; i++)
        {
            GlyphShapingData shapingData = collection[i];
            if (shapingData.CodePoint == FractionSlash || shapingData.CodePoint == Slash)
            {
                int start = i;
                int end = i + 1;

                // Apply numerator.
                if (start > 0)
                {
                    shapingData = collection[start - 1];
                    while (start > 0 && CodePoint.IsDigit(shapingData.CodePoint))
                    {
                        this.AddFeature(collection, start - 1, 1, NumrTag);
                        this.AddFeature(collection, start - 1, 1, FracTag);
                        start--;
                    }
                }

                // Apply denominator.
                if (end < collection.Count)
                {
                    shapingData = collection[end];
                    while (end < collection.Count && CodePoint.IsDigit(shapingData.CodePoint))
                    {
                        this.AddFeature(collection, end, 1, DnomTag);
                        this.AddFeature(collection, end, 1, FracTag);
                        end++;
                    }
                }

                // Apply fraction slash.
                this.AddFeature(collection, i, 1, FracTag);
                i = end - 1;
            }
        }
    }

    private bool HasFractions()
    {
        bool hasNmr = false;
        bool hasDnom = false;

        // My kingdom for a binary search on IReadOnlyList
        for (int i = 0; i < this.featureTags.Count; i++)
        {
            Tag feature = this.featureTags[i];
            if (feature == FracTag)
            {
                return true;
            }

            if (feature == DnomTag)
            {
                hasDnom = true;
            }

            if (feature == NumrTag)
            {
                hasNmr = true;
            }

            if (hasDnom && hasNmr)
            {
                return true;
            }
        }

        return false;
    }
}
