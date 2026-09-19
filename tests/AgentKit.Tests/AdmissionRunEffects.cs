// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Captures scope construction and loop requests for admission-boundary assertions.</summary>
internal sealed class AdmissionRunEffects
{
    /// <summary>Gets how many run scopes resolved their loop.</summary>
    public int Scopes { get; set; }

    /// <summary>Gets loop requests in the order they were received.</summary>
    public List<AgentLoopRunRequest> Requests { get; } = [];

    /// <summary>Gets or sets the exception returned by the scoped loop after recording a request.</summary>
    public Exception? LoopException { get; set; }
}
