// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

using System.Runtime.CompilerServices;

/// <summary>Provides canonical SQLite adapter argument guards for constraints absent from the base class library.</summary>
internal static class ArgumentExceptionExtensions
{
    /// <summary>Adds canonical budget SQLite path validation to <see cref="ArgumentException"/> without accepting URI or memory targets.</summary>
    extension(ArgumentException)
    {
        /// <summary>Throws when a SQLite budget-ledger target is not one fully qualified ordinary filesystem path.</summary>
        /// <param name="databasePath">The non-null, nonblank path to validate.</param>
        /// <param name="paramName">The caller parameter name inferred when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="databasePath"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="databasePath"/> is blank, relative, an SQLite memory target, a file URI, or a DataDirectory substitution.</exception>
        internal static void ThrowIfInvalidSqliteDatabasePath(
            string databasePath,
            [CallerArgumentExpression(nameof(databasePath))] string? paramName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath, paramName);
            if (!Path.IsPathFullyQualified(databasePath)
                || string.Equals(databasePath, ":memory:", StringComparison.OrdinalIgnoreCase)
                || databasePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || databasePath.StartsWith("|DataDirectory|", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The SQLite database path must be one fully qualified ordinary filesystem path.",
                    paramName);
            }
        }

    }
}
