// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.Options;

/// <summary>Journals a deferred approval's wait as a recoverable operation under the configured durability profile.</summary>
/// <remarks>
/// <para>
/// The wait is evidence, not authority. Recording it grants, widens, and consumes nothing, and the observer that
/// called the recorder already holds the same <see cref="SecurityApprovalRequired"/> decision either way. That is why
/// a durability gap is logged rather than surfaced: nothing is authorized by a deferral, so the only consequence of
/// missing evidence is that a recovering worker will not learn what this process was waiting for.
/// </para>
/// <para>
/// The operation is addressed from the request's own captured authorization, so a request that carries none, or
/// whose capture is sessionless or before-run, is not journaled at all rather than being given an invented run
/// identity. The recorded certainty is <see cref="SideEffectCertainty.DefinitelyNotPerformed"/>, because an approval
/// wait is precisely the state in which the protected effect has not been authorized and therefore cannot have
/// happened.
/// </para>
/// <para>
/// The recorder owns the dependency on the durability coordinator that the security authority must not have: the
/// coordinator authorizes each of its journal writes through that authority, so an authority-to-coordinator edge
/// would be a construction cycle. The profile is read from <see cref="AgentPermissionOptions.DurabilityProfile"/>
/// because the recorder is engine-wide and holds no per-agent selection.
/// </para>
/// <para>
/// Instances are immutable singletons and safe for concurrent use.
/// </para>
/// </remarks>
public sealed class DurableApprovalWaitRecorder: IApprovalWaitRecorder
{
    /// <summary>The window added to the injected clock for an approval-wait operation's declared deadline.</summary>
    /// <remarks>
    /// A deferred approval is answered by a human, so the durable operation's deadline is generous rather than tied
    /// to the request's own decision deadline: the operation exists to keep evidence of the wait, and expiring it
    /// early would make a still-pending approval look abandoned.
    /// </remarks>
    private static readonly TimeSpan _durableOperationTimeout = TimeSpan.FromHours(24);

    private readonly DurabilityProfileKey? _durabilityProfile;
    private readonly IDurableExecutionCoordinator? _durableExecution;
    private readonly IDurabilityProfileCatalog? _durabilityProfiles;
    private readonly DurableBoundaryRegistry _durableInvocations;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DurableApprovalWaitRecorder> _logger;

    /// <summary>Initializes the recorder over the composed durability runtime, when one exists.</summary>
    /// <param name="options">The validated permission configuration naming the approval-wait durability profile.</param>
    /// <param name="timeProvider">The injected clock that stamps the operation's deadline.</param>
    /// <param name="durableInvocations">
    /// The engine-wide live boundary continuation registry. Supplying the shared singleton is what lets the
    /// coordinator reach this recorder's waiting boundary.
    /// </param>
    /// <param name="durableExecution">
    /// The composed durable execution coordinator, or <see langword="null"/> when durability is not composed. It is
    /// used only to journal a deferred approval wait and never to reach a security decision.
    /// </param>
    /// <param name="durabilityProfiles">The composed durability profile catalog, or <see langword="null"/> when durability is not composed.</param>
    /// <param name="logger">The optional logger that receives safe, content-free diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/>, its value, <paramref name="timeProvider"/>, or <paramref name="durableInvocations"/> is <see langword="null"/>.
    /// </exception>
    public DurableApprovalWaitRecorder(
        IOptions<AgentPermissionOptions> options,
        TimeProvider timeProvider,
        DurableBoundaryRegistry durableInvocations,
        IDurableExecutionCoordinator? durableExecution = null,
        IDurabilityProfileCatalog? durabilityProfiles = null,
        ILogger<DurableApprovalWaitRecorder>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(durableInvocations);
        _durabilityProfile = options.Value.DurabilityProfile;
        _timeProvider = timeProvider;
        _durableInvocations = durableInvocations;
        _durableExecution = durableExecution;
        _durabilityProfiles = durabilityProfiles;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DurableApprovalWaitRecorder>.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A profile that is selected but cannot be honored, a request with no addressable authorization, and a durable
    /// write that fails are each logged as <c>ApprovalWaitNotRecorded</c> and otherwise ignored; none changes what the
    /// caller does with its decision. Cancellation always propagates.
    /// </remarks>
    public async ValueTask RecordAsync(
        SecurityRequest request,
        ApprovalRequest approval,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(approval);
        if (_durabilityProfile is null)
        {
            return;
        }

        if (DurableBoundaryScope.TryCreate(
                _durabilityProfile,
                _durableExecution,
                _durabilityProfiles,
                _durableInvocations,
                _timeProvider,
                _durableOperationTimeout,
                out var durability) is { } durabilityFailure)
        {
            SecurityLog.ApprovalWaitNotRecorded(_logger, request.Id, durabilityFailure);
            return;
        }

        Debug.Assert(durability is not null, "A selected, resolvable profile always produces a scope.");
        var authorization = request.Authorization;
        if (!durability.Journals(PermissionsDurableOperations.ApprovalWait, authorization))
        {
            return;
        }

        var manifest = new DurableApprovalWaitManifest(request.Id.Value, approval.Id.Value, request.Kind.ToString());
        try
        {
            _ = await durability.ExecuteAsync(
                PermissionsDurableOperations.ApprovalWait,
                PermissionsDurableOperations.ApprovalWaitVersion,
                authorization,
                DurableBoundaryPayload.Encode(manifest),
                SecurityEffect.Observe,
                hooks: null,
                async (context, token) =>
                {
                    _ = await context.Checkpoints.RecordWaitingAsync(
                        new DurableWaitCondition(
                            SideEffectCertainty.DefinitelyNotPerformed,
                            new ExternalOperationReference(
                                context.Binding.ExecutionContext.BackendKey, approval.Id.Value.ToString()),
                            notBefore: null),
                        token).ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            SecurityLog.ApprovalWaitNotRecorded(_logger, request.Id, exception.GetType().FullName ?? exception.GetType().Name);
            return;
        }

        SecurityLog.ApprovalWaitRecorded(_logger, request.Id, approval.Id);
    }
}
