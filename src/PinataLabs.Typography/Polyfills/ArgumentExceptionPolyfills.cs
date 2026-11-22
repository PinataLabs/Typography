// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace System;

/// <summary>
/// Provides <see cref="ArgumentException"/> members that the netstandard2.1 BCL lacks.
/// </summary>
internal static class ArgumentExceptionPolyfills
{
    extension(ArgumentException)
    {
        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null,
        /// or an <see cref="ArgumentException"/> if it is empty or consists only of white-space characters.
        /// </summary>
        /// <param name="argument">The string argument to validate.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowIfNullOrWhiteSpace(string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                ArgumentNullException.ThrowIfNull(argument, paramName);
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", paramName);
            }
        }
    }
}
