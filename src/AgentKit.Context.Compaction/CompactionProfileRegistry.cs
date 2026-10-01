// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;

/// <summary>The first-party <see cref="ICompactionProfileCatalog"/> over every profile registered through <c>AddCompactionProfile</c>.</summary>
/// <remarks>
/// Construction is the composition boundary for profiles: <see cref="Build"/> checks every declared profile against the
/// compactor, strategy, and summary-generator registrations it names and compiles one immutable
/// <see cref="CompactionPolicySnapshot"/> per profile. The resulting catalog is read-only, so lookups are safe from any
/// thread and never observe a later registration change.
/// </remarks>
internal sealed class CompactionProfileRegistry: ICompactionProfileCatalog
{
    private readonly FrozenDictionary<CompactionProfileKey, CompactionProfilePublication> _profiles;

    private CompactionProfileRegistry(FrozenDictionary<CompactionProfileKey, CompactionProfilePublication> profiles) =>
        _profiles = profiles;

    /// <inheritdoc/>
    public bool TryGet(CompactionProfileKey key, [NotNullWhen(true)] out CompactionProfilePublication? profile)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        return _profiles.TryGetValue(key, out profile);
    }

    /// <summary>Validates and compiles every registered profile.</summary>
    /// <param name="provider">The provider the registrations resolve from; only declarations and option snapshots are read.</param>
    /// <returns>The immutable catalog.</returns>
    /// <exception cref="InvalidOperationException">
    /// A profile names a compactor with no registration, a strategy that is not registered for that compactor, a strategy
    /// that requires a summary generator that is not registered, oversized-turn repair on a strategy that lacks the
    /// capability, an ordering that contradicts a strategy's before/after constraints, or a cycle among those constraints.
    /// </exception>
    /// <exception cref="OptionsValidationException">A compactor's <see cref="ContextCompactionOptions"/> are invalid.</exception>
    internal static CompactionProfileRegistry Build(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var strategies = provider.GetServices<CompactionStrategyDeclaration>().ToArray();
        var generators = provider.GetServices<CompactionSummaryGeneratorDeclaration>().ToArray();
        var profiles = new Dictionary<CompactionProfileKey, CompactionProfilePublication>();
        foreach (var declaration in provider.GetServices<CompactionProfileDeclaration>())
        {
            var compactorText = declaration.CompactorKey.Value;
            var ceilings = provider.GetKeyedService<ContextCompactionOptionsSnapshot>(compactorText)
                ?? throw new InvalidOperationException(
                    $"Compaction profile '{declaration.Key.Value}' names compactor '{compactorText}', which is not registered. Call AddAgentContextCompaction.");
            var registered = strategies
                .Where(strategy => strategy.CompactorKey.Equals(compactorText, StringComparison.Ordinal))
                .ToDictionary(static strategy => strategy.Registration.Descriptor.Key);
            CompactionStrategyOrdering.Validate(
                declaration.Key,
                [.. registered.Values.Select(static strategy => strategy.Registration)],
                declaration.StrategyOrder);

            foreach (var strategyKey in declaration.StrategyOrder)
            {
                if (!registered.TryGetValue(strategyKey, out var strategy))
                {
                    throw new InvalidOperationException(
                        $"Compaction profile '{declaration.Key.Value}' orders strategy '{strategyKey.Value}', which is not registered for compactor '{compactorText}'.");
                }

                var descriptor = strategy.Registration.Descriptor;
                if (declaration.AllowOversizedTurnRepair
                    && !descriptor.Capabilities.HasFlag(CompactionStrategyCapabilities.OversizedTurnRepair))
                {
                    throw new InvalidOperationException(
                        $"Compaction profile '{declaration.Key.Value}' allows oversized-turn repair, but strategy '{strategyKey.Value}' does not advertise that capability.");
                }

                if (descriptor.SummaryGeneratorKey is { } generatorKey
                    && !generators.Any(generator =>
                        generator.CompactorKey.Equals(compactorText, StringComparison.Ordinal)
                        && generator.Registration.Descriptor.Key == generatorKey))
                {
                    throw new InvalidOperationException(
                        $"Compaction profile '{declaration.Key.Value}' orders strategy '{strategyKey.Value}', which requires summary generator '{generatorKey.Value}', but none is registered for compactor '{compactorText}'.");
                }
            }

            profiles.Add(declaration.Key, new CompactionProfilePublication(Compile(declaration, ceilings), declaration.Enabled));
        }

        return new CompactionProfileRegistry(profiles.ToFrozenDictionary());
    }

    private static CompactionPolicySnapshot Compile(CompactionProfileDeclaration profile, ContextCompactionOptionsSnapshot ceilings) =>
        new(
            profile.Key,
            profile.Version,
            profile.CompactorKey,
            profile.StrategyOrder,
            ceilings.MaximumAttempts,
            ceilings.MaximumSourceEntries,
            ceilings.MaximumSourceBytes,
            ceilings.MaximumSummaryTokens,
            ceilings.MinimumRetainedEntries,
            ceilings.MaximumValidationIssues,
            ceilings.MinimumReductionRatio,
            profile.AllowOversizedTurnRepair,
            ceilings.PersistRejectedCandidates,
            Fingerprint(profile, ceilings));

    /// <summary>Hashes the canonical text of every value that shapes the compiled policy.</summary>
    private static ContentHash Fingerprint(CompactionProfileDeclaration profile, ContextCompactionOptionsSnapshot ceilings)
    {
        var text = new StringBuilder();
        void Line(string name, string value) => text.Append(name).Append('=').Append(value).Append('\n');
        Line("profile", profile.Key.Value);
        Line("version", profile.Version.Value.ToString(CultureInfo.InvariantCulture));
        Line("compactor", profile.CompactorKey.Value);
        Line("strategies", string.Join('\u001f', profile.StrategyOrder.Select(static key => key.Value)));
        Line("oversized", profile.AllowOversizedTurnRepair ? "1" : "0");
        Line("attempts", ceilings.MaximumAttempts.ToString(CultureInfo.InvariantCulture));
        Line("entries", ceilings.MaximumSourceEntries.ToString(CultureInfo.InvariantCulture));
        Line("bytes", ceilings.MaximumSourceBytes.ToString(CultureInfo.InvariantCulture));
        Line("tokens", ceilings.MaximumSummaryTokens.ToString(CultureInfo.InvariantCulture));
        Line("retained", ceilings.MinimumRetainedEntries.ToString(CultureInfo.InvariantCulture));
        Line("issues", ceilings.MaximumValidationIssues.ToString(CultureInfo.InvariantCulture));
        Line("reduction", ceilings.MinimumReductionRatio.ToString("R", CultureInfo.InvariantCulture));
        Line("persist", ceilings.PersistRejectedCandidates ? "1" : "0");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return new ContentHash($"sha256:{Convert.ToHexStringLower(hash)}");
    }
}
