// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Runtime.CompilerServices;

/// <summary>
/// Reusable <see cref="ArgumentOutOfRangeException"/> guard clauses for
/// constraints the base class library does not already expose as a
/// <c>ThrowIf*</c> member.
/// </summary>
public static class ArgumentOutOfRangeExceptionExtensions
{
    extension(ArgumentOutOfRangeException)
    {
        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> when
        /// <paramref name="value"/> is not a named value of
        /// <typeparamref name="TEnum"/>.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type to validate.</typeparam>
        /// <param name="value">The enumeration value to validate.</param>
        /// <param name="paramName">
        /// The caller-supplied parameter name, inferred from
        /// <paramref name="value"/> when omitted.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is not a named enumeration value.
        /// </exception>
        public static void ThrowIfUndefined<TEnum>(
            TEnum value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    $"Value must be a defined {typeof(TEnum).Name} value.");
            }
        }
    }
}
