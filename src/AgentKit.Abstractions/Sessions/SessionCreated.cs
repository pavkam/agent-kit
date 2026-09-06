// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The session was created, or an identical prior request with the same
/// idempotency key already created it.
/// </summary>
public sealed record SessionCreated: SessionCreateResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionCreated"/> record.</summary>
    /// <param name="descriptor">The created (or previously created) session's descriptor.</param>
    /// <param name="existing">
    /// <see langword="true"/> when this result reflects a prior creation
    /// found through the supplied idempotency key rather than a brand-new
    /// session.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public SessionCreated(SessionDescriptor descriptor, bool existing)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
        Existing = existing;
    }

    /// <summary>Gets the created (or previously created) session's descriptor.</summary>
    public SessionDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets a value indicating whether this result reflects a prior
    /// creation found through the supplied idempotency key.
    /// </summary>
    public bool Existing { get; init; }
}
