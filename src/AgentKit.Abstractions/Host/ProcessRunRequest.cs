// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests execution of one resolved process intent under an exact bounded grant.</summary>
public sealed record ProcessRunRequest
{
    /// <summary>Initializes an authorized process run request.</summary>
    /// <param name="intent">The canonical resolved intent.</param>
    /// <param name="grant">The exact single-use process authority.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ProcessRunRequest(ResolvedProcessIntent intent, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(grant);
        Intent = intent;
        Grant = grant;
    }

    /// <summary>Gets the canonical resolved intent.</summary>
    public ResolvedProcessIntent Intent { get; }
    /// <summary>Gets the exact single-use process authority.</summary>
    public SecurityGrant Grant { get; }
}
