// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.Options;

/// <summary>Selects the effective policy-snapshot reference that must evaluate one normalized security request.</summary>
/// <remarks>
/// Captured requests select the snapshot carried by their authorization evidence. Uncaptured requests select the
/// composition-bound snapshot when configured, otherwise the synthetic uncaptured reference retained by the catalog.
/// </remarks>
public sealed class DefaultSecurityPolicySelector: ISecurityPolicySelector
{
    private readonly ISecurityPolicyCatalog _catalog;
    private readonly SecurityPolicySnapshotReference? _boundSnapshot;
    private readonly SecurityPolicySnapshotReference _uncapturedSnapshot;

    /// <summary>Initializes a selector over the composition's frozen policy catalog.</summary>
    /// <param name="catalog">The catalog that retains effective-policy snapshot references.</param>
    /// <param name="options">The permission options captured when the catalog was constructed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="options"/> is null.</exception>
    public DefaultSecurityPolicySelector(
        ISecurityPolicyCatalog catalog,
        IOptions<AgentPermissionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        var optionValues = options.Value;
        ArgumentNullException.ThrowIfNull(optionValues);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(optionValues.PolicyVersion);

        _catalog = catalog;
        _boundSnapshot = optionValues.PolicySnapshot;
        _uncapturedSnapshot = SecurityPolicyEvaluationContexts.CreateUncapturedReference(
            new SecurityPolicyVersion(optionValues.PolicyVersion));
    }

    /// <inheritdoc/>
    public ValueTask<SecurityPolicySnapshotResult> SelectAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var reference = request.Authorization?.PolicySnapshot
            ?? _boundSnapshot
            ?? _uncapturedSnapshot;
        return _catalog.ResolveAsync(reference, cancellationToken);
    }
}
