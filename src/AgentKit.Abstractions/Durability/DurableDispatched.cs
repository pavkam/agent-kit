// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an external owner accepted durable work.</summary>
public sealed record DurableDispatched: DurableDispatchResult
{
    /// <summary>Initializes a successful dispatch.</summary>
    /// <param name="externalReference">The non-null external owner handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="externalReference"/> is null.</exception>
    public DurableDispatched(ExternalOperationReference externalReference)
    {
        ArgumentNullException.ThrowIfNull(externalReference);
        ExternalReference = externalReference;
    }

    /// <summary>Gets the external owner handle.</summary>
    public ExternalOperationReference ExternalReference { get; }
}
