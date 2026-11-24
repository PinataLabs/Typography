// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using PinataLabs.Typography;
using PinataLabs.Typography.AotSmoke;

int failures = 0;

foreach ((string resource, FontProgramFormat format) in new[]
{
    ("OpenSans-Regular.ttf", FontProgramFormat.TrueType),
    ("SourceSansPro-Regular.otf", FontProgramFormat.Cff),
    ("bchr8a.pfb", FontProgramFormat.Type1),
})
{
    FontProgram font = FontProgram.Load(ReadResource(resource));
    (ushort glyphId, int commands) = FirstOutline(font);
    bool ok = font.Format == format && commands > 0;
    Console.WriteLine($"{resource}: {font.Format}, {font.GlyphCount} glyphs, glyph {glyphId} has {commands} outline commands: {(ok ? "ok" : "FAIL")}");
    failures += ok ? 0 : 1;
}

using (MemoryStream stream = new(ReadResource("OpenSans-Regular.ttf")))
{
    FontCollection collection = new();
    Font font = collection.Add(stream).CreateFont(12);
    FontRectangle size = TextMeasurer.MeasureSize("Hello, AOT", new TextOptions(font));
    bool ok = size.Width > 0 && size.Height > 0;
    Console.WriteLine($"TextMeasurer.MeasureSize: {size.Width} x {size.Height}: {(ok ? "ok" : "FAIL")}");
    failures += ok ? 0 : 1;
}

return failures;

static byte[] ReadResource(string name)
{
    using Stream stream = typeof(CountingSink).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Missing embedded resource {name}.");
    using MemoryStream copy = new();
    stream.CopyTo(copy);
    return copy.ToArray();
}

// Glyph 0 is .notdef, which can be empty, and a Type 1 font's glyph 0 can be a space.
static (ushort GlyphId, int Commands) FirstOutline(FontProgram font)
{
    for (int i = 0; i < font.GlyphCount; i++)
    {
        CountingSink sink = default;
        if (font.TryWriteOutline((ushort)i, ref sink) && sink.Commands > 0)
        {
            return ((ushort)i, sink.Commands);
        }
    }

    return (0, 0);
}
