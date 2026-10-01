// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationModelUse"/>.</summary>
/// <param name="Provider">The provider identity.</param>
/// <param name="ApiFamily">The wire API family.</param>
/// <param name="Model">The model identity.</param>
/// <param name="Deployment">The deployment identity, or <see langword="null"/>.</param>
internal sealed record EvaluationModelUseDocument(string Provider, string ApiFamily, string Model, string? Deployment)
{
    /// <summary>Converts a model use to its persisted form.</summary>
    /// <param name="value">The non-null model use.</param>
    /// <returns>The document.</returns>
    internal static EvaluationModelUseDocument FromDomain(EvaluationModelUse value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Provider, value.ApiFamily, value.Model, value.Deployment);
    }

    /// <summary>Restores the model use, re-running its validation.</summary>
    /// <returns>The model use.</returns>
    internal EvaluationModelUse ToDomain() => new(Provider, ApiFamily, Model, Deployment);
}
