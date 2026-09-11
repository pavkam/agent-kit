// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Frozen;

/// <summary>Validates and retains a complete immutable source-to-invoker binding graph without taking resource ownership.</summary>
/// <remarks>Multiple independent captures may share this graph; each capture owns its own acquisition state.</remarks>
internal sealed class ToolProviderBindings
{
    /// <summary>Normalizes identity comparison and validates every binding before retaining the immutable graph.</summary>
    /// <param name="snapshot">The nonnull source publication whose exact descriptor identities define the binding keyset.</param>
    /// <param name="invokers">One nonnull borrowed invoker for every descriptor, with no extra, missing, default, or duplicate normalized identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/>, <paramref name="invokers"/>, or an invoker is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A binding key is default.</exception>
    /// <exception cref="ArgumentException">Normalization reveals duplicate identities or the binding keyset differs from the publication.</exception>
    internal ToolProviderBindings(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(invokers);
        var captured = new Dictionary<ToolIdentity, IToolInvoker>();
        foreach (var pair in invokers)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(invokers));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(invokers));
            ArgumentException.ThrowIfNotEqual(captured.TryAdd(pair.Key, pair.Value), true, nameof(invokers));
        }
        ArgumentException.ThrowIfNotEqual(captured.Count, snapshot.Tools.Length, nameof(invokers));
        var bindings = new Dictionary<ToolIdentity, (ToolDescriptor Tool, IToolInvoker Invoker)>();
        foreach (var tool in snapshot.Tools)
        {
            var identity = new ToolIdentity(tool.Id, tool.Version);
            ArgumentException.ThrowIfNotEqual(captured.ContainsKey(identity), true, nameof(invokers));
            bindings.Add(identity, (tool, captured[identity]));
        }
        Entries = bindings.ToFrozenDictionary();
        Snapshot = snapshot;
    }

    /// <summary>Gets the exact immutable source publication.</summary>
    /// <value>The supplied snapshot, without live provider metadata.</value>
    internal ToolProviderSnapshot Snapshot { get; }

    /// <summary>Gets the complete normalized exact-identity binding graph.</summary>
    /// <value>An immutable default-comparer map retaining the captured descriptor and borrowed invoker for every identity.</value>
    internal FrozenDictionary<ToolIdentity, (ToolDescriptor Tool, IToolInvoker Invoker)> Entries { get; }
}
