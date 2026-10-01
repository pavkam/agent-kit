// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using System.Text.Json;

/// <summary>Stamps and reads a goal's delegation depth on its extension data.</summary>
/// <remarks>The coordinator is the only writer of goals in a delegation tree, so depth is recorded once at creation and never recomputed by walking ancestors; a goal with no stamp is a root at depth zero.</remarks>
internal static class GoalDepth
{
    private const string _key = "agentkit.goals.depth";

    /// <summary>Reads the depth stamped on a goal.</summary>
    /// <param name="extensions">The goal's extension data.</param>
    /// <returns>The stamped depth, or zero when none is stamped.</returns>
    internal static int Read(ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        return extensions.Values.TryGetValue(_key, out var value)
            && JsonSerializer.Deserialize<int>(value.CanonicalJson.AsSpan()) is var depth and >= 0
            ? depth
            : 0;
    }

    /// <summary>Returns extension data carrying a depth stamp.</summary>
    /// <param name="extensions">The extension data to extend.</param>
    /// <param name="depth">The non-negative depth to stamp.</param>
    /// <returns>New extension data with the stamp set.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is negative.</exception>
    internal static ExtensionData Stamp(ExtensionData extensions, int depth)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        return new ExtensionData(extensions.Values.SetItem(_key, new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(depth)])));
    }
}
