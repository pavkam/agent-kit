// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Computes the canonical payload fingerprint that makes input admission idempotent.</summary>
/// <remarks>The fingerprint identifies exact immutable payload content, including part order and extension evidence. It is replay-comparison evidence only: it authorizes nothing, proves no admission occurred, and never exports payload bytes.</remarks>
public static class InputPayloadFingerprint
{
    /// <summary>Computes the canonical fingerprint of one immutable caller payload.</summary>
    /// <param name="input">The non-null immutable caller input.</param>
    /// <returns>An algorithm-qualified digest that is equal for structurally equal payloads and differs when any retained content, order, or extension differs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public static InputFingerprint Create(AgentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return SecurityCanonicalFingerprint.Create(input);
    }
}
