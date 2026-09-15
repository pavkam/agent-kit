// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>
/// Mutable composition-time input for the SQLite session store and directory
/// registrations that accept a configure delegate.
/// </summary>
/// <remarks>
/// <para>
/// An instance exists only for the duration of one
/// <c>AddSqliteSessionStore</c> or <c>AddSqliteSessionDirectory</c> call. The
/// registration reads the configured values into an immutable, validated
/// <see cref="SqliteSessionStoreSettings"/> and binds that record to the store
/// factory or directory; the options object itself is never added to the
/// service collection and is not resolvable through <c>IOptions&lt;T&gt;</c>.
/// </para>
/// <para>
/// Setters perform no validation so a delegate may assign properties in any
/// order. Every constraint documented on the corresponding property is
/// enforced when the registration constructs
/// <see cref="SqliteSessionStoreSettings"/>, so an invalid value fails at
/// registration time with <see cref="ArgumentOutOfRangeException"/> rather than
/// during the first store operation. The defaults equal
/// <see cref="SqliteSessionStoreSettings.CreateDefault"/>.
/// </para>
/// <para>This type is not thread-safe; it is intended for single-threaded composition.</para>
/// </remarks>
public sealed class SqliteSessionStoreOptions
{
    /// <summary>
    /// Gets or sets the SQLite busy timeout applied to every connection the
    /// store and directory open.
    /// </summary>
    /// <value>
    /// A whole-second duration of at least one second whose total seconds fit
    /// in an <see cref="int"/>. Defaults to five seconds. Fractional seconds
    /// are rejected because SQLite expresses the busy timeout in whole seconds.
    /// </value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum encoded byte length of one session entry
    /// payload the store commits.
    /// </summary>
    /// <value>
    /// A positive byte count. Defaults to 1,048,576 bytes (one mebibyte). An
    /// operation whose entry encodes to more bytes than this bound is rejected
    /// with that operation's typed failure, for example
    /// <see cref="SessionAppendFailed"/>, before anything is written. The
    /// effective bound is further limited by the configured session-entry codec
    /// catalog, which applies its own payload ceiling.
    /// </value>
    public int MaximumEntryPayloadBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets how many distinct adapter-issued paged-read snapshots one
    /// store instance retains in process for exact continuation.
    /// </summary>
    /// <value>
    /// A positive count. Defaults to 4,096. When more snapshots have been
    /// issued, the oldest are evicted first and a continuation that supplies an
    /// evicted snapshot fails with <see cref="SessionReadFailed"/> instead of
    /// trusting caller-authored version and sequence claims.
    /// </value>
    public int MaximumIssuedReadSnapshots { get; set; } = 4096;
}
