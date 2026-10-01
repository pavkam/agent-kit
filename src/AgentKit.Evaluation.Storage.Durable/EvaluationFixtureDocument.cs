// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationFixtureReference"/>.</summary>
/// <param name="Key">The fixture name.</param>
/// <param name="Version">The fixture revision label.</param>
internal sealed record EvaluationFixtureDocument(string Key, string Version)
{
    /// <summary>Converts a fixture reference to its persisted form.</summary>
    /// <param name="value">The non-null reference.</param>
    /// <returns>The document.</returns>
    internal static EvaluationFixtureDocument FromDomain(EvaluationFixtureReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Key, value.Version);
    }

    /// <summary>Restores the reference, re-running its validation.</summary>
    /// <returns>The reference.</returns>
    internal EvaluationFixtureReference ToDomain() => new(Key, Version);
}
