// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

/// <summary>Computes a deterministic <see cref="RunPolicyVersion"/> from the run-scoped inputs every caller can see.</summary>
/// <remarks>
/// <para>
/// No first-party component publishes a versioned run-policy snapshot yet (see
/// <c>docs/architecture/agent-runtime.md</c>). This is the shared core: a version derived from exactly the
/// run's effective turn limit, attempt timeout, and selected continuation-policy key — the facts an admission
/// boundary composing a durable <see cref="RunConfigurationReference"/> has, without depending on the concrete
/// loop package for its live <c>AgentLoopOptions</c>. <c>AgentKit.Loop</c>'s own
/// <c>RunPolicyVersioning.Compute</c> composes this core value with its additionally-visible resolved loop
/// options, so the two intentionally overlap on these three inputs rather than duplicate independent logic.
/// </para>
/// <para>
/// The result is content-derived rather than a monotonically issued sequence number, so it requires no mutable
/// registry, is reproducible across process restarts, and agrees across every process observing the identical
/// inputs without coordination.
/// </para>
/// </remarks>
public static class RunPolicyVersioning
{
    /// <summary>Computes the version identifying one exact effective continuation policy's admission-visible inputs.</summary>
    /// <param name="maxTurns">The run's exact effective turn limit, after any narrowing.</param>
    /// <param name="attemptTimeout">The run's exact effective attempt timeout, after any narrowing.</param>
    /// <param name="continuationPolicyKey">The exact selected <see cref="IRunContinuationPolicy"/> registration key.</param>
    /// <param name="allowedTools">The run's tool allow-list, or <see langword="null"/> when the run exposes its whole tool surface. Order is irrelevant.</param>
    /// <param name="budgetParentScopeId">The existing budget scope the run's scope is created beneath, or <see langword="null"/> for a root run scope.</param>
    /// <returns>
    /// A positive version that is identical for two calls with identical arguments and differs otherwise. A run with neither
    /// restriction yields the same version it would have without these parameters.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTurns"/> is not positive, or <paramref name="attemptTimeout"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="continuationPolicyKey"/> is blank.</exception>
    public static RunPolicyVersion Compute(
        int maxTurns,
        TimeSpan attemptTimeout,
        ComponentKey<IRunContinuationPolicy> continuationPolicyKey,
        ImmutableArray<ToolId>? allowedTools = null,
        BudgetScopeId? budgetParentScopeId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(continuationPolicyKey.Value, nameof(continuationPolicyKey));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendInt64(hash, maxTurns);
        AppendInt64(hash, attemptTimeout.Ticks);
        AppendText(hash, continuationPolicyKey.Value);
        if (allowedTools is { } tools)
        {
            AppendInt64(hash, 1);
            AppendInt64(hash, tools.Length);
            foreach (var tool in tools.Select(static tool => tool.Value ?? string.Empty).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                AppendText(hash, tool);
            }
        }

        if (budgetParentScopeId is { } parent)
        {
            AppendInt64(hash, 2);
            AppendText(hash, parent.ToString());
        }

        Span<byte> digest = stackalloc byte[32];
        _ = hash.GetHashAndReset(digest);
        var raw = BinaryPrimitives.ReadInt64BigEndian(digest) & long.MaxValue;
        return new RunPolicyVersion(raw == 0 ? 1 : raw);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendText(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt64(hash, bytes.Length);
        hash.AppendData(bytes);
    }
}
