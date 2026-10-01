// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Selects the effective policy-snapshot reference that must evaluate one normalized security request.</summary>
/// <remarks>
/// Every request selects the snapshot carried by its captured authorization evidence; there is no fallback to a
/// composition-bound or synthetic snapshot.
/// </remarks>
public sealed class DefaultSecurityPolicySelector: ISecurityPolicySelector
{
    private readonly ISecurityPolicyCatalog _catalog;

    /// <summary>Initializes a selector over the composition's frozen policy catalog.</summary>
    /// <param name="catalog">The catalog that retains effective-policy snapshot references.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
    public DefaultSecurityPolicySelector(ISecurityPolicyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityPolicySnapshotResult> SelectAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return _catalog.ResolveAsync(request.Authorization.PolicySnapshot, cancellationToken);
    }
}
