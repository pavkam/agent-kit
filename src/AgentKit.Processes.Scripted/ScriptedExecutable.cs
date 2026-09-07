// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Maps one configured executable reference to deterministic canonical resolution evidence.</summary>
public sealed record ScriptedExecutable
{
    /// <summary>Initializes one scripted executable mapping.</summary>
    /// <param name="reference">The exact reference accepted from requests.</param>
    /// <param name="absolutePath">The absolute canonical path projected into resolved evidence.</param>
    /// <param name="fingerprint">The deterministic executable-content fingerprint.</param>
    /// <exception cref="ArgumentException">A reference is blank or <paramref name="absolutePath"/> is blank or relative.</exception>
    public ScriptedExecutable(string reference, string absolutePath, ContentHash fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        if (!Path.IsPathRooted(absolutePath))
        {
            throw new ArgumentException("The scripted executable path must be absolute.", nameof(absolutePath));
        }

        Reference = reference;
        AbsolutePath = absolutePath;
        Fingerprint = fingerprint;
    }

    /// <summary>Gets the exact request reference.</summary>
    public string Reference { get; }
    /// <summary>Gets the deterministic absolute resolved path.</summary>
    public string AbsolutePath { get; }
    /// <summary>Gets the deterministic executable fingerprint.</summary>
    public ContentHash Fingerprint { get; }
}
