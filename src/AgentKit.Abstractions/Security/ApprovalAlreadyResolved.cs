// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that the exact same response was already recorded as the terminal resolution, idempotently.</summary>
public sealed record ApprovalAlreadyResolved: ApprovalResolutionResult
{
    /// <summary>Initializes an already-resolved result.</summary><param name="response">The non-null previously recorded terminal response.</param><exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public ApprovalAlreadyResolved(ApprovalResponse response) { ArgumentNullException.ThrowIfNull(response); Response = response; }
    /// <summary>Gets the previously recorded terminal response.</summary>
    public ApprovalResponse Response { get; }
}
