// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Running;

namespace PinataLabs.Typography.Benchmarks;

internal class Program
{
    public static void Main(string[] args) => BenchmarkSwitcher
        .FromAssembly(typeof(Program).Assembly)
        .Run(args);
}
