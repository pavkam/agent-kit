// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

using System.Runtime.CompilerServices;

/// <summary>Provides the session SQLite path guard absent from the BCL.</summary>
internal static class ArgumentExceptionExtensions
{
    /// <summary>Adds fixed ordinary-path validation to <see cref="ArgumentException"/>.</summary>
    extension(ArgumentException)
    {
        /// <summary>Rejects relative paths and SQLite special target syntax.</summary>
        /// <param name="databasePath">The nonnull path.</param>
        /// <param name="paramName">The inferred caller parameter name.</param>
        /// <exception cref="ArgumentException">The path is blank, relative, URI-based, memory-backed, or uses DataDirectory substitution.</exception>
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
                throw new ArgumentException("The SQLite database path must be fully qualified ordinary path.", paramName);
            }
        }
    }
}
