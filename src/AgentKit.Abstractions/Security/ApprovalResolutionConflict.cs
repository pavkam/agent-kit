// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that the request is already bound to different terminal evidence than this response.</summary>
public sealed record ApprovalResolutionConflict: ApprovalResolutionResult
{
    /// <summary>Initializes a conflict result.</summary><param name="safeReason">The nonblank caller-safe reason.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public ApprovalResolutionConflict(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets the caller-safe conflict reason.</summary>
    public string SafeReason { get; }
}
