// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using PinataLabs.Typography;

namespace ListFonts;

public static class Program
{
    public static void Main(string[] args)
    {
        var pairings = new List<Pairing>();
        IOrderedEnumerable<FontFamily> ordered = SystemFonts.Families.OrderBy(x => x.Name);
        foreach (FontFamily family in ordered)
        {
            // TryGetPaths lists one path for each face, in the order of Faces.
            IReadOnlyList<FontMetrics> faces = family.Faces;
            string[] paths = family.TryGetPaths(out IEnumerable<string> familyPaths) ? familyPaths.ToArray() : [];
            for (int i = 0; i < faces.Count; i++)
            {
                string? path = paths.Length == faces.Count ? paths[i] : null;
                pairings.Add(new Pairing(faces[i].Description.FontNameInvariantCulture, path));
            }
        }

        int max = pairings.Max(x => x.Name.Length);
        foreach (Pairing p in pairings)
        {
            Console.WriteLine($"{p.Name.PadRight(max)} {p.Path}");
        }

        if (Debugger.IsAttached)
        {
            Console.WriteLine(string.Empty);
            while (!Console.KeyAvailable)
            {
                Thread.Sleep(100);
            }
        }
    }

    public struct Pairing
    {
        public Pairing(string name, string? path)
        {
            this.Name = name;
            this.Path = path;
        }

        public string Name { get; }

        public string? Path { get; }
    }
}
