// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The session was created and its immutable creation receipt was recorded.
/// An identical retry returns that original receipt without changing it.
/// </summary>
public sealed record SessionCreated: SessionCreateResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionCreated"/> record.</summary>
    /// <param name="descriptor">The descriptor captured when creation first succeeded.</param>
    /// <param name="existing">
    /// Whether the original creation receipt represented an already-existing
    /// session. An idempotency replay preserves this original value; it does
    /// not describe the retry that retrieved the receipt.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public SessionCreated(SessionDescriptor descriptor, bool existing)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
        Existing = existing;
    }

    /// <summary>Gets the descriptor captured when the original creation receipt was recorded.</summary>
    public SessionDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets whether the original creation receipt represented an existing
    /// session. A replay does not change this value.
    /// </summary>
    public bool Existing { get; init; }
}
