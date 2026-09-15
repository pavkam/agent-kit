// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IModelSelector"/> test double that returns one scripted selection outcome and records the exact
/// request it received, so tests can assert the scope, policy, and requirements a component selected with.
/// </summary>
/// <param name="result">The outcome every selection returns.</param>
public sealed class ScriptedModelSelector(ModelSelectionResult result): IModelSelector
{
    /// <summary>Gets the number of selections requested.</summary>
    public int SelectCount { get; private set; }

    /// <summary>Gets the most recent exact selection request, or null before the first selection.</summary>
    public ModelSelectionRequest? LastRequest { get; private set; }

    /// <summary>Creates a selector that always chooses <paramref name="model"/>.</summary>
    /// <param name="model">The descriptor to select.</param>
    /// <returns>A selector whose every outcome is <see cref="ModelSelected"/> for <paramref name="model"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public static ScriptedModelSelector Selecting(ModelDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new(new ModelSelected(new ModelSelectionDecision(model, new ModelCatalogVersion(1), "test selection", [])));
    }

    /// <inheritdoc/>
    public ValueTask<ModelSelectionResult> SelectAsync(
        ModelSelectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        SelectCount++;
        LastRequest = request;
        return ValueTask.FromResult(result);
    }
}
