// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports reuse of an input identity with nonequivalent canonical content.</summary>
public sealed record InputConflict: InputAdmissionResult
{
    /// <summary>Initializes a conflict.</summary><param name="inputId">The conflicting identity.</param><param name="safeReason">Content-free explanation.</param>
    public InputConflict(InputId inputId, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(inputId, default); ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        InputId = inputId; SafeReason = safeReason;
    }
    /// <summary>Gets conflicting input.</summary><value>The nondefault caller identity.</value>
    public InputId InputId { get; }
    /// <summary>Gets safe explanation.</summary><value>Content-free text.</value>
    public string SafeReason { get; }
}
