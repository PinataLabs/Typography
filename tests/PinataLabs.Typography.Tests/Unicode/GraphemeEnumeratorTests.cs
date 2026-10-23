// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using PinataLabs.Typography.Unicode;
using TUnit.Core.Interfaces;
using Xunit;

namespace PinataLabs.Typography.Tests.Unicode
{
    public class GraphemeEnumeratorTests
    {
        private ITestOutput Output => TestContext.Current!.Output;

        [Test]
        public void CanEnumerateSpan()
        {
            // Test example taken from.
            // https://docs.microsoft.com/en-us/dotnet/api/system.text.rune?view=net-5.0#when-to-use-the-rune-type
            const string text = "𐓏𐓘𐓻𐓘𐓻𐓟 𐒻𐓟";
            int count = 0;
            Span<char> span = text.ToCharArray();
            foreach (ReadOnlySpan<char> grapheme in span.EnumerateGraphemes())
            {
                Assert.True(grapheme.Length > 0);
                count++;
            }

            Assert.Equal(9, count);
        }

        [Test]
        public void CanEnumerateReadonlySpan()
        {
            // Test example taken from.
            // https://docs.microsoft.com/en-us/dotnet/api/system.text.rune?view=net-5.0#when-to-use-the-rune-type
            const string text = "𐓏𐓘𐓻𐓘𐓻𐓟 𐒻𐓟";
            int count = 0;

            foreach (ReadOnlySpan<char> grapheme in text.AsSpan().EnumerateGraphemes())
            {
                Assert.True(grapheme.Length > 0);
                count++;
            }

            Assert.Equal(9, count);
        }

        [Test]
        public void CanEnumerateInvalidReadonlySpan()
        {
            // The string below contains 2 combining characters then
            // a single high surrogate code unit, then 2 more sets or combining characters.
            // 'ā̈' 'b' '�' 'ç'
            const string text = "a\u0304\u0308b\ud800c\u0327";
            int count = 0;
            foreach (ReadOnlySpan<char> grapheme in text.AsSpan().EnumerateGraphemes())
            {
                Assert.True(grapheme.Length > 0);
                count++;
            }

            Assert.Equal(4, count);
        }

        [Test]
        public void Should_Enumerate_Emoji()
        {
            // https://docs.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction#example-count-char-rune-and-text-element-instances
            const string text = "👩🏽‍🚒";

            int count = 0;
            foreach (ReadOnlySpan<char> grapheme in new SpanGraphemeEnumerator(text.AsSpan()))
            {
                Assert.Equal(4, CodePoint.GetCodePointCount(grapheme));
                Assert.Equal(7, grapheme.Length);
                count++;
            }

            Assert.Equal(1, count);
        }

        [Test]
        public void Should_Enumerate_Alpha()
        {
            const string text = "ABCDEFGHIJ";
            int count = 0;

            foreach (ReadOnlySpan<char> grapheme in new SpanGraphemeEnumerator(text.AsSpan()))
            {
                Assert.Equal(1, CodePoint.GetCodePointCount(grapheme));
                Assert.Equal(1, grapheme.Length);
                count++;
            }

            Assert.Equal(10, count);
        }

        [Test]
        public void ICUTests() => Assert.True(this.ICUTestsImpl());

        private bool ICUTestsImpl()
        {
            this.Output.WriteLine("Grapheme Cluster Tests");
            this.Output.WriteLine("----------------------");

            // Read the test file
            string[] lines = File.ReadAllLines(Path.Combine(TestEnvironment.UnicodeTestDataFullPath, "GraphemeBreakTest.txt"));

            // Process each line
            var tests = new List<Test>();
            for (int lineNumber = 1; lineNumber < lines.Length + 1; lineNumber++)
            {
                // Get the line, remove comments
                string line = lines[lineNumber - 1].Split('#')[0].Trim();

                // Ignore blank/comment only lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var codePoints = new List<uint>();
                var breakPoints = new List<int>();

                // Parse the test
                int p = 0;
                while (p < line.Length)
                {
                    // Ignore white space
                    if (char.IsWhiteSpace(line[p]))
                    {
                        p++;
                        continue;
                    }

                    if (line[p] == '×')
                    {
                        p++;
                        continue;
                    }

                    if (line[p] == '÷')
                    {
                        breakPoints.Add(codePoints.Count);
                        p++;
                        continue;
                    }

                    int codePointPos = p;
                    while (p < line.Length && IsHexDigit(line[p]))
                    {
                        p++;
                    }

                    string codePointStr = line.Substring(codePointPos, p - codePointPos);
                    uint codePoint = Convert.ToUInt32(codePointStr, 16);
                    codePoints.Add(codePoint);
                }

                // Create test
                tests.Add(new Test(lineNumber, codePoints.ToArray(), breakPoints.ToArray()));
            }

            var foundBreaks = new List<int>
            {
                Capacity = 100
            };

            for (int testNumber = 0; testNumber < tests.Count; testNumber++)
            {
                Test t = tests[testNumber];

                foundBreaks.Clear();

                string text = Encoding.UTF32.GetString(MemoryMarshal.Cast<uint, byte>(t.CodePoints).ToArray());

                // Always a leading boundary
                int boundary = 0;
                foundBreaks.Add(boundary);

                // Run the algorithm
                foreach (ReadOnlySpan<char> grapheme in text.AsSpan().EnumerateGraphemes())
                {
                    boundary += CodePoint.GetCodePointCount(grapheme);
                    foundBreaks.Add(boundary);
                }

                // Check the same
                bool pass = true;
                if (foundBreaks.Count != t.BreakPoints.Length)
                {
                    pass = false;
                }
                else
                {
                    for (int i = 0; i < foundBreaks.Count; i++)
                    {
                        if (foundBreaks[i] != t.BreakPoints[i])
                        {
                            pass = false;
                        }
                    }
                }

                if (!pass)
                {
                    this.Output.WriteLine($"Failed test on line {t.LineNumber}");
                    this.Output.WriteLine($"    Code Points: {string.Join(" ", t.CodePoints)}");
                    this.Output.WriteLine($"Expected Breaks: {string.Join(" ", t.BreakPoints)}");
                    this.Output.WriteLine($"  Actual Breaks: {string.Join(" ", foundBreaks)}");
                    this.Output.WriteLine($"     Char Props: {string.Join(" ", t.CodePoints.Select(x => UnicodeData.GetGraphemeClusterClass(x)))}");
                    return false;
                }
            }

            return true;
        }

        private static bool IsHexDigit(char ch)
            => char.IsDigit(ch) || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');

        private readonly struct Test
        {
            public Test(int lineNumber, uint[] codePoints, int[] breakPoints)
            {
                this.LineNumber = lineNumber;
                this.CodePoints = codePoints;
                this.BreakPoints = breakPoints;
            }

            public int LineNumber { get; }

            public uint[] CodePoints { get; }

            public int[] BreakPoints { get; }
        }
    }
}
