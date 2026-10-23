// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Unicode;
using Xunit;

namespace PinataLabs.Typography.Tests.Unicode
{
    public class UnicodeUtilityTests
    {
        [Test]
        [Arguments(0x3040u, 0x309Fu)] // Hiragana
        [Arguments(0x30A0u, 0x30FFu)] // Katakana
        [Arguments(0xAC00u, 0xD7A3u)] // Hangul Syllables
        [Arguments(0x4E00u, 0x9FFFu)] // CJK Unified Ideographs
        [Arguments(0x3400u, 0x4DBFu)] // CJK Unified Ideographs Extension A
        [Arguments(0x20000u, 0x2A6DFu)] // CJK Unified Ideographs Extension B
        [Arguments(0x2A700u, 0x2B73Fu)] // CJK Unified Ideographs Extension C
        [Arguments(0x2B740u, 0x2B81Fu)] // CJK Unified Ideographs Extension D
        [Arguments(0x2B820u, 0x2CEAFu)] // CJK Unified Ideographs Extension E
        [Arguments(0x2CEB0u, 0x2EBEFu)] // CJK Unified Ideographs Extension F
        [Arguments(0x30000u, 0x3134Fu)] // CJK Unified Ideographs Extension G
        [Arguments(0xF900u, 0xFAFFu)] // CJK Compatibility Ideographs
        [Arguments(0x2F800u, 0x2FA1Fu)] // CJK Compatibility Ideographs Supplement
        public void CanDetectCJKCodePoints(uint min, uint max)
        {
            for (uint i = min; i <= max; i++)
            {
                Assert.True(UnicodeUtility.IsCJKCodePoint(i));
            }
        }

        [Test]
        [Arguments(0x0u, 0x7Fu)] // ASCII
        public void NoFalsePositiveCJKCodePoints(uint min, uint max)
        {
            for (uint i = min; i <= max; i++)
            {
                Assert.False(UnicodeUtility.IsCJKCodePoint(i));
            }
        }

        [Test]
        [Arguments(0x00AD, 0x00AD)]
        [Arguments(0x034F, 0x034F)]
        [Arguments(0x061C, 0x061C)]
        [Arguments(0x115F, 0x1160)]
        [Arguments(0x17B4, 0x17B5)]
        [Arguments(0x180B, 0x180D)]
        [Arguments(0x180E, 0x180E)]
        [Arguments(0x180F, 0x180F)]
        [Arguments(0x200B, 0x200F)]
        [Arguments(0x202A, 0x202E)]
        [Arguments(0x2060, 0x2064)]
        [Arguments(0x2065, 0x2065)]
        [Arguments(0x2066, 0x206F)]
        [Arguments(0x3164, 0x3164)]
        [Arguments(0xFE00, 0xFE0F)]
        [Arguments(0xFEFF, 0xFEFF)]
        [Arguments(0xFFA0, 0xFFA0)]
        [Arguments(0xFFF0, 0xFFF8)]
        [Arguments(0x1BCA0, 0x1BCA3)]
        [Arguments(0x1D173, 0x1D17A)]
        [Arguments(0xE0000, 0xE0000)]
        [Arguments(0xE0001, 0xE0001)]
        [Arguments(0xE0002, 0xE001F)]
        [Arguments(0xE0020, 0xE007F)]
        [Arguments(0xE0080, 0xE00FF)]
        [Arguments(0xE0100, 0xE01EF)]
        [Arguments(0xE01F0, 0xE0FFF)]
        public void CanDetectDefaultIgnorableCodePoint(uint min, uint max)
        {
            for (uint i = min; i <= max; i++)
            {
                Assert.True(UnicodeUtility.IsDefaultIgnorableCodePoint(i));
            }
        }

        [Test]
        [Arguments(0x0u, 0x7Fu)] // ASCII
        public void NoFalsePositiveDefaultIgnorableCodePoint(uint min, uint max)
        {
            for (uint i = min; i <= max; i++)
            {
                Assert.False(UnicodeUtility.IsDefaultIgnorableCodePoint(i));
            }
        }
    }
}
