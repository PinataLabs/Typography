// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// This is a shaper for Arabic, and other cursive scripts.
/// The shaping state machine was ported from fontkit.
/// <see href="https://github.com/foliojs/fontkit/blob/master/src/opentype/shapers/ArabicShaper.js"/>
/// </summary>
internal sealed class ArabicShaper : DefaultShaper
{
    private static readonly Tag MsetTag = Tag.Parse("mset");

    private static readonly Tag FinaTag = Tag.Parse("fina");

    private static readonly Tag Fin2Tag = Tag.Parse("fin2");

    private static readonly Tag Fin3Tag = Tag.Parse("fin3");

    private static readonly Tag IsolTag = Tag.Parse("isol");

    private static readonly Tag InitTag = Tag.Parse("init");

    private static readonly Tag MediTag = Tag.Parse("medi");

    private static readonly Tag Med2Tag = Tag.Parse("med2");

    // As in HarfBuzz, ZWJ means "do not join" in Arabic too, so its forms and its ligating features keep ZWJ.
    private static readonly IReadOnlyDictionary<Tag, ManualJoiners> ManualJoinerFeatures = WithCommonManualJoiners(
        ManualJoiners.Zwj, CcmpTag, LoclTag, IsolTag, FinaTag, Fin2Tag, Fin3Tag, MediTag, Med2Tag, InitTag, MsetTag, RligTag, CaltTag, LigaTag, CligTag);

    private const byte None = 0;

    private const byte Isol = 1;

    private const byte Fina = 2;

    private const byte Fin2 = 3;

    private const byte Fin3 = 4;

    private const byte Medi = 5;

    private const byte Med2 = 6;

    private const byte Init = 7;

    // Each entry is [prevAction, curAction, nextState]
    private static readonly byte[,][] StateTable =
    {
        // #           NonJoining,                    LeftJoining,                 RightJoining,                 DualJoining,                    ALAPH,                     DALATH RISH
        // State 0: prev was U,  not willing to join.
        { new byte[] { None, None, 0 }, new byte[] { None, Isol, 2 }, new byte[] { None, Isol, 1 }, new byte[] { None, Isol, 2 }, new byte[] { None, Isol, 1 }, new byte[] { None, Isol, 6 } },

        // State 1: prev was R or ISOL/ALAPH,  not willing to join.
        { new byte[] { None, None, 0 }, new byte[] { None, Isol, 2 }, new byte[] { None, Isol, 1 }, new byte[] { None, Isol, 2 }, new byte[] { None, Fin2, 5 }, new byte[] { None, Isol, 6 } },

        // State 2: prev was D/L in ISOL form,  willing to join.
        { new byte[] { None, None, 0 }, new byte[] { None, Isol, 2 }, new byte[] { Init, Fina, 1 }, new byte[] { Init, Fina, 3 }, new byte[] { Init, Fina, 4 }, new byte[] { Init, Fina, 6 } },

        // State 3: prev was D in FINA form,  willing to join.
        { new byte[] { None, None, 0 }, new byte[] { None, Isol, 2 }, new byte[] { Medi, Fina, 1 }, new byte[] { Medi, Fina, 3 }, new byte[] { Medi, Fina, 4 }, new byte[] { Medi, Fina, 6 } },

        // State 4: prev was FINA ALAPH,  not willing to join.
        { new byte[] { None, None, 0 }, new byte[] { None, Isol, 2 }, new byte[] { Med2, Isol, 1 }, new byte[] { Med2, Isol, 2 }, new byte[] { Med2, Fin2, 5 }, new byte[] { Med2, Isol, 6 } },

        // State 5: prev was FIN2/FIN3 ALAPH,  not willing to join.
        { new byte[] { None, None, 0 }, new byte[] { None, Isol, 2 }, new byte[] { Isol, Isol, 1 }, new byte[] { Isol, Isol, 2 }, new byte[] { Isol, Fin2, 5 }, new byte[] { Isol, Isol, 6 } },

        // State 6: prev was DALATH/RISH,  not willing to join.
        { new byte[] { None, None, 0 }, new byte[] { None, Isol, 2 }, new byte[] { None, Isol, 1 }, new byte[] { None, Isol, 2 }, new byte[] { None, Fin3, 5 }, new byte[] { None, Isol, 6 } },
    };

    public ArabicShaper(ScriptClass script, TextOptions textOptions)
        : base(script, MarkZeroingMode.PostGpos, textOptions, ManualJoinerFeatures)
    {
    }

    /// <inheritdoc/>
    protected override void PlanFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        // The directional features planned before these run to completion first.
        this.EndStage();

        this.AddFeature(collection, index, count, CcmpTag);
        this.AddFeature(collection, index, count, LoclTag);
        this.EndStage();

        // Each joining form is a stage of its own, and so is `rlig`: fonts rely on the
        // lookups of one form having all run before the next form is tried.
        ReadOnlySpan<Tag> joiningForms = [IsolTag, FinaTag, Fin2Tag, Fin3Tag, MediTag, Med2Tag, InitTag];
        foreach (Tag joiningForm in joiningForms)
        {
            this.AddFeature(collection, index, count, joiningForm, false);
            this.EndStage();
        }

        this.AddFeature(collection, index, count, MsetTag);
        this.EndStage();

        // In a font without the joining forms of Arabic, the presentation forms stand in for them after `rlig`.
        this.AddFeature(collection, index, count, RligTag, postAction: this.ScriptClass == ScriptClass.Arabic ? ArabicFallbackShaper.Apply : null);
        this.EndStage();

        // A count of zero places these features in a stage of their own without assigning them
        // to any glyph. The glyphs get them where the default shaper plans them.
        this.AddFeature(collection, index, 0, CaltTag);
        this.AddFeature(collection, index, 0, RcltTag);
        this.EndStage();
    }

    /// <inheritdoc/>
    protected override void AssignFeatures(IGlyphShapingCollection collection, int index, int count)
    {
        base.AssignFeatures(collection, index, count);
        AssignJoiningForms(collection, index, count);
    }

    /// <summary>
    /// Enables on each glyph the feature of the joining form that its letter takes between its neighbours.
    /// The glyphs must already have the features of the forms that the shaper applies.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="index">The zero-based index of the first glyph.</param>
    /// <param name="count">The number of glyphs.</param>
    internal static void AssignJoiningForms(IGlyphShapingCollection collection, int index, int count)
    {
        int prev = -1;
        int state = 0;
        byte[] actions = new byte[count];

        // As in HarfBuzz, the first character before the glyphs that is not transparent sets the state to start in.
        if (TryGetContextJoiningType(collection, index - 1, -1, out ArabicJoiningType before))
        {
            state = StateTable[0, GetShapingClassIndex(before)][2];
        }

        // Apply the state machine to map glyphs to features.
        for (int i = 0; i < count; i++)
        {
            GlyphShapingData data = collection[i + index];
            ArabicJoiningClass joiningClass = CodePoint.GetArabicJoiningClass(data.CodePoint);
            ArabicJoiningType joiningType = joiningClass.JoiningType;
            if (joiningType == ArabicJoiningType.Transparent)
            {
                actions[i] = None;
                continue;
            }

            int shapingClassIndex = GetShapingClassIndex(joiningType);
            byte[] actionsWithState = StateTable[state, shapingClassIndex];
            byte prevAction = actionsWithState[0];
            byte curAction = actionsWithState[1];
            state = actionsWithState[2];

            if (prevAction != None && prev != -1)
            {
                actions[prev] = prevAction;
            }

            actions[i] = curAction;
            prev = i;
        }

        // The first character after the glyphs that is not transparent can change the form of the last letter.
        if (prev != -1 && TryGetContextJoiningType(collection, index + count, 1, out ArabicJoiningType after))
        {
            byte prevAction = StateTable[state, GetShapingClassIndex(after)][0];
            if (prevAction != None)
            {
                actions[prev] = prevAction;
            }
        }

        // As in HarfBuzz, a Mongolian free variation selector takes the form of the glyph before it.
        // A lookup of that form can then match the letter and its selector.
        for (int i = 1; i < count; i++)
        {
            if (collection[i + index].CodePoint.Value is (>= 0x180B and <= 0x180D) or 0x180F)
            {
                actions[i] = actions[i - 1];
            }
        }

        // Apply the chosen features to their respective glyphs.
        for (int i = 0; i < actions.Length; i++)
        {
            switch (actions[i])
            {
                case Fina:
                    collection.EnableShapingFeature(i + index, FinaTag);
                    break;
                case Fin2:
                    collection.EnableShapingFeature(i + index, Fin2Tag);
                    break;
                case Fin3:
                    collection.EnableShapingFeature(i + index, Fin3Tag);
                    break;
                case Isol:
                    collection.EnableShapingFeature(i + index, IsolTag);
                    break;
                case Init:
                    collection.EnableShapingFeature(i + index, InitTag);
                    break;
                case Medi:
                    collection.EnableShapingFeature(i + index, MediTag);
                    break;
                case Med2:
                    collection.EnableShapingFeature(i + index, Med2Tag);
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the joining type of the first character that is not transparent, in the given direction from a glyph,
    /// among the glyphs of the collection and then the text around them. As in HarfBuzz, it looks at no more than
    /// five characters.
    /// </summary>
    /// <param name="collection">The glyph shaping collection.</param>
    /// <param name="start">The zero-based index of the glyph to start at.</param>
    /// <param name="step">-1 to look before the glyph, and 1 to look after it.</param>
    /// <param name="joiningType">The joining type that was found.</param>
    /// <returns><see langword="true"/> if a character that is not transparent was found.</returns>
    private static bool TryGetContextJoiningType(IGlyphShapingCollection collection, int start, int step, out ArabicJoiningType joiningType)
    {
        const int contextLength = 5;
        IReadOnlyList<CodePoint> context = collection is GlyphSubstitutionCollection substitutions
            ? step < 0 ? substitutions.PreContext : substitutions.PostContext
            : [];

        int looked = 0;
        for (int i = start; i >= 0 && i < collection.Count && looked < contextLength; i += step, looked++)
        {
            joiningType = CodePoint.GetArabicJoiningClass(collection[i].CodePoint).JoiningType;
            if (joiningType != ArabicJoiningType.Transparent)
            {
                return true;
            }
        }

        for (int i = 0; i < context.Count && looked < contextLength; i++, looked++)
        {
            joiningType = CodePoint.GetArabicJoiningClass(context[i]).JoiningType;
            if (joiningType != ArabicJoiningType.Transparent)
            {
                return true;
            }
        }

        joiningType = default;
        return false;
    }

    private static int GetShapingClassIndex(ArabicJoiningType joiningType) => joiningType switch
    {
        ArabicJoiningType.NonJoining => 0,
        ArabicJoiningType.LeftJoining => 1,
        ArabicJoiningType.RightJoining => 2,
        ArabicJoiningType.DualJoining or ArabicJoiningType.JoinCausing => 3,

        // TODO: ALAPH: 4
        // TODO: DALATH RISH': 5
        ArabicJoiningType.Transparent => 6,
        _ => 0,
    };
}
