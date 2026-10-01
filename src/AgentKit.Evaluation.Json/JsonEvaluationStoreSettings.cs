// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json;

/// <summary>Is the validated, immutable snapshot of a JSON evaluation result store bounds and encoding contract.</summary>
public sealed record JsonEvaluationStoreSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="maximumRecordBytes">The positive largest encoded result record.</param>
    /// <param name="maximumDocumentBytes">The positive largest manifest document.</param>
    /// <param name="encoding">The non-null encoding contract.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is null.</exception>
    public JsonEvaluationStoreSettings(int maximumRecordBytes, int maximumDocumentBytes, JsonEncodingSettings encoding)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDocumentBytes);
        ArgumentNullException.ThrowIfNull(encoding);
        MaximumRecordBytes = maximumRecordBytes;
        MaximumDocumentBytes = maximumDocumentBytes;
        Encoding = encoding;
    }

    /// <summary>Gets the largest encoded result record.</summary>
    public int MaximumRecordBytes { get; }

    /// <summary>Gets the largest manifest document.</summary>
    public int MaximumDocumentBytes { get; }

    /// <summary>Gets the encoding contract.</summary>
    public JsonEncodingSettings Encoding { get; }

    /// <summary>Creates settings with every documented default.</summary>
    /// <returns>Default settings.</returns>
    public static JsonEvaluationStoreSettings CreateDefault() => new(1_048_576, 1_048_576, JsonEncodingSettings.CreateDefault());
}
