// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// One stretch of text in a single script.
/// </summary>
internal readonly record struct ScriptRun(int Start, int Length, ScriptClass Script);
