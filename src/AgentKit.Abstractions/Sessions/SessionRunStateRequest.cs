// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests authorized recovery loading of one exact lane operation.</summary>
public sealed record SessionRunStateRequest
{
    /// <summary>Initializes a state-load request.</summary><param name="context">The lane-bound in-run context.</param><exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception><exception cref="ArgumentException">The context is not lane-bound or in-run.</exception>
    public SessionRunStateRequest(SessionOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentException.ThrowIfSessionContextNotLaneBound(context);
        if (context.Correlation is not InRunOperationCorrelation)
        {
            throw new ArgumentException("Run-state loading requires in-run correlation.", nameof(context));
        }
        Context = context;
    }
    /// <summary>Gets the exact context.</summary><value>A lane-bound in-run context used to prevent stale recovery.</value>
    public SessionOperationContext Context { get; }
}
