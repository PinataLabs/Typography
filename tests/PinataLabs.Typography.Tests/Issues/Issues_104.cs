// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.General.CMap;
using PinataLabs.Typography.Unicode;
using Xunit;
using static PinataLabs.Typography.Tables.General.CMap.Format4SubTable;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_104
    {
        [Fact]
        public void Format4SubTableWithSegmentsHasOffByOneWhenOverflowing()
        {
            Segment[] segments = new[]
            {
                new Segment(
                    0,
                    ushort.MaxValue, // end
                    ushort.MinValue, // start of range
                    short.MaxValue, // delta
                    0) // zero to force correctly tested codepath
            };
            var tbl = new Format4SubTable(
                0,
                WellKnownIds.PlatformIDs.Windows,
                0,
                segments,
                null);

            const int delta = short.MaxValue + 2; // extra 2 to handle the difference between ushort and short when offsettings

            const int codePoint = delta + 5;
            Assert.True(tbl.TryGetGlyphId(new CodePoint(codePoint), out ushort gid));

            Assert.Equal(5, gid);
        }
    }
}
