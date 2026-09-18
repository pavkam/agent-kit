// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

/// <summary>Computes a deterministic <see cref="RunPolicyVersion"/> from the exact loop-continuation-relevant inputs.</summary>
/// <remarks>
/// <para>
/// No first-party component publishes a versioned run-policy snapshot yet (see
/// <c>docs/implementation-plan.md</c>, workstream 1): the effective continuation-relevant behavior a run observes
/// is the combination of the definition's narrowed turn limit and attempt timeout, the selected
/// <see cref="IRunContinuationPolicy"/> registration, and the host's live, independently hot-reloadable
/// <see cref="AgentLoopOptions"/>. This type composes the shared admission-visible core
/// (<see cref="AgentKit.RunPolicyVersioning"/>, which an admission boundary outside this package uses too)
/// with this instance's additionally-visible resolved <see cref="AgentLoopOptions"/>, so two evaluations that
/// observe the identical effective policy always agree, and a change to any one input — including an operator's
/// live <see cref="AgentLoopOptions"/> reload that never touches the owning <see cref="AgentDefinition"/> — is
/// reflected in a different version.
/// </para>
/// <para>
/// The result is content-derived rather than a monotonically issued sequence number, so it requires no mutable
/// registry, is reproducible across process restarts, and agrees across every process observing the identical
/// inputs without coordination. It is not a substitute for a published, catalog-validated run-policy profile;
/// it exists so <see cref="RunContinuationContext.PolicyVersion"/> carries a real, non-fabricated value until
/// that profile system exists.
/// </para>
/// </remarks>
public static class RunPolicyVersioning
{
    /// <summary>Computes the version identifying one exact effective continuation policy.</summary>
    /// <param name="maxTurns">The run's exact effective turn limit, after any narrowing.</param>
    /// <param name="attemptTimeout">The run's exact effective attempt timeout, after any narrowing.</param>
    /// <param name="continuationPolicyKey">The exact selected <see cref="IRunContinuationPolicy"/> registration key.</param>
    /// <param name="options">The exact resolved <see cref="AgentLoopOptions"/> this run observes.</param>
    /// <returns>A positive version that is identical for two calls with identical arguments and differs otherwise.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTurns"/> is not positive, or <paramref name="attemptTimeout"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="continuationPolicyKey"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static RunPolicyVersion Compute(
        int maxTurns,
        TimeSpan attemptTimeout,
        ComponentKey<IRunContinuationPolicy> continuationPolicyKey,
        AgentLoopOptions options)
    {
        // Validates maxTurns, attemptTimeout, and continuationPolicyKey identically to the core.
        var core = AgentKit.RunPolicyVersioning.Compute(maxTurns, attemptTimeout, continuationPolicyKey);
        ArgumentNullException.ThrowIfNull(options);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendInt64(hash, core.Value);
        AppendInt64(hash, options.HistoryReadPageSize);
        AppendInt64(hash, options.AppendConflictRetryLimit);
        AppendInt64(hash, options.DisableToolsOnFinalTurn ? 1 : 0);
        AppendInt64(hash, options.SettlementTimeout.Ticks);
        AppendInt64(hash, options.ObserverDeliveryTimeout.Ticks);
        AppendDouble(hash, options.ContextPressureThreshold);
        AppendDouble(hash, options.EstimatedCharactersPerToken);

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

    private static void AppendDouble(IncrementalHash hash, double value) =>
        AppendInt64(hash, BitConverter.DoubleToInt64Bits(value));

    private static void AppendText(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt64(hash, bytes.Length);
        hash.AppendData(bytes);
    }
}
