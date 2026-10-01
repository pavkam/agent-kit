// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records one concrete provider model a run actually used, as reported by its usage evidence.</summary>
public sealed record EvaluationModelUse
{
    /// <summary>Initializes a validated model use.</summary>
    /// <param name="provider">The non-blank provider identity.</param>
    /// <param name="apiFamily">The non-blank wire API family.</param>
    /// <param name="model">The non-blank model identity.</param>
    /// <param name="deployment">The deployment identity, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A required text is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A required text is blank, or <paramref name="deployment"/> is present but blank.</exception>
    public EvaluationModelUse(string provider, string apiFamily, string model, string? deployment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (deployment is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(deployment);
        }

        Provider = provider;
        ApiFamily = apiFamily;
        Model = model;
        Deployment = deployment;
    }

    /// <summary>Gets the provider identity.</summary>
    public string Provider { get; }

    /// <summary>Gets the wire API family.</summary>
    public string ApiFamily { get; }

    /// <summary>Gets the model identity.</summary>
    public string Model { get; }

    /// <summary>Gets the deployment identity, or <see langword="null"/>.</summary>
    public string? Deployment { get; }
}
