// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests an authorized idempotency lookup before input preprocessing or capacity reservation.</summary>
public sealed record SessionInputLookupRequest
{
    /// <summary>Initializes a lookup for one original caller input.</summary>
    /// <param name="context">The lane-bound before-run session context.</param>
    /// <param name="input">The immutable original caller input.</param>
    /// <param name="originalFingerprint">The canonical fingerprint computed during non-effecting input preflight.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The context is not lane-bound or its correlation is not before-run.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="originalFingerprint"/> is default.</exception>
    public SessionInputLookupRequest(SessionOperationContext context, AgentInput input, InputFingerprint originalFingerprint)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfEqual(originalFingerprint, default);
        ArgumentException.ThrowIfSessionContextNotLaneBound(context);
        ArgumentException.ThrowIfSessionContextNotBeforeRun(context);
        Context = context;
        Input = input;
        OriginalFingerprint = originalFingerprint;
    }

    /// <summary>Gets the exact session operation context.</summary><value>A lane-bound before-run context with captured authorization.</value>
    public SessionOperationContext Context { get; }
    /// <summary>Gets the original caller input.</summary><value>The immutable input compared with a retained admission.</value>
    public AgentInput Input { get; }
    /// <summary>Gets the canonical original-input fingerprint.</summary><value>The digest used for authorization and replay comparison.</value>
    public InputFingerprint OriginalFingerprint { get; }
}
