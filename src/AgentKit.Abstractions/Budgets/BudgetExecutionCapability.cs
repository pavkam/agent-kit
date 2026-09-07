// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one live budget scope to the exact profile, identity, and operation authorized for an invocation.</summary>
/// <remarks>
/// This invocation-only value is never serialized or retained beyond the operation. The consumer neither owns nor disposes
/// <see cref="Scope"/>; the component that created the scope retains its lifetime and reconciliation responsibilities.
/// </remarks>
public sealed record BudgetExecutionCapability
{
    /// <summary>Initializes an exact live budget execution binding.</summary>
    /// <param name="profileKey">The selected nondefault budget profile.</param>
    /// <param name="profileVersion">The selected positive immutable profile revision.</param>
    /// <param name="identity">The authenticated execution identity whose tenant and principal own the scope.</param>
    /// <param name="correlation">The exact operation and run-stage correlation for this invocation.</param>
    /// <param name="scope">The nondefault live scope whose address matches the identity and correlation.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="profileKey"/> is default, or <paramref name="identity"/>, <paramref name="correlation"/>,
    /// <paramref name="scope"/>, or the scope address is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="profileKey"/> is blank, or the scope address does not match the identity, operation, or correlation stage.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="profileVersion"/> is not positive or the scope identity is default.
    /// </exception>
    public BudgetExecutionCapability(
        BudgetProfileKey profileKey,
        BudgetProfileVersion profileVersion,
        ExecutionIdentity identity,
        OperationCorrelation correlation,
        IBudgetScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(scope);
        var scopeId = scope.Id;
        var address = scope.Address;
        ArgumentOutOfRangeException.ThrowIfEqual(scopeId, default, nameof(scope));
        ArgumentNullException.ThrowIfNull(address, nameof(scope));
        ArgumentException.ThrowIfInvalidBudgetExecutionBinding(address, identity, correlation, nameof(scope));

        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        Identity = identity;
        Correlation = correlation;
        Scope = scope;
    }

    /// <summary>Gets the selected immutable budget-profile key.</summary>
    public BudgetProfileKey ProfileKey { get; }

    /// <summary>Gets the selected positive budget-profile revision.</summary>
    public BudgetProfileVersion ProfileVersion { get; }

    /// <summary>Gets the authenticated identity bound to the live scope.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the exact operation and run-stage correlation.</summary>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the borrowed live budget scope; consumers must not dispose or retain it.</summary>
    public IBudgetScope Scope { get; }
}
