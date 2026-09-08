// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A request to create a new branch forking from an exact point in an
/// existing branch, leaving the original branch unchanged.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Editing an earlier message, changing direction,
/// or reverting is always represented as a new branch through this request,
/// never as an in-place rewrite of <see cref="ParentBranchId"/>.
/// </remarks>
public sealed record SessionBranchRequest
{
    /// <summary>Initializes a new instance of the <see cref="SessionBranchRequest"/> record.</summary>
    /// <param name="context">The operation context for this branch creation.</param>
    /// <param name="parentBranchId">The existing branch to fork from.</param>
    /// <param name="atSequence">
    /// The exact sequence in <paramref name="parentBranchId"/> the new
    /// branch forks from; entries after this sequence are not visible on
    /// the new branch.
    /// </param>
    /// <param name="idempotencyKey">
    /// The key that makes repeating this exact request safe: a retry with
    /// the same key returns the originally created branch.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public SessionBranchRequest(
        SessionOperationContext context,
        BranchId parentBranchId,
        SessionSequence atSequence,
        IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(context);

        Context = context;
        ParentBranchId = parentBranchId;
        AtSequence = atSequence;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the operation context for this branch creation.</summary>
    public SessionOperationContext Context { get; }

    /// <summary>Gets the existing branch to fork from.</summary>
    public BranchId ParentBranchId { get; }

    /// <summary>
    /// Gets the exact sequence in <see cref="ParentBranchId"/> the new
    /// branch forks from.
    /// </summary>
    public SessionSequence AtSequence { get; }

    /// <summary>
    /// Gets the key that makes repeating this exact request safe.
    /// </summary>
    public IdempotencyKey IdempotencyKey { get; }
}
