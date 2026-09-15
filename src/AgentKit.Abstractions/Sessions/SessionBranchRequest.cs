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
/// never as an in-place rewrite of <see cref="ParentBranchId"/>. A successful fork copies
/// the parent's entries whose sequence is at most <see cref="AtSequence"/> into the new
/// branch's own sequence space, unchanged; the new branch's next append must start at
/// <see cref="AtSequence"/> plus one, and its later sequences are independent of the
/// parent's and of any other sibling branch's.
/// </remarks>
public sealed record SessionBranchRequest
{
    /// <summary>Initializes a new instance of the <see cref="SessionBranchRequest"/> record.</summary>
    /// <param name="context">The operation context for this branch creation.</param>
    /// <param name="parentBranchId">The existing branch to fork from.</param>
    /// <param name="atSequence">
    /// The exact sequence in <paramref name="parentBranchId"/> the new
    /// branch forks from; entries after this sequence are not visible on
    /// the new branch. Zero forks an empty branch. Any other value must be
    /// the sequence of an entry committed on the parent branch itself;
    /// session sequences allocated to sibling branches or beyond the parent
    /// tip are rejected with <see cref="SessionBranchParentNotFound"/>.
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
    /// <value>Zero for an empty fork; otherwise a sequence committed on the parent branch.</value>
    public SessionSequence AtSequence { get; }

    /// <summary>
    /// Gets the key that makes repeating this exact request safe.
    /// </summary>
    public IdempotencyKey IdempotencyKey { get; }
}
