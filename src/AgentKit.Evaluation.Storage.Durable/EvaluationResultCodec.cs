// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Encodes and decodes results under one JSON contract for the durable adapters.</summary>
internal static class EvaluationResultCodec
{
    /// <summary>Encodes a result under a contract and byte bound.</summary>
    /// <param name="result">The non-null result.</param>
    /// <param name="options">The non-null frozen serializer options.</param>
    /// <param name="maximumBytes">The positive greatest encoded size.</param>
    /// <returns>The UTF-8 JSON payload.</returns>
    /// <exception cref="InvalidDataException">The encoded payload exceeds <paramref name="maximumBytes"/>.</exception>
    internal static byte[] Encode(EvaluationCaseResult result, JsonSerializerOptions options, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonStoreSerialization.Encode(EvaluationResultDocument.FromDomain(result), options, maximumBytes);
    }

    /// <summary>Decodes a persisted payload and restores the validated result.</summary>
    /// <param name="payload">The UTF-8 JSON payload.</param>
    /// <param name="options">The non-null frozen serializer options.</param>
    /// <returns>The validated result.</returns>
    /// <exception cref="JsonException">The payload is not valid under the contract.</exception>
    /// <exception cref="InvalidDataException">The payload decodes to nothing.</exception>
    /// <exception cref="ArgumentException">A restored value fails domain validation.</exception>
    internal static EvaluationCaseResult Decode(ReadOnlySpan<byte> payload, JsonSerializerOptions options) =>
        JsonStoreSerialization.Decode<EvaluationResultDocument>(payload, options).ToDomain();
}
