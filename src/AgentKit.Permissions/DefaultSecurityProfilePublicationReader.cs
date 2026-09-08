// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Reads only explicitly composed immutable security-profile publications.</summary>
/// <remarks>The reader freezes registrations by their full coordinates and does not scan configuration or choose a newer fallback.</remarks>
internal sealed class DefaultSecurityProfilePublicationReader: ISecurityProfilePublicationReader
{
    private readonly FrozenDictionary<
        (AgentId AgentId, AgentDefinitionRevision DefinitionRevision, ConfigurationVersion ConfigurationVersion, SecurityProfileKey ProfileKey),
        SecurityProfilePublication> _publications;
    private readonly ILogger<DefaultSecurityProfilePublicationReader> _logger;

    /// <summary>Initializes a reader from all explicitly composed profile publications.</summary>
    /// <param name="publications">The non-null publication registrations to validate and freeze.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="publications"/> or an entry is null.</exception>
    /// <exception cref="ArgumentException">Two publications use the same exact coordinates.</exception>
    public DefaultSecurityProfilePublicationReader(
        IEnumerable<SecurityProfilePublication> publications,
        ILogger<DefaultSecurityProfilePublicationReader>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(publications);
        var snapshot = publications.ToArray();
        ArgumentException.ThrowIfDuplicateSecurityProfilePublication(snapshot, nameof(publications));
        _publications = snapshot.ToFrozenDictionary(static publication => (
            publication.AgentId,
            publication.AgentDefinitionRevision,
            publication.ConfigurationVersion,
            publication.ProfileKey));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSecurityProfilePublicationReader>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityProfilePublicationResult> ReadAsync(
        AgentId agentId,
        AgentDefinitionRevision agentDefinitionRevision,
        ConfigurationVersion configurationVersion,
        SecurityProfileKey profileKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfNegative(agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configurationVersion.Value, nameof(configurationVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coordinates = (agentId, agentDefinitionRevision, configurationVersion, profileKey);
            SecurityProfilePublicationResult result = _publications.TryGetValue(coordinates, out var publication)
                ? new SecurityProfilePublicationFound(publication)
                : new SecurityProfilePublicationUnavailable("The exact security-profile publication is unavailable.");
            var outcome = result is SecurityProfilePublicationFound
                ? SecurityProfilePublicationReadOutcome.Found
                : SecurityProfilePublicationReadOutcome.Unavailable;
            if (outcome is SecurityProfilePublicationReadOutcome.Found)
            {
                SafeLog(() => SecurityLog.ProfilePublicationFound(_logger, profileKey));
            }
            else
            {
                SafeLog(() => SecurityLog.ProfilePublicationUnavailable(_logger, profileKey));
            }

            SafeObserve(outcome);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeLog(() => SecurityLog.ProfilePublicationReadCancelled(_logger, profileKey));
            SafeObserve(SecurityProfilePublicationReadOutcome.Cancelled);
            throw;
        }
    }

    private static void SafeObserve(SecurityProfilePublicationReadOutcome outcome)
    {
        Debug.Assert(Enum.IsDefined(outcome), "Publication-read observation receives a defined terminal outcome.");
        try
        {
            SecurityMetrics.RecordProfilePublicationRead(outcome);
        }
        catch
        {
            // Meter listeners are observational and cannot alter publication lookup.
        }
    }

    private static void SafeLog(Action action)
    {
        Debug.Assert(action is not null, "Log observation requires a callback.");
        try
        {
            action();
        }
        catch
        {
            // Logging providers are observational and cannot alter publication lookup.
        }
    }
}
