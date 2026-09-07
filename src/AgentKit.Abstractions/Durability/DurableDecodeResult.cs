// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the outcome of decoding one durable payload back
/// into typed operation state.
/// </summary>
/// <typeparam name="TState">
/// The typed state the payload decodes into.
/// </typeparam>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="DurableDecoded{TState}"/> and
/// <see cref="DurableDecodeIncompatible{TState}"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a third kind.
/// </remarks>
public abstract record DurableDecodeResult<TState>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableDecodeResult{TState}"/> record. This constructor is
    /// <see langword="private protected"/> so only the closed set of kinds
    /// declared in this assembly can extend the hierarchy.
    /// </summary>
    private protected DurableDecodeResult()
    {
    }
}
