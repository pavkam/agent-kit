// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes one isolated approval-store implementation for reusable contract tests.</summary>
public interface IApprovalStoreConformanceFixture: IAsyncDisposable
{
    /// <summary>Creates or returns the isolated store.</summary>
    /// <param name="cancellationToken">Cancels fixture creation.</param>
    /// <returns>The store under test.</returns>
    public ValueTask<IApprovalStore> CreateAsync(CancellationToken cancellationToken = default);
}
