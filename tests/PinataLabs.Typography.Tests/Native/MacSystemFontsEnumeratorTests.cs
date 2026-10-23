// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using PinataLabs.Typography.Native;
using Xunit;

namespace PinataLabs.Typography.Tests.Native
{
    public class MacSystemFontsEnumeratorTests
    {
        [Fact]
        public void TestReset()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }

            using var enumerator = new MacSystemFontsEnumerator();
            var fonts1 = new HashSet<string>(enumerator);
            Assert.NotEmpty(fonts1);

            enumerator.Reset();
            var fonts2 = new HashSet<string>(enumerator);
            Assert.Empty(fonts1.Except(fonts2));
        }
    }
}
