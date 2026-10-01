// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.Evaluation;

/// <summary>Supplies one isolated evaluation result store composed through its public registration.</summary>
/// <remarks>The fixture is constructed per case and owns every store and storage root it creates, releasing them when the case disposes it.</remarks>
public interface IEvaluationResultStoreConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets the capabilities the adapter claims; durability cases run only when it claims durability.</summary>
    public ConformanceCapabilities Capabilities { get; }

    /// <summary>Gets the store under test.</summary>
    public IEvaluationResultStore Store { get; }

    /// <summary>Opens a fresh store instance over the same storage, as a restarted host would.</summary>
    /// <param name="cancellationToken">Cancels reopening.</param>
    /// <returns>A new store over the persisted state, or the same store when the adapter is ephemeral.</returns>
    public ValueTask<IEvaluationResultStore> ReopenAsync(CancellationToken cancellationToken = default);
}
