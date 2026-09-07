// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Selects one registered recovery policy, which classifies durable evidence
/// into a decision such as start, reconcile, retry, commit-only, require
/// operator action, or fail.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal, textual)
/// equality over <see cref="Value"/>. It carries no mutable state and is safe
/// to share and compare across threads without synchronization.
/// </para>
/// <para>
/// Recovery policy is captured on the durable record so that an operation is
/// classified under the rules it started with. Applying a newer, more
/// permissive policy to older evidence could retry a non-idempotent effect
/// that the original configuration deliberately routed to an operator.
/// </para>
/// </remarks>
public readonly record struct RecoveryPolicyKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryPolicyKey"/>
    /// struct, validating that it carries usable key text.
    /// </summary>
    /// <param name="value">The non-empty canonical recovery policy key.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public RecoveryPolicyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical recovery policy key text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical recovery policy key text, suitable for logging
    /// and composition-validation messages.
    /// </summary>
    public override string ToString() => Value;
}
