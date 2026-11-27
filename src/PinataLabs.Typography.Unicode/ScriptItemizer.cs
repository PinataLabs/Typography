// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Splits text into runs of one script. <see href="https://unicode.org/reports/tr24/"/>
/// </summary>
/// <remarks>
/// <para>
/// Most punctuation, every space and every digit has the script Common, and a combining mark has the script
/// Inherited. Neither says which script the text is in, so each takes the script of the text before it. Text that
/// starts with them waits for the first character that has a script, and takes that one.
/// </para>
/// <para>
/// A character that no script owns, such as one in the Private Use Area or an unassigned code point, has the script
/// Unknown, and is treated in the same way. A private-use icon in the middle of a Latin word is in the Latin run, as
/// the font that draws the word is the font that draws the icon, and does not cut the word into three runs. Text
/// that has nothing but such characters is one run of script Unknown, and text that has such characters and Common
/// or Inherited characters but no letter is one run of script Common. The itemizer of PdfPinata that this replaces
/// makes a run of its own of the characters that no script owns.
/// </para>
/// <para>
/// A paired bracket is the exception. A closing bracket takes the script of the bracket that opened it, so
/// <c>a (б) c</c> is Latin, Cyrillic and then Latin again, and not Latin and then a Cyrillic run that holds the
/// bracket. The pairs are the paired brackets of the bidi algorithm. A closing bracket without an opening bracket
/// before it takes the script of the text before it.
/// </para>
/// <para>
/// This reads the Script property and not Script_Extensions. A character that several scripts use, such as
/// U+0640 ARABIC TATWEEL, takes the script it is named for.
/// </para>
/// </remarks>
internal static class ScriptItemizer
{
    private const int MaxBracketDepth = 128;

    /// <summary>
    /// Gets the runs of one window of the text in logical order, with indices into the whole text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index of the first code unit of the window.</param>
    /// <param name="length">The number of code units in the window.</param>
    /// <returns>The runs, which cover the window.</returns>
    public static List<ScriptRun> Itemize(ReadOnlySpan<char> text, int start, int length)
    {
        var runs = new List<ScriptRun>();
        if (length == 0)
        {
            return runs;
        }

        // Decoding stops at the end of the window, so one that ends between the halves of a surrogate pair
        // does not read past its end.
        int end = start + length;
        ReadOnlySpan<char> window = text[..end];

        List<OpenBracket>? brackets = null;
        ScriptClass script = ScriptClass.Common;
        bool onlyUnknown = true;
        int from = start;
        for (int index = start; index < end;)
        {
            CodePoint codePoint = CodePoint.DecodeFromUtf16At(window, index, out int width);
            ScriptClass codePointScript = CodePoint.GetScriptClass(codePoint);
            if (codePointScript == ScriptClass.Common)
            {
                codePointScript = ResolveBracket(codePoint, script, ref brackets);
            }

            if (codePointScript != ScriptClass.Unknown)
            {
                onlyUnknown = false;
            }

            if (codePointScript is not (ScriptClass.Common or ScriptClass.Inherited or ScriptClass.Unknown))
            {
                if (script == ScriptClass.Common)
                {
                    script = codePointScript;
                    ResolveOpenBrackets(brackets, script);
                }
                else if (codePointScript != script)
                {
                    runs.Add(new ScriptRun(from, index - from, script));
                    from = index;
                    script = codePointScript;
                }
            }

            index += width;
        }

        if (script == ScriptClass.Common && onlyUnknown)
        {
            script = ScriptClass.Unknown;
        }

        runs.Add(new ScriptRun(from, end - from, script));
        return runs;
    }

    /// <summary>
    /// Pushes an opening bracket with the script of the text it is in, and pops the pair that a closing bracket closes.
    /// </summary>
    /// <returns>
    /// The script of the opening bracket for a closing bracket that has one, and <see cref="ScriptClass.Common"/>
    /// for any other character.
    /// </returns>
    private static ScriptClass ResolveBracket(CodePoint codePoint, ScriptClass script, ref List<OpenBracket>? brackets)
    {
        BidiClass bidiClass = CodePoint.GetBidiClass(codePoint);
        switch (bidiClass.PairedBracketType)
        {
            case BidiPairedBracketType.Open:
                brackets ??= [];
                if (brackets.Count == MaxBracketDepth)
                {
                    brackets.RemoveAt(0);
                }

                brackets.Add(new OpenBracket(bidiClass.GetBracketPairKey(codePoint), script));
                return ScriptClass.Common;

            case BidiPairedBracketType.Close when brackets is not null:
                int key = bidiClass.GetBracketPairKey(codePoint);
                for (int i = brackets.Count - 1; i >= 0; i--)
                {
                    if (brackets[i].Key == key)
                    {
                        ScriptClass openedIn = brackets[i].Script;
                        brackets.RemoveRange(i, brackets.Count - i);
                        return openedIn;
                    }
                }

                return ScriptClass.Common;

            default:
                return ScriptClass.Common;
        }
    }

    /// <summary>
    /// Gives the brackets that opened before the first character with a script that script.
    /// </summary>
    private static void ResolveOpenBrackets(List<OpenBracket>? brackets, ScriptClass script)
    {
        if (brackets is null)
        {
            return;
        }

        for (int i = 0; i < brackets.Count; i++)
        {
            if (brackets[i].Script == ScriptClass.Common)
            {
                brackets[i] = new OpenBracket(brackets[i].Key, script);
            }
        }
    }

    private readonly record struct OpenBracket(int Key, ScriptClass Script);
}
