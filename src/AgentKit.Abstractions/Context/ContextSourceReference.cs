// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>References one exact immutable publication of a context source.</summary>
/// <remarks>The reference retains provenance only; resolving it does not grant authority or elevate content trust.</remarks>
public sealed record ContextSourceReference
{
    /// <summary>Creates an exact source reference from its namespace, key, and published version.</summary>
    /// <param name="sourceNamespace">The nondefault namespace that owns the source key.</param>
    /// <param name="key">The nondefault key within the owning namespace.</param>
    /// <param name="version">The nondefault exact source revision.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sourceNamespace"/>, <paramref name="key"/>, or <paramref name="version"/> is default.
    /// </exception>
    public ContextSourceReference(
        ContextSourceNamespace sourceNamespace,
        ContextSourceKey key,
        ContextSourceVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceNamespace, default);
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);

        Namespace = sourceNamespace;
        Key = key;
        Version = version;
    }

    /// <summary>Gets the namespace that owns the source key.</summary>
    /// <value>A nondefault immutable namespace.</value>
    public ContextSourceNamespace Namespace { get; }

    /// <summary>Gets the source key within its namespace.</summary>
    /// <value>A nondefault immutable key.</value>
    public ContextSourceKey Key { get; }

    /// <summary>Gets the exact published source revision.</summary>
    /// <value>A nondefault immutable version.</value>
    public ContextSourceVersion Version { get; }
}
