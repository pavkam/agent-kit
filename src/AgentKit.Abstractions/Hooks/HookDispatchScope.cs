// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The explicit, immutable record of which hook points are currently active
/// on the call path leading to one dispatch, used to detect and bound
/// reentrant dispatch without relying on async-local or other ambient
/// state.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="ActiveDepths"/>, safe to share across threads without
/// synchronization. A caller starts a fresh call chain from
/// <see cref="Root"/> and threads the scope returned by
/// <see cref="Entering"/> into every nested dispatch it performs from
/// within a hook, so a dispatcher can compare the depth already recorded
/// for a point against the caller's configured maximum before invoking that
/// point's hooks again.
/// </para>
/// <para>
/// Because this state is passed explicitly by value rather than captured
/// ambiently, two unrelated top-level dispatches — even running
/// concurrently on the same thread pool — never observe or influence each
/// other's reentrancy depth.
/// </para>
/// </remarks>
public sealed record HookDispatchScope
{
    private HookDispatchScope(ImmutableDictionary<HookPointId, int> activeDepths) => ActiveDepths = activeDepths;

    /// <summary>Gets a fresh scope with no active hook points, for starting a new top-level dispatch chain.</summary>
    public static HookDispatchScope Root { get; } = new([]);

    /// <summary>Gets the current active dispatch depth recorded for each hook point on this call path.</summary>
    public ImmutableDictionary<HookPointId, int> ActiveDepths { get; }

    /// <summary>Gets the current active dispatch depth recorded for one hook point.</summary>
    /// <param name="point">The hook point to look up.</param>
    /// <returns>The number of dispatches for <paramref name="point"/> currently active on this call path.</returns>
    public int DepthOf(HookPointId point) => ActiveDepths.TryGetValue(point, out var depth) ? depth : 0;

    /// <summary>Returns a new scope reflecting one more active dispatch for the given hook point.</summary>
    /// <param name="point">The hook point being entered.</param>
    /// <returns>A new scope with <paramref name="point"/>'s recorded depth incremented by one.</returns>
    public HookDispatchScope Entering(HookPointId point) => new(ActiveDepths.SetItem(point, DepthOf(point) + 1));

    /// <inheritdoc/>
    public bool Equals(HookDispatchScope? other)
    {
        if (other is null || ActiveDepths.Count != other.ActiveDepths.Count)
        {
            return false;
        }

        foreach (var (point, depth) in ActiveDepths)
        {
            if (!other.ActiveDepths.TryGetValue(point, out var otherDepth) || depth != otherDepth)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var (point, depth) in ActiveDepths.OrderBy(static kvp => kvp.Key.Value, StringComparer.Ordinal))
        {
            hash.Add(point);
            hash.Add(depth);
        }

        return hash.ToHashCode();
    }
}
