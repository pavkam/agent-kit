// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that resolution could not complete against the authoritative approval store.</summary>
public sealed record ApprovalResolutionUnavailable: ApprovalResolutionResult
{
    /// <summary>Initializes an unavailable result.</summary><param name="safeReason">The nonblank caller-safe reason.</param><exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public ApprovalResolutionUnavailable(string safeReason) { ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); SafeReason = safeReason; }
    /// <summary>Gets the caller-safe unavailable reason.</summary>
    public string SafeReason { get; }
}
