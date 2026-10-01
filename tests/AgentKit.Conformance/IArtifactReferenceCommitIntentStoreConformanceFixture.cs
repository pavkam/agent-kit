// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates one isolated reference-commit intent store through the implementation's normal composition path.</summary>
/// <remarks>The fixture owns every store and resource it creates. Disposing it releases them, including any durable root or database file.</remarks>
public interface IArtifactReferenceCommitIntentStoreConformanceFixture: IAsyncDisposable
{
    /// <summary>Creates the public intent-store contract over empty state.</summary>
    /// <param name="cancellationToken">Cancels fixture composition before it completes.</param>
    /// <returns>The fixture-owned store under test; repeated calls return the same store.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<IArtifactReferenceCommitIntentStore> CreateAsync(CancellationToken cancellationToken = default);
}
