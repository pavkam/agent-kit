// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

using System.Runtime.CompilerServices;

/// <summary>Holds the canonical argument guard for ordinary SQLite database paths.</summary>
internal static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>Throws unless a path is one fully qualified ordinary filesystem path.</summary>
        /// <param name="databasePath">The path to validate.</param>
        /// <param name="paramName">The argument name, inferred from the call site.</param>
        /// <exception cref="ArgumentNullException"><paramref name="databasePath"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="databasePath"/> is blank, relative, in-memory, a URI, or a data-directory substitution.</exception>
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
                throw new ArgumentException("The SQLite database path must be one fully qualified ordinary filesystem path.", paramName);
            }
        }
    }
}
