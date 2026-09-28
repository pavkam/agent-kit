// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Routes compaction events to sinks registered for one compactor key.</summary>
internal sealed class DefaultCompactionEventDispatcher(
    ComponentKey<ICompactor> compactorKey,
    IEnumerable<CompactionEventSinkDeclaration> declarations,
    IServiceProvider services): ICompactionEventDispatcher
{
    private readonly ComponentKey<ICompactor> _compactorKey = compactorKey;
    private readonly ImmutableArray<CompactionEventSinkDeclaration> _declarations =
        [.. declarations.Where(d => d.CompactorKey.Equals(compactorKey.Value, StringComparison.Ordinal))
            .OrderBy(static d => d.Registration.Order)];

    /// <inheritdoc/>
    public async ValueTask<CompactionEventDispatchResult> PublishAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionEvent compactionEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compactionEvent);
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);
        if (!compactorKey.Equals(_compactorKey))
        {
            return new CompactionEventPublished();
        }

        foreach (var declaration in _declarations)
        {
            var sink = (ICompactionEventSink?) services.GetService(declaration.SinkType);
            if (sink is null)
            {
                if (declaration.Registration.Delivery == CompactionEventDelivery.Required)
                {
                    return new RequiredCompactionEventUnavailable(
                        declaration.Registration.Id,
                        new CompactionFailure(
                            CompactionFailureKind.RequiredObservationUnavailable,
                            $"Required compaction event sink '{declaration.Registration.Id}' is not available.",
                            retryable: false,
                            ExtensionData.Empty));
                }

                continue;
            }

            try
            {
                await sink.PublishAsync(compactionEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (declaration.Registration.Delivery == CompactionEventDelivery.Required)
            {
                return new RequiredCompactionEventUnavailable(
                    declaration.Registration.Id,
                    new CompactionFailure(
                        CompactionFailureKind.RequiredObservationUnavailable,
                        $"Required compaction event sink '{declaration.Registration.Id}' failed: {exception.GetType().Name}.",
                        retryable: false,
                        ExtensionData.Empty));
            }
        }

        return new CompactionEventPublished();
    }
}
