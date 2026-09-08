// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed immutable proposal returned by an <see cref="IRunContinuationPolicy"/>.</summary>
public abstract record RunContinuationDecision
{
    private protected RunContinuationDecision() { }
}
