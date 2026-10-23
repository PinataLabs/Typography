// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core.Interfaces;

namespace PinataLabs.Typography.Tests.Unicode
{
    public class LineBreakEnumeratorTests
    {
        private ITestOutput Output => TestContext.Current!.Output;

        [Test]
        public async Task BasicLatinTest()
        {
            var lineBreaker = new LineBreakEnumerator("Hello World\r\nThis is a test.".AsSpan());
            var breaks = new List<LineBreak>();
            while (lineBreaker.MoveNext())
            {
                breaks.Add(lineBreaker.Current);
            }

            await Assert.That(breaks.Count).IsEqualTo(6);

            await Assert.That(breaks[0].PositionWrap).IsEqualTo(6);
            await Assert.That(breaks[0].Required).IsFalse();

            await Assert.That(breaks[1].PositionWrap).IsEqualTo(13);
            await Assert.That(breaks[1].Required).IsTrue();

            await Assert.That(breaks[2].PositionWrap).IsEqualTo(18);
            await Assert.That(breaks[2].Required).IsFalse();

            await Assert.That(breaks[3].PositionWrap).IsEqualTo(21);
            await Assert.That(breaks[3].Required).IsFalse();

            await Assert.That(breaks[4].PositionWrap).IsEqualTo(23);
            await Assert.That(breaks[4].Required).IsFalse();

            await Assert.That(breaks[5].PositionWrap).IsEqualTo(28);
            await Assert.That(breaks[5].Required).IsFalse();
        }

        [Test]
        public async Task ForwardTextWithOuterWhitespace()
        {
            string text = " Apples Pears Bananas   ";
            var breaks = new List<LineBreak>();

            foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan()))
            {
                breaks.Add(lineBreak);
            }

            await Assert.That(breaks[0].PositionWrap).IsEqualTo(1);
            await Assert.That(breaks[0].PositionMeasure).IsEqualTo(0);
            await Assert.That(breaks[1].PositionWrap).IsEqualTo(8);
            await Assert.That(breaks[1].PositionMeasure).IsEqualTo(7);
            await Assert.That(breaks[2].PositionWrap).IsEqualTo(14);
            await Assert.That(breaks[2].PositionMeasure).IsEqualTo(13);
            await Assert.That(breaks[3].PositionWrap).IsEqualTo(24);
            await Assert.That(breaks[3].PositionMeasure).IsEqualTo(21);
        }

        [Test]
        public async Task ForwardTest()
        {
            string text = "Apples Pears Bananas";
            var breaks = new List<LineBreak>();

            foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan()))
            {
                breaks.Add(lineBreak);
            }

            await Assert.That(breaks[0].PositionWrap).IsEqualTo(7);
            await Assert.That(breaks[0].PositionMeasure).IsEqualTo(6);
            await Assert.That(breaks[1].PositionWrap).IsEqualTo(13);
            await Assert.That(breaks[1].PositionMeasure).IsEqualTo(12);
            await Assert.That(breaks[2].PositionWrap).IsEqualTo(20);
            await Assert.That(breaks[2].PositionMeasure).IsEqualTo(20);
        }

        [Test]
        public async Task ICUTests()
        {
            await Assert.That(this.ICUTestsImpl()).IsTrue();
        }

        // Contains over 7000 tests
        // https://www.unicode.org/Public/13.0.0/ucd/auxiliary/LineBreakTest.html
        private bool ICUTestsImpl()
        {
            this.Output.WriteLine("Line Breaker Tests");
            this.Output.WriteLine("------------------");

            // Read the test file
            string[] lines = File.ReadAllLines(Path.Combine(TestEnvironment.UnicodeTestDataFullPath, "LineBreakTest.txt"));

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
                var test = new Test(lineNumber, codePoints.ToArray(), breakPoints.ToArray());
                tests.Add(test);
            }

            var foundBreaks = new List<int>();

            for (int testNumber = 0; testNumber < tests.Count; testNumber++)
            {
                Test t = tests[testNumber];

                foundBreaks.Clear();

                // Run the line breaker and build a list of break points
                string text = Encoding.UTF32.GetString(MemoryMarshal.Cast<uint, byte>(t.CodePoints).ToArray());

                var enumerator = new LineBreakEnumerator(text.AsSpan());
                while (enumerator.MoveNext())
                {
                    foundBreaks.Add(enumerator.Current.PositionWrap);
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
                    LineBreakClass[] classes = t.CodePoints.Select(x => UnicodeData.GetLineBreakClass(x)).ToArray();

                    this.Output.WriteLine($"Failed test on line {t.LineNumber}");
                    this.Output.WriteLine($"    Code Points: {string.Join(" ", t.CodePoints)}");
                    this.Output.WriteLine($"Expected Breaks: {string.Join(" ", t.BreakPoints)}");
                    this.Output.WriteLine($"  Actual Breaks: {string.Join(" ", foundBreaks)}");
                    this.Output.WriteLine($"     Char Props: {string.Join(" ", classes)}");

                    return false;
                }
            }

            return true;
        }

        private static bool IsHexDigit(char ch) => char.IsDigit(ch) || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');

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
