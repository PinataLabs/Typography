// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PinataLabs.Typography.Native;

namespace PinataLabs.Typography.Tests.Native
{
    public class MacSystemFontsEnumeratorTests
    {
        [Test]
        public async Task TestReset()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }

            using var enumerator = new MacSystemFontsEnumerator();
            var fonts1 = new HashSet<string>(enumerator);
            await Assert.That(fonts1).IsNotEmpty();

            enumerator.Reset();
            var fonts2 = new HashSet<string>(enumerator);
            await Assert.That(fonts1.Except(fonts2)).IsEmpty();
        }
    }
}
