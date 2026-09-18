// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Captures immutable positive evidence bounds and the encoding contract for one JSON budget ledger.</summary>
/// <remarks>
/// Unlike a store whose log can be rewritten from live state, this ledger keeps its complete ordered transition journal:
/// monotonic ledger revisions, accounting revisions, overrun-hold generations, and recovery watermarks are all derived from
/// that history, so discarding records would change persisted identities. There is therefore no compaction threshold to
/// configure; the only rewrite the adapter performs discards one incomplete trailing append under
/// <see cref="JsonStoreRecoveryMode.RecoverTornAppends"/>.
/// </remarks>
public sealed record JsonBudgetLedgerSettings
{
    /// <summary>Initializes explicit evidence bounds and the frozen JSON encoding contract.</summary>
    /// <param name="maximumRecordBytes">The positive maximum encoded size of one persisted transition record, including a whole indivisible batch.</param>
    /// <param name="maximumDocumentBytes">The positive maximum encoded size of the store manifest document.</param>
    /// <param name="encoding">The frozen JSON encoding contract bound to the store root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A byte bound is not positive.</exception>
    public JsonBudgetLedgerSettings(int maximumRecordBytes, int maximumDocumentBytes, JsonEncodingSettings encoding)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDocumentBytes);
        ArgumentNullException.ThrowIfNull(encoding);

        MaximumRecordBytes = maximumRecordBytes;
        MaximumDocumentBytes = maximumDocumentBytes;
        Encoding = encoding;
    }

    /// <summary>Gets the maximum encoded transition-record size.</summary>
    /// <value>
    /// A positive byte count enforced before an append and while replaying the journal. Because one indivisible batch is
    /// written as a single record, this value also bounds how large an atomic batch this store can accept.
    /// </value>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the maximum encoded manifest size.</summary>
    /// <value>A positive byte count enforced before the manifest document is decoded.</value>
    public int MaximumDocumentBytes { get; }

    /// <summary>Gets the frozen JSON encoding contract.</summary>
    /// <value>The immutable contract whose fingerprint is bound to the store root at initialization.</value>
    public JsonEncodingSettings Encoding { get; }

    /// <summary>Creates conservative explicit defaults for local applications.</summary>
    /// <returns>Settings with one-mebibyte record and manifest bounds and the canonical encoding contract.</returns>
    public static JsonBudgetLedgerSettings CreateDefault() => new(
        1_048_576,
        1_048_576,
        JsonEncodingSettings.CreateDefault());
}
