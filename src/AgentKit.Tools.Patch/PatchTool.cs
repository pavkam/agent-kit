// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Parses, plans, authorizes, and applies exact source-ordered workspace patch batches.</summary>
public sealed class PatchTool: ITool
{
    /// <summary>The stable identity under which the tool is registered.</summary>
    public static readonly ToolId Id = new("patch");

    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "patch": { "type": "string", "minLength": 1 }
          },
          "required": ["patch"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IFileSnapshotReader _snapshotReader;
    private readonly IWorkspacePatchApplier _applier;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly IIdentifierGenerator<WorkspaceMutationId> _mutationIds;
    private readonly TimeProvider _timeProvider;
    private readonly PatchToolOptions _options;

    /// <summary>Initializes a side-effect-free parsed and separately authorized patch tool.</summary>
    /// <param name="snapshotReader">The exact bounded source and absence observation capability.</param>
    /// <param name="applier">The transactional host patch capability.</param>
    /// <param name="securityAuthority">The system-wide authority for every observation and mutation entry.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="mutationIds">The per-entry mutation identity generator.</param>
    /// <param name="timeProvider">The deterministic authority-deadline clock.</param>
    /// <param name="options">The validated parser and complete-file bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive.</exception>
    public PatchTool(
        IFileSnapshotReader snapshotReader,
        IWorkspacePatchApplier applier,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        IIdentifierGenerator<WorkspaceMutationId> mutationIds,
        TimeProvider timeProvider,
        IOptions<PatchToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(snapshotReader);
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(mutationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumPatchBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumFileBytes);
        _snapshotReader = snapshotReader;
        _applier = applier;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _mutationIds = mutationIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "patch",
        "Applies an agentkit-patch-v1 Begin/End Patch envelope with Add File, exact-context Update File, Delete File, and content-preserving Move to entries. It plans the complete patch without effects, rejects overlapping paths, separately authorizes every observation and final mutation, and reports honest multi-file partial settlement.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.Mutating, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.patch"),
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryGetPatchText(request.Arguments, out var patchText, out var argumentError))
        {
            return Failure(argumentError!, "InvalidArguments", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        int patchBytes;
        try
        {
            patchBytes = _strictUtf8.GetByteCount(patchText);
        }
        catch (EncoderFallbackException)
        {
            return Failure("The patch syntax contains invalid Unicode scalar data.", "InvalidPatch", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (patchBytes > _options.MaximumPatchBytes)
        {
            return Failure("The patch syntax exceeds its configured byte bound.", "LimitExceeded", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!AgentKitPatchParser.TryParse(
                patchText,
                _options.MaximumEntries,
                out var parsed,
                out var parseError))
        {
            return Failure(parseError!, "InvalidPatch", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var planned = ImmutableArray.CreateBuilder<PlannedPatchEntry>(parsed!.Entries.Length);
        foreach (var entry in parsed.Entries)
        {
            var source = await ObserveAsync(request.Context, entry.Path, cancellationToken).ConfigureAwait(false);
            if (source.Error is not null)
            {
                return Failure(source.Error, source.Status, source.TerminalStatus, SideEffectCertainty.DefinitelyNotPerformed);
            }

            switch (entry.Kind)
            {
                case ParsedPatchEntryKind.Add:
                    if (source.Snapshot!.Status != FileSnapshotStatus.NotFound)
                    {
                        return Failure("An add-file target must not exist.", "Conflict", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    ImmutableArray<byte> addedContent;
                    try
                    {
                        addedContent = PatchTextPlanner.BuildAddedContent(entry.Lines);
                    }
                    catch (EncoderFallbackException)
                    {
                        return Failure("An added file contains invalid Unicode scalar data.", "InvalidPatch", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
                    }
                    if (addedContent.Length > _options.MaximumFileBytes)
                    {
                        return Failure("An added file exceeds the configured file byte bound.", "LimitExceeded", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    planned.Add(new PlannedPatchEntry(entry, _mutationIds.Create(), null, addedContent));
                    break;
                case ParsedPatchEntryKind.Update:
                    if (!TryRequireSnapshot(source.Snapshot!, out var updateFingerprint, out var sourceError))
                    {
                        return Failure(sourceError!, source.Snapshot!.Status.ToString(), ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    if (!PatchTextPlanner.TryApply(
                            source.Snapshot!.Content,
                            entry.Hunks,
                            out var finalContent,
                            out var planError))
                    {
                        return Failure(planError!, "PatchConflict", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    if (finalContent.Length > _options.MaximumFileBytes)
                    {
                        return Failure("An updated file exceeds the configured file byte bound.", "LimitExceeded", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    if (finalContent.AsSpan().SequenceEqual(source.Snapshot.Content.AsSpan()))
                    {
                        return Failure("An update hunk produced no byte change.", "NoChange", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    planned.Add(new PlannedPatchEntry(
                        entry, _mutationIds.Create(), updateFingerprint, finalContent));
                    break;
                case ParsedPatchEntryKind.Delete:
                    if (!TryRequireSnapshot(source.Snapshot!, out var deleteFingerprint, out sourceError))
                    {
                        return Failure(sourceError!, source.Snapshot!.Status.ToString(), ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    planned.Add(new PlannedPatchEntry(entry, _mutationIds.Create(), deleteFingerprint, []));
                    break;
                case ParsedPatchEntryKind.Move:
                    if (!TryRequireSnapshot(source.Snapshot!, out var moveFingerprint, out sourceError))
                    {
                        return Failure(sourceError!, source.Snapshot!.Status.ToString(), ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    var destination = await ObserveAsync(
                        request.Context, entry.DestinationPath!.Value, cancellationToken).ConfigureAwait(false);
                    if (destination.Error is not null)
                    {
                        return Failure(destination.Error, destination.Status, destination.TerminalStatus, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    if (destination.Snapshot!.Status != FileSnapshotStatus.NotFound)
                    {
                        return Failure("A move destination must not exist.", "Conflict", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
                    }

                    planned.Add(new PlannedPatchEntry(entry, _mutationIds.Create(), moveFingerprint, []));
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        var hostEntries = ImmutableArray.CreateBuilder<WorkspacePatchEntry>(planned.Count);
        foreach (var entry in planned)
        {
            var decision = await AuthorizeMutationAsync(
                request.Context, entry, cancellationToken).ConfigureAwait(false);
            if (decision is SecurityDenied denied)
            {
                return Failure(denied.Denial.SafeMessage, "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
            }

            if (decision is not SecurityAllowed allowed)
            {
                return Failure("The security authority returned an unsupported decision.", "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
            }

            hostEntries.Add(ToHostEntry(entry, allowed.Grant));
        }

        var result = await _applier.ApplyPatchAsync(
            new WorkspacePatchRequest(hostEntries.MoveToImmutable()),
            cancellationToken).ConfigureAwait(false);
        return Result(result);
    }

    private async ValueTask<(FileSnapshotResult? Snapshot, string? Error, string Status, ToolTerminalStatus TerminalStatus)> ObserveAsync(
        ToolExecutionContext context,
        FileSystemPath path,
        CancellationToken cancellationToken)
    {
        var decision = await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
                _snapshotReader.SecurityAudience,
                SecurityOperationKind.FileRead,
                SecurityEffect.Observe,
                [FileSecurityBinding.Resource(path)],
                FileSecurityBinding.SnapshotFingerprint(path, _options.MaximumFileBytes),
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return (null, denied.Denial.SafeMessage, "Denied", ToolTerminalStatus.Denied);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return (null, "The security authority returned an unsupported decision.", "Denied", ToolTerminalStatus.Unsupported);
        }

        var snapshot = await _snapshotReader.ReadSnapshotAsync(
            new FileSnapshotRequest(path, _options.MaximumFileBytes, allowed.Grant),
            cancellationToken).ConfigureAwait(false);
        return snapshot.Status is FileSnapshotStatus.Success or FileSnapshotStatus.NotFound
            ? (snapshot, null, snapshot.Status.ToString(), ToolTerminalStatus.Succeeded)
            : (snapshot, snapshot.SafeMessage ?? "The patch source could not be observed.", snapshot.Status.ToString(), snapshot.Status is FileSnapshotStatus.Denied ? ToolTerminalStatus.Denied : ToolTerminalStatus.InvocationFailed);
    }

    private async ValueTask<SecurityDecision> AuthorizeMutationAsync(
        ToolExecutionContext context,
        PlannedPatchEntry entry,
        CancellationToken cancellationToken)
    {
        var (effect, resources, fingerprint) = entry.Parsed.Kind switch
        {
            ParsedPatchEntryKind.Add => (
                SecurityEffect.Create,
                WorkspacePatchSecurityBinding.CreateResources(entry.Id, entry.Parsed.Path),
                WorkspacePatchSecurityBinding.CreateFingerprint(entry.Id, entry.Parsed.Path, entry.FinalContent)),
            ParsedPatchEntryKind.Update => (
                SecurityEffect.Replace,
                FileSecurityBinding.AtomicReplaceResources(entry.Id, entry.Parsed.Path),
                FileSecurityBinding.AtomicReplaceFingerprint(
                    entry.Id,
                    entry.Parsed.Path,
                    entry.ExpectedContentFingerprint!.Value,
                    entry.FinalContent)),
            ParsedPatchEntryKind.Delete => (
                SecurityEffect.Delete,
                WorkspacePatchSecurityBinding.DeleteResources(entry.Parsed.Path),
                WorkspacePatchSecurityBinding.DeleteFingerprint(
                    entry.Parsed.Path, entry.ExpectedContentFingerprint!.Value)),
            ParsedPatchEntryKind.Move => (
                SecurityEffect.Move,
                WorkspacePatchSecurityBinding.MoveResources(
                    entry.Parsed.Path, entry.Parsed.DestinationPath!.Value),
                WorkspacePatchSecurityBinding.MoveFingerprint(
                    entry.Parsed.Path,
                    entry.Parsed.DestinationPath!.Value,
                    entry.ExpectedContentFingerprint!.Value)),
            _ => throw new UnreachableException(),
        };
        return await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
                _applier.SecurityAudience,
                SecurityOperationKind.FileWrite,
                effect,
                resources,
                fingerprint,
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
    }

    private static WorkspacePatchEntry ToHostEntry(PlannedPatchEntry entry, SecurityGrant grant) =>
        entry.Parsed.Kind switch
        {
            ParsedPatchEntryKind.Add => new WorkspacePatchCreate(
                entry.Id, entry.Parsed.Path, entry.FinalContent, grant),
            ParsedPatchEntryKind.Update => new WorkspacePatchReplace(
                entry.Id,
                entry.Parsed.Path,
                entry.ExpectedContentFingerprint!.Value,
                entry.FinalContent,
                grant),
            ParsedPatchEntryKind.Delete => new WorkspacePatchDelete(
                entry.Id, entry.Parsed.Path, entry.ExpectedContentFingerprint!.Value, grant),
            ParsedPatchEntryKind.Move => new WorkspacePatchMove(
                entry.Id,
                entry.Parsed.Path,
                entry.Parsed.DestinationPath!.Value,
                entry.ExpectedContentFingerprint!.Value,
                grant),
            _ => throw new UnreachableException(),
        };

    private static bool TryRequireSnapshot(
        FileSnapshotResult snapshot,
        out ContentHash fingerprint,
        out string? error)
    {
        if (snapshot.Status == FileSnapshotStatus.Success && snapshot.ContentFingerprint is { } value)
        {
            fingerprint = value;
            error = null;
            return true;
        }

        fingerprint = default;
        error = snapshot.SafeMessage ?? "The required patch source does not exist.";
        return false;
    }

    private static bool TryGetPatchText(JsonElement arguments, out string patch, out string? error)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("patch", out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(property.GetString()))
        {
            patch = property.GetString()!;
            error = null;
            return true;
        }

        patch = "";
        error = "A non-empty string property named 'patch' is required.";
        return false;
    }

    private static ToolInvocationResult Result(WorkspacePatchResult result)
    {
        var json = JsonSerializer.Serialize(new
        {
            status = result.Status.ToString(),
            entries = result.Entries.Select(static entry => new
            {
                entry.Index,
                kind = entry.Kind.ToString(),
                status = entry.Status.ToString(),
                source_path = entry.SourcePath.Value,
                destination_path = entry.DestinationPath?.Value,
                content_fingerprint = entry.ContentFingerprint?.Value,
                message = entry.SafeMessage,
            }),
            message = result.SafeMessage,
        });
        var successful = result.Status is WorkspacePatchStatus.AtomicCommitted
            or WorkspacePatchStatus.CommittedWithNonAtomicVisibility;
        return new ToolInvocationResult(
            new ToolCallOutcome(
                successful ? ToolCallOutcomeKind.Success : ToolCallOutcomeKind.Failed,
                successful ? ToolTerminalStatus.Succeeded : ToolTerminalStatus.InvocationFailed,
                result.Entries.Any(static entry => entry.Status is WorkspacePatchEntryStatus.Uncertain)
                    ? SideEffectCertainty.Unknown
                    : successful ? SideEffectCertainty.DefinitelyPerformed
                    : result.Entries.Any(static entry => entry.Status is WorkspacePatchEntryStatus.Committed)
                        ? SideEffectCertainty.PartiallyPerformed : SideEffectCertainty.DefinitelyNotPerformed,
                false,
                successful ? null : result.SafeMessage ?? "The patch did not fully commit.",
                Status(result.Status.ToString())),
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)]);
    }

    private static ToolInvocationResult Failure(string reason, string status, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
        new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, Status(status)), []);

    private static ExtensionData Status(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.patch.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));
}
