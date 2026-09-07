// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

/// <summary>Lists one deterministic, snapshot-bound page of child paths from an authorized directory.</summary>
public sealed class ListDirectoryTool: ITool
{
    /// <summary>The stable identity under which the tool is registered.</summary>
    public static readonly ToolId Id = new("list_directory");

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "path": { "type": ["string", "null"], "description": "Directory relative to the workspace root; null lists the root." },
            "maximum_entries": { "type": "integer", "minimum": 1 },
            "cursor": {
              "type": ["object", "null"],
              "properties": {
                "snapshot": { "type": "string" },
                "next_index": { "type": "integer", "minimum": 1 }
              },
              "required": ["snapshot", "next_index"],
              "additionalProperties": false
            }
          },
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IDirectoryReader _directoryReader;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly ListDirectoryToolOptions _options;

    /// <summary>Initializes a directory-listing tool.</summary>
    /// <param name="directoryReader">The narrow host enumeration capability.</param>
    /// <param name="securityAuthority">The system-wide security authority.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated page options.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured page bound is not positive or the default exceeds the maximum.</exception>
    public ListDirectoryTool(
        IDirectoryReader directoryReader,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<ListDirectoryToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(directoryReader);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.DefaultPageEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumPageEntries);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.Value.DefaultPageEntries, options.Value.MaximumPageEntries);
        _directoryReader = directoryReader;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "list_directory",
        "Lists a deterministic page of child paths without following entries. Continuations fail if the directory changes.",
        _inputSchema,
        ToolEffect.ReadOnly,
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryParse(request.Arguments, out var path, out var maximumEntries, out var cursor, out var error))
        {
            return Failed(error!, "invalid_arguments");
        }

        var context = request.Context;
        var decision = await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
                _directoryReader.SecurityAudience,
                SecurityOperationKind.DirectoryRead,
                SecurityEffect.Observe,
                [DirectorySecurityBinding.Resource(path)],
                DirectorySecurityBinding.Fingerprint(path, maximumEntries, cursor),
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return Failed(denied.Denial.SafeMessage, "denied");
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failed("The security authority returned an unsupported decision.", "denied");
        }

        var result = await _directoryReader.EnumerateAsync(
            new DirectoryEnumerationRequest(path, maximumEntries, cursor, allowed.Grant),
            cancellationToken).ConfigureAwait(false);
        if (result.Status != DirectoryEnumerationStatus.Success)
        {
            return Failed(result.SafeMessage!, result.Status.ToString());
        }

        var json = JsonSerializer.Serialize(new
        {
            entries = result.Entries.Select(static entry => entry.Path.Value),
            snapshot = result.SnapshotFingerprint?.Value,
            continuation = result.Continuation is null
                ? null
                : new
                {
                    snapshot = result.Continuation.SnapshotFingerprint.Value,
                    next_index = result.Continuation.NextIndex,
                },
        });
        return new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)]);
    }

    private bool TryParse(
        JsonElement arguments,
        out FileSystemPath? path,
        out int maximumEntries,
        out DirectoryEnumerationCursor? cursor,
        out string? error)
    {
        path = null;
        maximumEntries = _options.DefaultPageEntries;
        cursor = null;
        error = null;
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            error = "Arguments must be a JSON object.";
            return false;
        }

        if (arguments.TryGetProperty("path", out var pathProperty) && pathProperty.ValueKind != JsonValueKind.Null)
        {
            if (pathProperty.ValueKind != JsonValueKind.String)
            {
                error = "Property 'path' must be a string or null.";
                return false;
            }

            try
            {
                path = new FileSystemPath(pathProperty.GetString()!);
            }
            catch (ArgumentException exception)
            {
                error = $"Invalid path: {exception.Message}";
                return false;
            }
        }

        if (arguments.TryGetProperty("maximum_entries", out var maximumProperty))
        {
            if (!maximumProperty.TryGetInt32(out maximumEntries)
                || maximumEntries <= 0
                || maximumEntries > _options.MaximumPageEntries)
            {
                error = $"Property 'maximum_entries' must be between 1 and {_options.MaximumPageEntries}.";
                return false;
            }
        }

        if (arguments.TryGetProperty("cursor", out var cursorProperty) && cursorProperty.ValueKind != JsonValueKind.Null)
        {
            if (cursorProperty.ValueKind != JsonValueKind.Object
                || !cursorProperty.TryGetProperty("snapshot", out var snapshot)
                || snapshot.ValueKind != JsonValueKind.String
                || !cursorProperty.TryGetProperty("next_index", out var nextIndex)
                || !nextIndex.TryGetInt32(out var parsedIndex)
                || parsedIndex <= 0)
            {
                error = "Property 'cursor' must contain a string 'snapshot' and positive integer 'next_index'.";
                return false;
            }

            try
            {
                cursor = new DirectoryEnumerationCursor(new ContentHash(snapshot.GetString()!), parsedIndex);
            }
            catch (ArgumentException exception)
            {
                error = $"Invalid cursor: {exception.Message}";
                return false;
            }
        }

        return true;
    }

    private static ToolInvocationResult Failed(string reason, string status) => new(
        new ToolCallOutcome(
            ToolCallOutcomeKind.Failed,
            reason,
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                "agentkit.directory.status",
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])))),
        []);
}
