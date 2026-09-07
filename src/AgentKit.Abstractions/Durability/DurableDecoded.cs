// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The payload was understood and produced typed operation state.
/// </summary>
/// <typeparam name="TState">The decoded state type.</typeparam>
public sealed record DurableDecoded<TState>: DurableDecodeResult<TState>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableDecoded{TState}"/> record.
    /// </summary>
    /// <param name="state">
    /// The decoded state. A <see langword="null"/> value is permitted only
    /// when <typeparamref name="TState"/> is itself nullable, because some
    /// operations legitimately carry no state.
    /// </param>
    public DurableDecoded(TState state) => State = state;

    /// <summary>Gets the decoded operation state.</summary>
    public TState State { get; init; }
}
