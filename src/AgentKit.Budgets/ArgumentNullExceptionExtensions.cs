// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides the canonical guard for <see cref="Lock"/>, whose special compiler
/// semantics make conversion to <see cref="object"/> unsafe at a locking call site.
/// </summary>
internal static class ArgumentNullExceptionExtensions
{
    extension(ArgumentNullException)
    {
        /// <summary>Throws when an authority-owned hierarchy lock is null.</summary>
        /// <param name="value">The lock required to coordinate atomic hierarchy mutations.</param>
        /// <param name="paramName">The caller expression used as the exception parameter name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        internal static void ThrowIfNullLock(
            Lock? value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
