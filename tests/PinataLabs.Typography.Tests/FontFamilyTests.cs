// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests
{
    public class FontFamilyTests
    {
        private readonly FontFamily[] families = SystemFonts.Families.ToArray();

        [Test]
        public async Task EqualNullTests()
        {
            FontFamily fontFamily = default;
            await Assert.That(fontFamily == default).IsTrue();
            await Assert.That(fontFamily != default).IsFalse();

            fontFamily = this.families[0];
            await Assert.That(fontFamily != default).IsTrue();
            await Assert.That(fontFamily == default).IsFalse();
            await Assert.That(fontFamily.Equals(default)).IsFalse();
        }

        [Test]
        public async Task EqualTests()
        {
            FontFamily fontFamily = this.families[0];
            FontFamily fontFamily2 = this.families[0];

            await Assert.That(fontFamily == fontFamily2).IsTrue();
            await Assert.That(fontFamily != fontFamily2).IsFalse();
            await Assert.That(fontFamily.Equals(fontFamily2)).IsTrue();
        }

        [Test]
        public async Task NotEqualTests()
        {
            FontFamily fontFamily = this.families[0];
            FontFamily fontFamily2 = this.families[1];

            await Assert.That(fontFamily == fontFamily2).IsFalse();
            await Assert.That(fontFamily != fontFamily2).IsTrue();
            await Assert.That(fontFamily.Equals(fontFamily2)).IsFalse();
        }

        [Test]
        [MethodDataSource(nameof(SystemFontFamilies))]
        public async Task HasPathTests(FontFamily family)
        {
            await Assert.That(family.TryGetPaths(out IEnumerable<string> familyPaths)).IsTrue();
            await Assert.That(familyPaths).IsNotNull();
            await Assert.That(familyPaths.Any()).IsTrue();

            foreach (FontStyle style in family.GetAvailableStyles())
            {
                Font font = family.CreateFont(12, style);
                await Assert.That(font.TryGetPath(out string fontPath)).IsTrue();
                await Assert.That(familyPaths).Contains(fontPath);
            }
        }

        [Test]
        public async Task Throws_FontException_CreateFont_WhenDefault()
        {
            await Assert.That(() => default(FontFamily).CreateFont(12F)).ThrowsExactly<FontException>();
        }

        [Test]
        public async Task Throws_FontException_TryGetPath_WhenDefault()
        {
            await Assert.That(() => default(FontFamily).TryGetPaths(out IEnumerable<string> _)).ThrowsExactly<FontException>();
        }

        [Test]
        public async Task Throws_FontException_TryGetMetrics_WhenDefault()
        {
            await Assert.That(() => default(FontFamily).TryGetMetrics(FontStyle.Regular, out FontMetrics _)).ThrowsExactly<FontException>();
        }

        public static IEnumerable<FontFamily> SystemFontFamilies() => SystemFonts.Families;
    }
}
