// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

using System.Runtime.CompilerServices;

/// <summary>Provides canonical SQLite adapter argument guards for constraints absent from the base class library.</summary>
internal static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>Throws when a SQLite grant-store target is not one fully qualified ordinary filesystem path.</summary>
        /// <param name="databasePath">The nonblank path to validate after ordinary null and whitespace guards.</param>
        /// <param name="paramName">The caller parameter name inferred when omitted.</param>
        /// <exception cref="ArgumentException"><paramref name="databasePath"/> is relative, an SQLite memory target, a file URI, or a DataDirectory substitution.</exception>
        internal static void ThrowIfInvalidSqliteDatabasePath(
            string databasePath,
            [CallerArgumentExpression(nameof(databasePath))] string? paramName = null)
        {
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

        /// <summary>Throws when a grant cannot round-trip through the adapter's strict bounded evidence codec.</summary>
        /// <param name="grant">The grant to validate without storage access.</param>
        /// <param name="settings">The immutable codec bounds.</param>
        /// <param name="paramName">The caller parameter name inferred when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grant"/> or <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="grant"/> contains malformed copied or nested evidence that cannot be reconstructed exactly.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="grant"/> exceeds a configured evidence bound.</exception>
        internal static void ThrowIfNotPersistable(
            SecurityGrant grant,
            SqliteSecurityGrantStoreSettings settings,
            [CallerArgumentExpression(nameof(grant))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(grant);
            ArgumentNullException.ThrowIfNull(settings);
            ValidateCodecRoundTrip(
                () => SqliteSecurityGrantCodec.EncodeGrant(grant, settings),
                payload => SqliteSecurityGrantCodec.EncodeGrant(
                    SqliteSecurityGrantCodec.DecodeGrant(payload, settings), settings),
                paramName);
        }

        /// <summary>Throws when enforcement evidence cannot round-trip through the adapter's strict bounded evidence codec.</summary>
        /// <param name="enforcement">The enforcement evidence to validate without storage access.</param>
        /// <param name="settings">The immutable codec bounds.</param>
        /// <param name="paramName">The caller parameter name inferred when omitted.</param>
        /// <exception cref="ArgumentNullException"><paramref name="enforcement"/> or <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="enforcement"/> contains malformed copied or nested evidence that cannot be reconstructed exactly.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="enforcement"/> exceeds a configured evidence bound.</exception>
        internal static void ThrowIfNotPersistable(
            SecurityEnforcementRequest enforcement,
            SqliteSecurityGrantStoreSettings settings,
            [CallerArgumentExpression(nameof(enforcement))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(enforcement);
            ArgumentNullException.ThrowIfNull(settings);
            ValidateCodecRoundTrip(
                () => SqliteSecurityGrantCodec.EncodeEnforcement(enforcement, settings),
                payload => SqliteSecurityGrantCodec.EncodeEnforcement(
                    SqliteSecurityGrantCodec.DecodeEnforcement(payload, settings), settings),
                paramName);
        }

    }

    /// <summary>Executes one bounded encode-decode-encode validation and maps malformed evidence to its caller parameter.</summary>
    /// <param name="encode">The initial bounded encoder.</param><param name="reconstruct">The strict decoder and reconstruction encoder.</param><param name="paramName">The public evidence parameter name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encode"/> or <paramref name="reconstruct"/> is null.</exception>
    /// <exception cref="ArgumentException">The evidence cannot be reconstructed byte for byte.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured evidence bound is exceeded.</exception>
    internal static void ValidateCodecRoundTrip(
        Func<byte[]> encode,
        Func<byte[], byte[]> reconstruct,
        string? paramName)
    {
        ArgumentNullException.ThrowIfNull(encode);
        ArgumentNullException.ThrowIfNull(reconstruct);
        try
        {
            var encoded = encode();
            var reconstructed = reconstruct(encoded);
            if (!encoded.AsSpan().SequenceEqual(reconstructed))
            {
                throw new InvalidDataException("The security evidence codec did not reconstruct exact bytes.");
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or NullReferenceException
            or OverflowException)
        {
            throw new ArgumentException(
                "The security evidence cannot be persisted and reconstructed exactly.", paramName, exception);
        }
    }
}
