// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records which versioned dataset fixture one case ran against.</summary>
/// <remarks>AgentKit.Evaluation does not resolve or seed fixtures. The host composes the engine, file system, network, and process fakes that realize a fixture before the plan runs; the reference is recorded evidence that travels with every result so a comparison can tell fixture revisions apart.</remarks>
public sealed record EvaluationFixtureReference
{
    /// <summary>Initializes a validated fixture reference.</summary>
    /// <param name="key">The non-blank stable fixture name.</param>
    /// <param name="version">The non-blank fixture revision label.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="version"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="version"/> is blank.</exception>
    public EvaluationFixtureReference(string key, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Key = key;
        Version = version;
    }

    /// <summary>Gets the stable fixture name.</summary>
    public string Key { get; }

    /// <summary>Gets the fixture revision label.</summary>
    public string Version { get; }
}
