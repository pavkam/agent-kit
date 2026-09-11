// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Edit;

/// <summary>Performs exact, version-conditional UTF-8 text replacement with atomic target visibility.</summary>
public sealed class EditTool: ITool
{
    /// <summary>The stable identity under which the tool is registered.</summary>
    public static readonly ToolId Id = new("edit");

    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string" },
            "old_text": { "type": "string", "minLength": 1 },
            "new_text": { "type": "string" },
            "replace_all": { "type": "boolean", "default": false },
            "maximum_bytes": { "type": "integer", "minimum": 1 }
          },
          "required": ["path", "old_text", "new_text"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IFileSnapshotReader _snapshotReader;
    private readonly IAtomicFileReplacer _replacer;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly IIdentifierGenerator<WorkspaceMutationId> _mutationIds;
    private readonly TimeProvider _timeProvider;
    private readonly EditToolOptions _options;

    /// <summary>Initializes an exact text-editing tool.</summary>
    /// <param name="snapshotReader">The exact byte-snapshot capability.</param>
    /// <param name="replacer">The conditional atomic replacement capability.</param>
    /// <param name="securityAuthority">The system-wide security authority.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="mutationIds">The mutation identity generator.</param>
    /// <param name="timeProvider">The deterministic authority-deadline clock.</param>
    /// <param name="options">The validated complete-file bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured byte bound is invalid.</exception>
    public EditTool(
        IFileSnapshotReader snapshotReader,
        IAtomicFileReplacer replacer,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        IIdentifierGenerator<WorkspaceMutationId> mutationIds,
        TimeProvider timeProvider,
        IOptions<EditToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(snapshotReader);
        ArgumentNullException.ThrowIfNull(replacer);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(mutationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            options.Value.DefaultMaximumBytes, nameof(options.Value.DefaultMaximumBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            options.Value.MaximumBytes, nameof(options.Value.MaximumBytes));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.Value.DefaultMaximumBytes,
            options.Value.MaximumBytes,
            nameof(options.Value.DefaultMaximumBytes));
        _snapshotReader = snapshotReader;
        _replacer = replacer;
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
        "edit",
        "Replaces one unique exact text occurrence, or every occurrence when requested, in a strict UTF-8 file. It preserves all untouched bytes, BOM, newline spelling, and final-newline state, requires the planned content hash at commit, and atomically replaces the existing file without following symlinks.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.Mutating, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.edit"),
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryParse(request.Arguments, out var arguments, out var error))
        {
            return Failure(error!, "InvalidArguments", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var readDecision = await AuthorizeReadAsync(request.Context, arguments, cancellationToken).ConfigureAwait(false);
        if (readDecision is SecurityDenied readDenied)
        {
            return Failure(readDenied.Denial.SafeMessage, "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (readDecision is not SecurityAllowed readAllowed)
        {
            return Failure("The security authority returned an unsupported decision.", "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var snapshot = await _snapshotReader.ReadSnapshotAsync(
            new FileSnapshotRequest(arguments.Path, arguments.MaximumBytes, readAllowed.Grant),
            cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != FileSnapshotStatus.Success || snapshot.ContentFingerprint is null)
        {
            return Failure(snapshot.SafeMessage ?? "The file snapshot failed.", snapshot.Status.ToString(), snapshot.Status is FileSnapshotStatus.Denied ? ToolTerminalStatus.Denied : ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        string original;
        try
        {
            original = _strictUtf8.GetString(snapshot.Content.AsSpan());
        }
        catch (DecoderFallbackException)
        {
            return Failure("The edit target is not strict UTF-8 text.", "BinaryOrInvalidText", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (original.Contains('\0', StringComparison.Ordinal))
        {
            return Failure("The edit target is binary content.", "BinaryOrInvalidText", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var occurrences = CountOccurrences(original, arguments.OldText);
        if (occurrences == 0)
        {
            return Failure("The exact old text was not found.", "NoMatch", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (occurrences > 1 && !arguments.ReplaceAll)
        {
            return Failure($"The exact old text matched {occurrences} locations; set replace_all to replace all.", "Ambiguous", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var replacements = arguments.ReplaceAll ? occurrences : 1;
        var updated = arguments.ReplaceAll
            ? original.Replace(arguments.OldText, arguments.NewText, StringComparison.Ordinal)
            : ReplaceFirst(original, arguments.OldText, arguments.NewText);
        var finalBytes = ImmutableArray.CreateRange(_strictUtf8.GetBytes(updated));
        if (finalBytes.Length > arguments.MaximumBytes)
        {
            return Failure("The final content exceeds the requested complete-file byte bound.", "LimitExceeded", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (finalBytes.AsSpan().SequenceEqual(snapshot.Content.AsSpan()))
        {
            return Success(SideEffectCertainty.DefinitelyNotPerformed, "NoChange", arguments.Path, replacements, snapshot.ContentFingerprint.Value, snapshot.ContentFingerprint.Value, finalBytes.Length);
        }

        var mutationId = _mutationIds.Create();
        var writeDecision = await AuthorizeReplaceAsync(
            request.Context,
            mutationId,
            arguments.Path,
            snapshot.ContentFingerprint.Value,
            finalBytes,
            cancellationToken).ConfigureAwait(false);
        if (writeDecision is SecurityDenied writeDenied)
        {
            return Failure(writeDenied.Denial.SafeMessage, "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (writeDecision is not SecurityAllowed writeAllowed)
        {
            return Failure("The security authority returned an unsupported decision.", "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var replacement = await _replacer.ReplaceAsync(
            new AtomicFileReplaceRequest(
                mutationId,
                arguments.Path,
                snapshot.ContentFingerprint.Value,
                finalBytes,
                writeAllowed.Grant),
            cancellationToken).ConfigureAwait(false);
        return replacement.Status == AtomicFileReplaceStatus.Committed && replacement.ContentFingerprint is { } finalHash
            ? Success(
                SideEffectCertainty.DefinitelyPerformed, replacement.Status.ToString(),
                arguments.Path,
                replacements,
                snapshot.ContentFingerprint.Value,
                finalHash,
                replacement.Bytes,
                replacement.SafeMessage)
            : Failure(replacement.SafeMessage ?? "The replacement failed.", replacement.Status.ToString(), replacement.Status is AtomicFileReplaceStatus.Denied ? ToolTerminalStatus.Denied : ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
    }

    private async ValueTask<SecurityDecision> AuthorizeReadAsync(
        ToolExecutionContext context,
        ParsedArguments arguments,
        CancellationToken cancellationToken) => await _securityAuthority.AuthorizeAsync(
        new SecurityRequest(
            _requestIds.Create(),
            new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
            context.ToolCallId,
            context.Identity,
            _snapshotReader.SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(arguments.Path)],
            FileSecurityBinding.SnapshotFingerprint(arguments.Path, arguments.MaximumBytes),
            _timeProvider.GetUtcNow().AddMinutes(1)),
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<SecurityDecision> AuthorizeReplaceAsync(
        ToolExecutionContext context,
        WorkspaceMutationId mutationId,
        FileSystemPath path,
        ContentHash expected,
        ImmutableArray<byte> content,
        CancellationToken cancellationToken) => await _securityAuthority.AuthorizeAsync(
        new SecurityRequest(
            _requestIds.Create(),
            new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
            context.ToolCallId,
            context.Identity,
            _replacer.SecurityAudience,
            SecurityOperationKind.FileWrite,
            SecurityEffect.Replace,
            FileSecurityBinding.AtomicReplaceResources(mutationId, path),
            FileSecurityBinding.AtomicReplaceFingerprint(mutationId, path, expected, content),
            _timeProvider.GetUtcNow().AddMinutes(1)),
        cancellationToken).ConfigureAwait(false);

    private bool TryParse(JsonElement json, out ParsedArguments arguments, out string? error)
    {
        arguments = default;
        error = null;
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("path", out var rawPath)
            || rawPath.ValueKind != JsonValueKind.String
            || !json.TryGetProperty("old_text", out var oldText)
            || oldText.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(oldText.GetString())
            || !json.TryGetProperty("new_text", out var newText)
            || newText.ValueKind != JsonValueKind.String
            || !TryBoolean(json, "replace_all", false, out var replaceAll)
            || !TryMaximumBytes(json, out var maximumBytes))
        {
            error = "Properties 'path', non-empty 'old_text', and 'new_text' are required; options must have the declared types and bounds.";
            return false;
        }

        try
        {
            arguments = new ParsedArguments(
                new FileSystemPath(rawPath.GetString()!),
                oldText.GetString()!,
                newText.GetString()!,
                replaceAll,
                maximumBytes);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool TryMaximumBytes(JsonElement json, out long maximumBytes)
    {
        if (!json.TryGetProperty("maximum_bytes", out var property))
        {
            maximumBytes = _options.DefaultMaximumBytes;
            return true;
        }

        return property.TryGetInt64(out maximumBytes)
            && maximumBytes > 0
            && maximumBytes <= _options.MaximumBytes;
    }

    private static bool TryBoolean(JsonElement json, string name, bool fallback, out bool value)
    {
        if (!json.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        value = property.ValueKind == JsonValueKind.True;
        return property.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string ReplaceFirst(string text, string oldText, string newText)
    {
        var offset = text.IndexOf(oldText, StringComparison.Ordinal);
        System.Diagnostics.Debug.Assert(offset >= 0, "The caller establishes that one exact match exists.");
        return string.Concat(text.AsSpan(0, offset), newText, text.AsSpan(offset + oldText.Length));
    }

    private static ToolInvocationResult Success(
        SideEffectCertainty certainty,
        string status,
        FileSystemPath path,
        int replacements,
        ContentHash previous,
        ContentHash final,
        long bytes,
        string? warning = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            status,
            path = path.Value,
            replacements,
            previous_content_fingerprint = previous.Value,
            final_content_fingerprint = final.Value,
            bytes,
            atomic_target_visibility = true,
            warning,
        });
        return new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, certainty, false, null, Status(status)),
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)]);
    }

    private static ToolInvocationResult Failure(string reason, string status, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
        new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, Status(status)), []);

    private static ExtensionData Status(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.edit.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));

    private readonly record struct ParsedArguments(
        FileSystemPath Path,
        string OldText,
        string NewText,
        bool ReplaceAll,
        long MaximumBytes);
}
