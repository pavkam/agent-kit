// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Converts one operation's typed state to and from a versioned durable
/// payload.
/// </summary>
/// <typeparam name="TState">
/// The typed state this codec serializes. It must be plain data.
/// </typeparam>
/// <remarks>
/// <para>
/// Implementations must be thread-safe, deterministic, and free of ambient
/// state. They are normally registered as singletons and are selected by the
/// <see cref="OperationName"/> and <see cref="Version"/> pair.
/// </para>
/// <para>
/// A codec serializes data, never behavior. Runtime objects, service scopes,
/// tasks, cancellation sources, delegates, credentials, and provider clients
/// must never be written into a payload; a recovering worker rebuilds those
/// from its own validated composition.
/// </para>
/// <para>
/// Round-tripping preserves unknown but compatible fields. A newer writer and
/// an older reader will coexist during deployment, and silently dropping
/// fields the reader does not recognize corrupts state that the writer will
/// later expect to find.
/// </para>
/// </remarks>
public interface IDurableOperationCodec<TState>
{
    /// <summary>
    /// Gets the operation name this codec serializes state for.
    /// </summary>
    public DurableOperationName OperationName { get; }

    /// <summary>
    /// Gets the contract version this codec reads and writes.
    /// </summary>
    /// <value>
    /// Registering two codecs with the same
    /// <see cref="OperationName"/> and version is a composition error, since
    /// the selection would be ambiguous.
    /// </value>
    public DurableOperationVersion Version { get; }

    /// <summary>
    /// Encodes typed state into a durable payload.
    /// </summary>
    /// <param name="value">The state to serialize.</param>
    /// <returns>
    /// A payload stamped with the schema version it was written under.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> contains data this codec cannot represent.
    /// </exception>
    public OperationPayload Encode(TState value);

    /// <summary>
    /// Decodes a durable payload back into typed state.
    /// </summary>
    /// <param name="payload">The recorded payload.</param>
    /// <returns>
    /// <see cref="DurableDecoded{TState}"/> when the payload is understood,
    /// or <see cref="DurableDecodeIncompatible{TState}"/> when it was written
    /// under a version this codec cannot interpret.
    /// </returns>
    /// <remarks>
    /// An unreadable payload returns the incompatible result rather than
    /// throwing, because deploying new code over existing durable state is an
    /// expected condition that recovery must handle deliberately.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="payload"/> is <see langword="null"/>.
    /// </exception>
    public DurableDecodeResult<TState> Decode(OperationPayload payload);
}
