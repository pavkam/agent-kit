// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// A catalog test double returning one fixed snapshot.
/// </summary>
internal sealed class FakeModelCatalog(ModelCatalogSnapshot snapshot): IModelCatalog
{
    /// <summary>Gets the number of times the loop read the catalog.</summary>
    public int ReadCount { get; private set; }

    public ValueTask<ModelCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        return ValueTask.FromResult(snapshot);
    }
}

/// <summary>
/// A selector test double returning a scripted selection outcome.
/// </summary>
internal sealed class FakeModelSelector(ModelSelectionResult result): IModelSelector
{
    /// <summary>Gets the number of times the loop asked for a selection.</summary>
    public int SelectCount { get; private set; }

    /// <summary>Creates a selector that always chooses <paramref name="model"/>.</summary>
    public static FakeModelSelector Selecting(ModelDescriptor model) =>
        new(new ModelSelected(new ModelSelectionDecision(
            model,
            new ModelCatalogVersion(1),
            "test selection",
            [])));

    public ValueTask<ModelSelectionResult> SelectAsync(
        ModelSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        SelectCount++;
        return ValueTask.FromResult(result);
    }
}

/// <summary>
/// A resolver test double mapping one alias to one adapter.
/// </summary>
internal sealed class FakeLlmModelResolver(ILlmModel? adapter): ILlmModelResolver
{
    public ILlmModel? Resolve(ModelDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return adapter is not null && adapter.Alias == model.Alias ? adapter : null;
    }
}
