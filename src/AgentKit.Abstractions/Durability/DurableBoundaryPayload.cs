// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Encodes and decodes in-process boundary manifests as one versioned canonical JSON payload shape.</summary>
/// <remarks>
/// <para>
/// A journaled boundary records a manifest of the work it represents, never the runtime object graph that produced
/// it. The effect is reconstructed from already-durable truth — session history, the approval store, the input
/// queue — so a durable payload only has to identify which attempt this is and prove that a replay describes the
/// same one.
/// </para>
/// <para>
/// Encoding is deliberately narrow: identities, counts, and fingerprints only. Prompts, tool arguments, model
/// output, retrieved content, and raw paths are content and never enter a durable payload through this helper.
/// </para>
/// <para>
/// The helper is stateless and safe for concurrent use. It is a shared encoding convention rather than a
/// replaceable policy, so it carries no interface: a consumer that needs different persisted bytes registers its
/// own <see cref="IDurableOperationCodec{TState}"/> instead.
/// </para>
/// </remarks>
public static class DurableBoundaryPayload
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
    };

    /// <summary>Gets the schema version stamped on every payload this helper writes.</summary>
    /// <value>The single version that <see cref="Decode{TState}"/> accepts; changing the encoded shape requires a new version.</value>
    public static SchemaVersion Schema { get; } = new("agentkit.durability.boundary-state.v1");

    /// <summary>Encodes one boundary manifest into a versioned payload.</summary>
    /// <typeparam name="TState">The immutable manifest shape being journaled. It must be serializable without runtime references.</typeparam>
    /// <param name="state">The non-null manifest to encode.</param>
    /// <returns>A payload carrying <see cref="Schema"/> and the canonical UTF-8 encoding of <paramref name="state"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException"><typeparamref name="TState"/> cannot be serialized by the shared options.</exception>
    public static OperationPayload Encode<TState>(TState state)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(state);
        return new OperationPayload(Schema, [.. JsonSerializer.SerializeToUtf8Bytes(state, _options)]);
    }

    /// <summary>Decodes one boundary manifest from a payload this helper wrote.</summary>
    /// <typeparam name="TState">The immutable manifest shape being read.</typeparam>
    /// <param name="payload">The non-null payload to decode.</param>
    /// <returns>The decoded manifest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The payload carries a different schema version, or its bytes decode to no manifest. Changed code never
    /// reinterprets older bytes under newer rules.
    /// </exception>
    public static TState Decode<TState>(OperationPayload payload)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload.SchemaVersion != Schema
            ? throw new InvalidOperationException(
                "The durable payload was written under a different boundary state schema version.")
            : JsonSerializer.Deserialize<TState>(payload.Data.AsSpan(), _options)
                ?? throw new InvalidOperationException("The durable payload carries no readable boundary state.");
    }
}
