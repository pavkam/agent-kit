// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;
/// <summary>Reports that the response was newly recorded as the terminal resolution for its request.</summary>
public sealed record ApprovalResolved: ApprovalResolutionResult
{
    /// <summary>Initializes a resolved result.</summary><param name="response">The non-null newly recorded terminal response.</param><exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public ApprovalResolved(ApprovalResponse response) { ArgumentNullException.ThrowIfNull(response); Response = response; }
    /// <summary>Gets the newly recorded terminal response.</summary>
    public ApprovalResponse Response { get; }
}
