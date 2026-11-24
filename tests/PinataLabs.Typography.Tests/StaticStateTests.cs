// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class StaticStateTests
{
    // Callers render separate documents in parallel, so a static that code can reassign is state they share.
    // Types the compiler generates, such as <>O, cache one delegate per method group for the process.
    // Code coverage injects tracker types with writable statics, so only the library's own namespace is checked.
    [Test]
    public async Task EveryStaticFieldIsReadOnly()
    {
        List<string> writable = typeof(FontProgram).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("PinataLabs.Typography", StringComparison.Ordinal) == true)
            .Where(type => !type.Name.StartsWith('<') && type.DeclaringType?.Name.StartsWith('<') != true)
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsLiteral && !field.IsInitOnly)
                .Select(field => $"{type.FullName}.{field.Name}"))
            .ToList();

        await Assert.That(writable).IsEmpty();
    }
}
