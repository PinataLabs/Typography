// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Unicode
{
    public partial class CodePointTests
    {
        [Test]
        [MethodDataSource(nameof(GeneralTestData_BmpCodePoints_NoSurrogates))]
        public async Task Ctor_Cast_Char_Valid(GeneralTestData testData)
        {
            var codePoint = new CodePoint(checked((char)testData.ScalarValue));
            var codePointFromCast = (CodePoint)(char)testData.ScalarValue;

            await Assert.That(codePointFromCast).IsEqualTo(codePoint);
            await Assert.That(codePoint.Value).IsEqualTo(testData.ScalarValue);
            await Assert.That(codePoint.IsAscii).IsEqualTo(testData.IsAscii);
            await Assert.That(codePoint.IsBmp).IsEqualTo(testData.IsBmp);
            await Assert.That(codePoint.Plane).IsEqualTo(testData.Plane);
        }

        [Test]
        [MethodDataSource(nameof(BmpCodePoints_SurrogatesOnly))]
        public async Task Ctor_Cast_Char_Invalid_Throws(char value)
        {
            await Assert.That(() => new CodePoint(value)).ThrowsExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
            await Assert.That(() => (CodePoint)value).ThrowsExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
        }

        [Test]
        [MethodDataSource(nameof(GeneralTestData_BmpCodePoints_NoSurrogates))]
        [MethodDataSource(nameof(GeneralTestData_SupplementaryCodePoints_ValidOnly))]
        public async Task Ctor_Cast_Int32_Valid(GeneralTestData testData)
        {
            var codePoint = new CodePoint(testData.ScalarValue);
            var codePointFromCast = (CodePoint)testData.ScalarValue;

            await Assert.That(codePointFromCast).IsEqualTo(codePoint);
            await Assert.That(codePoint.Value).IsEqualTo(testData.ScalarValue);
            await Assert.That(codePoint.IsAscii).IsEqualTo(testData.IsAscii);
            await Assert.That(codePoint.IsBmp).IsEqualTo(testData.IsBmp);
            await Assert.That(codePoint.Plane).IsEqualTo(testData.Plane);
        }

        [Test]
        [MethodDataSource(nameof(SupplementaryCodePoints_InvalidOnly))]
        public async Task Ctor_Cast_Int32_Invalid_Throws(int value)
        {
            await Assert.That(() => new CodePoint(value)).ThrowsExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
            await Assert.That(() => (CodePoint)value).ThrowsExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
        }

        [Test]
        [MethodDataSource(nameof(GeneralTestData_BmpCodePoints_NoSurrogates))]
        [MethodDataSource(nameof(GeneralTestData_SupplementaryCodePoints_ValidOnly))]
        public async Task Ctor_Cast_UInt32_Valid(GeneralTestData testData)
        {
            var codePoint = new CodePoint((uint)testData.ScalarValue);
            var codePointFromCast = (CodePoint)(uint)testData.ScalarValue;

            await Assert.That(codePointFromCast).IsEqualTo(codePoint);
            await Assert.That(codePoint.Value).IsEqualTo(testData.ScalarValue);
            await Assert.That(codePoint.IsAscii).IsEqualTo(testData.IsAscii);
            await Assert.That(codePoint.IsBmp).IsEqualTo(testData.IsBmp);
            await Assert.That(codePoint.Plane).IsEqualTo(testData.Plane);
        }

        [Test]
        [MethodDataSource(nameof(SupplementaryCodePoints_InvalidOnly))]
        public async Task Ctor_Cast_UInt32_Invalid_Throws(int value)
        {
            await Assert.That(() => new CodePoint((uint)value)).ThrowsExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
            await Assert.That(() => (CodePoint)(uint)value).ThrowsExactly<ArgumentOutOfRangeException>().WithParameterName(nameof(value));
        }

        [Test]
        [MethodDataSource(nameof(SurrogatePairTestData_ValidOnly))]
        public async Task Ctor_SurrogatePair_Valid(char highSurrogate, char lowSurrogate, int expectedValue)
        {
            await Assert.That(new CodePoint(highSurrogate, lowSurrogate).Value).IsEqualTo(expectedValue);
        }

        [Test]
        [MethodDataSource(nameof(SurrogatePairTestData_InvalidOnly))]
        public async Task Ctor_SurrogatePair_Invalid(char highSurrogate, char lowSurrogate)
        {
            string expectedParamName = !char.IsHighSurrogate(highSurrogate) ? nameof(highSurrogate) : nameof(lowSurrogate);
            await Assert.That(() => new CodePoint(highSurrogate, lowSurrogate)).ThrowsExactly<ArgumentOutOfRangeException>().WithParameterName(expectedParamName);
        }

        [Test]
        public async Task CanEnumerateSpan()
        {
            // Test example taken from.
            // https://docs.microsoft.com/en-us/dotnet/api/system.text.rune?view=net-5.0#when-to-use-the-rune-type
            const string text = "𐓏𐓘𐓻𐓘𐓻𐓟 𐒻𐓟";
            int letterCount = 0;

            Span<char> span = text.ToCharArray();
            foreach (CodePoint codePoint in span.EnumerateCodePoints())
            {
                if (CodePoint.IsLetter(codePoint))
                {
                    letterCount++;
                }
            }

            await Assert.That(letterCount).IsEqualTo(8);
        }

        [Test]
        public async Task CanEnumerateReadonlySpan()
        {
            // Test example taken from.
            // https://docs.microsoft.com/en-us/dotnet/api/system.text.rune?view=net-5.0#when-to-use-the-rune-type
            const string text = "𐓏𐓘𐓻𐓘𐓻𐓟 𐒻𐓟";
            int letterCount = 0;

            foreach (CodePoint codePoint in text.AsSpan().EnumerateCodePoints())
            {
                if (CodePoint.IsLetter(codePoint))
                {
                    letterCount++;
                }
            }

            await Assert.That(letterCount).IsEqualTo(8);
        }

        [Test]
        public async Task CanEnumerateInvalidReadonlySpan()
        {
            // The string below contains 2 combining characters then
            // a single high surrogate code unit, then 2 more sets or combining characters.
            // 'ā̈' 'b' '�' 'ç'
            const string text = "a\u0304\u0308b\ud800c\u0327";
            int letterCount = 0;
            int codePointCount = 0;
            foreach (CodePoint codePoint in text.AsSpan().EnumerateCodePoints())
            {
                if (CodePoint.IsLetter(codePoint))
                {
                    letterCount++;
                }

                codePointCount++;
            }

            await Assert.That(letterCount).IsEqualTo(3);
            await Assert.That(codePointCount).IsEqualTo(7);
        }

        [Test]
        public async Task CodePointIsValid()
        {
            // Collect the failures first; awaiting an assertion per code point is slow.
            var invalid = new List<uint>();
            uint i;
            for (i = 0; i <= 0x10FFFFu; i++)
            {
                if (!CodePoint.IsValid(i))
                {
                    invalid.Add(i);
                }
            }

            await Assert.That(invalid).IsEmpty();

            i++;
            await Assert.That(CodePoint.IsValid(i)).IsFalse();
        }

        [Test]
        [MethodDataSource(nameof(GeneralTestData_BmpCodePoints_NoSurrogates))]
        [MethodDataSource(nameof(GeneralTestData_SupplementaryCodePoints_ValidOnly))]
        public async Task Utf16SequenceLengthIsCorrect(GeneralTestData testData)
        {
            var codePoint = new CodePoint(testData.ScalarValue);
            await Assert.That(codePoint.Utf16SequenceLength).IsEqualTo(testData.Utf16Sequence.Length);
        }

        [Test]
        [MethodDataSource(nameof(GeneralTestData_BmpCodePoints_NoSurrogates))]
        [MethodDataSource(nameof(GeneralTestData_SupplementaryCodePoints_ValidOnly))]
        public async Task Utf8SequenceLengthIsCorrect(GeneralTestData testData)
        {
            var codePoint = new CodePoint(testData.ScalarValue);
            await Assert.That(codePoint.Utf8SequenceLength).IsEqualTo(testData.Utf8Sequence.Length);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsDigit(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsDigit(testData.ScalarValue)).IsEqualTo(testData.IsDigit);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsLetter(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsLetter(testData.ScalarValue)).IsEqualTo(testData.IsLetter);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsLetterOrDigit(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsLetterOrDigit(testData.ScalarValue)).IsEqualTo(testData.IsLetterOrDigit);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsLower(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsLower(testData.ScalarValue)).IsEqualTo(testData.IsLower);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsNumber(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsNumber(testData.ScalarValue)).IsEqualTo(testData.IsNumber);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsPunctuation(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsPunctuation(testData.ScalarValue)).IsEqualTo(testData.IsPunctuation);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsSeparator(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsSeparator(testData.ScalarValue)).IsEqualTo(testData.IsSeparator);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsSymbol(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsSymbol(testData.ScalarValue)).IsEqualTo(testData.IsSymbol);
        }

        [Test]
        [MethodDataSource(nameof(UnicodeInfoTestData_Latin1AndSelectOthers))]
        public async Task CodePointIsUpper(UnicodeInfoTestData testData)
        {
            await Assert.That(CodePoint.IsUpper(testData.ScalarValue)).IsEqualTo(testData.IsUpper);
        }

        [Test]
        public async Task CodePointIsWhiteSpaceAscii()
        {
            for (uint i = 0; i <= 0x7Fu; i++)
            {
                await Assert.That(UnicodeUtility.IsAsciiCodePoint(i)).IsTrue();
                await Assert.That(CodePoint.IsWhiteSpace(new CodePoint(i))).IsEqualTo(char.IsWhiteSpace((char)i));
            }
        }

        [Test]
        public async Task CodePointIsWhiteSpaceBmp()
        {
            for (uint i = 0x7Fu + 1u; i <= 0xFFFFu; i++)
            {
                await Assert.That(UnicodeUtility.IsBmpCodePoint(i)).IsTrue();
                await Assert.That(CodePoint.IsWhiteSpace(new CodePoint(i))).IsEqualTo(char.IsWhiteSpace((char)i));
            }
        }

        [Test]
        public async Task CodePointIsControl()
        {
            // Collect the failures first; awaiting an assertion per code point is slow.
            var mismatches = new List<uint>();
            for (uint i = 0; i <= 0x10FFFFu; i++)
            {
                var cp = new CodePoint(i);
                if (cp.IsBmp)
                {
                    if (CodePoint.IsControl(new CodePoint(i)) != char.IsControl((char)i))
                    {
                        mismatches.Add(i);
                    }
                }
                else
                {
                    // Per the Unicode stability policy, the set of control characters
                    // is forever fixed at [ U+0000..U+001F ], [ U+007F..U+009F ]. No
                    // characters will ever be added to or removed from the "control characters"
                    // group. See https://www.unicode.org/policies/stability_policy.html.
                    if (CodePoint.IsControl(cp))
                    {
                        mismatches.Add(i);
                    }
                }
            }

            await Assert.That(mismatches).IsEmpty();
        }

        [Test]
        public async Task CodePointIsMandatoryBreak()
        {
            static bool IsLineBreakClassBreak(uint value)
            {
                // See https://www.unicode.org/standard/reports/tr13/tr13-5.html
                switch (value)
                {
                    case 0x000A: // LINE FEED (LF)
                    case 0x000B: // LINE TABULATION (VT)
                    case 0x000C: // FORM FEED (FF)
                    case 0x000D: // CARRIAGE RETURN (CR)
                    case 0x0085: // NEXT LINE (NEL)
                    case 0x2028: // LINE SEPARATOR (LS)
                    case 0x2029: // PARAGRAPH SEPARATOR (PS)
                        return true;
                    default:
                        return false;
                }
            }

            // Collect the failures first; awaiting an assertion per code point is slow.
            var mismatches = new List<uint>();
            for (uint i = 0; i <= 0x10FFFFu; i++)
            {
                if (CodePoint.IsNewLine(new CodePoint(i)) != IsLineBreakClassBreak(i))
                {
                    mismatches.Add(i);
                }
            }

            await Assert.That(mismatches).IsEmpty();
        }

        [Test]
        [Arguments(0, 0)]
        [Arguments(0x80, 0x80)]
        [Arguments(0x80, 0x100)]
        [Arguments(0x100, 0x80)]
        public async Task Operators_And_CompareTo(uint scalarValueLeft, uint scalarValueRight)
        {
            var left = new CodePoint(scalarValueLeft);
            var right = new CodePoint(scalarValueRight);

            await Assert.That(left == right).IsEqualTo(scalarValueLeft == scalarValueRight);
            await Assert.That(left != right).IsEqualTo(scalarValueLeft != scalarValueRight);
            await Assert.That(left < right).IsEqualTo(scalarValueLeft < scalarValueRight);
            await Assert.That(left <= right).IsEqualTo(scalarValueLeft <= scalarValueRight);
            await Assert.That(left > right).IsEqualTo(scalarValueLeft > scalarValueRight);
            await Assert.That(left >= right).IsEqualTo(scalarValueLeft >= scalarValueRight);
            await Assert.That(Math.Sign(left.CompareTo(right))).IsEqualTo(Math.Sign(scalarValueLeft.CompareTo(scalarValueRight)));
            await Assert.That(Math.Sign(((IComparable)left).CompareTo(right))).IsEqualTo(Math.Sign(((IComparable)scalarValueLeft).CompareTo(scalarValueRight)));
        }

        [Test]
        [Arguments(0)]
        [Arguments(0x10FFFF)]
        public async Task NonGenericCompareTo_NonNullAlwaysGreaterThanNull(uint scalarValue)
        {
            await Assert.That(Math.Sign(((IComparable)new CodePoint(scalarValue)).CompareTo(null))).IsEqualTo(1);
        }

        [Test]
        public async Task NonGenericCompareTo_GivenNonCodePointArgument_ThrowsArgumentException()
        {
            IComparable codePoint = new CodePoint(0);

            await Assert.That(() => codePoint.CompareTo(0 /* int32 */)).ThrowsExactly<ArgumentException>();
        }

        [Test]
        public async Task ReplacementChar()
        {
            await Assert.That(CodePoint.ReplacementChar.Value).IsEqualTo(0xFFFD);
        }

        [Test]
        public async Task CorrectlyIdentifiesTab()
        {
            await Assert.That(CodePoint.IsWhiteSpace(new CodePoint('\t'))).IsTrue();
            await Assert.That(CodePoint.IsNewLine(new CodePoint('\t'))).IsFalse();
        }

        // TODO: Add more cases from Script.txt
        [Test]
        [Arguments(0x0041, 0x005A, ScriptClass.Latin)]
        [Arguments(0xFF41, 0xFF5A, ScriptClass.Latin)]
        [Arguments(0x0370, 0x0373, ScriptClass.Greek)]
        [Arguments(0x1D242, 0x1D245, ScriptClass.Greek)]
        [Arguments(0x620, 0x063F, ScriptClass.Arabic)]
        [Arguments(0x1EEF0, 0x1EEF1, ScriptClass.Arabic)]
        public async Task CodePointIsScript(uint min, uint max, ScriptClass script)
        {
            for (uint i = min; i <= max; i++)
            {
                await Assert.That(CodePoint.GetScriptClass(new CodePoint(i))).IsEqualTo(script);
            }
        }

        [Test]
        [Arguments(0x10ACD, ArabicJoiningType.LeftJoining, ArabicJoiningGroup.ManichaeanHeth)]
        [Arguments(0x0715, ArabicJoiningType.RightJoining, ArabicJoiningGroup.DalathRish)]
        [Arguments(0x1886, ArabicJoiningType.Transparent, ArabicJoiningGroup.NoJoiningGroup)]
        [Arguments(0x18A6, ArabicJoiningType.DualJoining, ArabicJoiningGroup.NoJoiningGroup)]
        [Arguments(0x200C, ArabicJoiningType.NonJoining, ArabicJoiningGroup.NoJoiningGroup)]
        public async Task CodePointIsJoiningClass(uint codePoint, ArabicJoiningType type, ArabicJoiningGroup group)
        {
            ArabicJoiningClass join = CodePoint.GetArabicJoiningClass(new CodePoint(codePoint));

            await Assert.That(join.JoiningType).IsEqualTo(type);
            await Assert.That(join.JoiningGroup).IsEqualTo(group);
        }

        [Test]
        [Arguments(0xFE0F, true)]
        [Arguments(0xFE0E, true)]
        [Arguments(0xFE01, true)]
        [Arguments(0xFE00, true)]
        [Arguments(0xFF00, false)]
        [Arguments(0xFE10, false)]
        public async Task CodePointIsVariationSelector(uint codePoint, bool expectedResult)
        {
            bool result = CodePoint.IsVariationSelector(new CodePoint(codePoint));
            await Assert.That(result).IsEqualTo(expectedResult);
        }

        [Test]
        [Arguments(0x002E, 0x002F, VerticalOrientationType.Rotate)]
        [Arguments(0x003F, 0x0040, VerticalOrientationType.Rotate)]
        [Arguments(0x00A7, 0x00A7, VerticalOrientationType.Upright)]
        [Arguments(0x3001, 0x3002, VerticalOrientationType.TransformUpright)]
        [Arguments(0x300A, 0x300A, VerticalOrientationType.TransformRotate)]
        public async Task CodePointIsVerticalOrientation(uint min, uint max, VerticalOrientationType type)
        {
            for (uint i = min; i <= max; i++)
            {
                VerticalOrientationType vertical = CodePoint.GetVerticalOrientationType(new CodePoint(min));
                await Assert.That(vertical).IsEqualTo(type);
            }
        }

        [Test]
        [Arguments(0x10A0E, IndicSyllabicCategory.Bindu, IndicPositionalCategory.Bottom)]
        [Arguments(0xA983, IndicSyllabicCategory.Visarga, IndicPositionalCategory.Right)]
        [Arguments(0x111C1, IndicSyllabicCategory.Avagraha, IndicPositionalCategory.NA)]
        [Arguments(0x1C37, IndicSyllabicCategory.Nukta, IndicPositionalCategory.Bottom)]
        [Arguments(0x0C48, IndicSyllabicCategory.VowelDependent, IndicPositionalCategory.TopAndBottom)]
        public async Task CodePointIsIndicCategory(uint value, IndicSyllabicCategory syllable, IndicPositionalCategory position)
        {
            CodePoint codePoint = new(value);
            await Assert.That(CodePoint.GetIndicSyllabicCategory(codePoint)).IsEqualTo(syllable);
            await Assert.That(CodePoint.GetIndicPositionalCategory(codePoint)).IsEqualTo(position);
        }
    }
}
