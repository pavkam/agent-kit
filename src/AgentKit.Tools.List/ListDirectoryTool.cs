// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

#pragma warning disable CS0612 // Legacy ILegacyDirectoryReader until WS5-C8 migrates list_directory onto spec IDirectoryReader.

/// <summary>Lists one deterministic, snapshot-bound page of child paths from an authorized directory.</summary>
public sealed class ListDirectoryTool: IToolInvoker, ITool
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

    /// <summary>Gets the immutable descriptor shared by registration and discovery.</summary>
    /// <value>The complete publication used by <see cref="ServiceExtensions.AddListTool"/>.</value>
    public static ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "list_directory",
        "Lists a deterministic page of child paths without following entries. Continuations fail if the directory changes.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.list"),
        ExtensionData.Empty);

    /// <summary>Gets the default toolset publication selecting this tool from the application tool source.</summary>
    /// <value>An immutable publication hosts add through <see cref="Tools.ServiceExtensions.AddToolset"/>.</value>
    public static ToolsetPublication DefaultToolset { get; } = new(
        new ToolsetKey("agentkit.tools.list"),
        new ToolsetVersion(1),
        new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)),
        [new ToolsetSourceSelection(ApplicationToolSources.Default)],
        [new ToolAliasAssignment(new ToolAlias("list_directory"), new ToolIdentity(Id, Descriptor.Version))]);

    [Obsolete("Use spec IDirectoryReader after WS5-C8 migrates list_directory.")]
    private readonly ILegacyDirectoryReader _directoryReader;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly ListDirectoryToolOptions _options;

    /// <summary>Initializes a directory-listing tool.</summary>
    /// <param name="directoryReader">The narrow host enumeration capability.</param>
    /// <param name="authoritySelector">The security authority selector.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated page options.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured page bound is not positive or the default exceeds the maximum.</exception>
    [Obsolete("Use spec IDirectoryReader after WS5-C8 migrates list_directory.")]
    public ListDirectoryTool(
        ILegacyDirectoryReader directoryReader,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<ListDirectoryToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(directoryReader);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.DefaultPageEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumPageEntries);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.Value.DefaultPageEntries, options.Value.MaximumPageEntries);
        _directoryReader = directoryReader;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    ToolDescriptor ITool.Descriptor => Descriptor;

    /// <inheritdoc/>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authorization = context.InvocationGrant.Authorization
            ?? throw new InvalidOperationException("Tool invocations require grants that retain complete authorization evidence.");
        return InvokeCoreAsync(authorization, context.CallId, context.Arguments, cancellationToken);
    }

    /// <inheritdoc/>
    [Obsolete("Legacy host surface.")]

    public Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeCoreAsync(
            request.Context.Authorization,
            request.Context.ToolCallId,
            request.Arguments,
            cancellationToken).AsTask();
    }

    private async ValueTask<ToolInvocationResult> InvokeCoreAsync(
        SecurityAuthorizationContext authorization,
        ToolCallId callId,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParse(arguments, out var path, out var maximumEntries, out var cursor, out var error))
        {
            return Failed(error!, "invalid_arguments", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return Failed("The captured security authority is unavailable.", "denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var decision = await selected.Authority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                authorization.Scope,
                callId,
                authorization.Identity,
                authorization,
                _directoryReader.SecurityAudience,
                SecurityOperationKind.DirectoryRead,
                SecurityEffect.Observe,
                [DirectorySecurityBinding.Resource(path)],
                DirectorySecurityBinding.Fingerprint(path, maximumEntries, cursor),
                _timeProvider.GetUtcNow().AddMinutes(1)),
            hooks: null,
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return Failed(denied.Denial.SafeMessage, "denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failed("The security authority returned an unsupported decision.", "denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var result = await _directoryReader.EnumerateAsync(
            new DirectoryEnumerationRequest(path, maximumEntries, cursor, allowed.Grant),
            cancellationToken).ConfigureAwait(false);
        if (result.Status != DirectoryEnumerationStatus.Success)
        {
            return Failed(result.SafeMessage!, result.Status.ToString(), result.Status switch { DirectoryEnumerationStatus.Denied => ToolTerminalStatus.Denied, DirectoryEnumerationStatus.Success or DirectoryEnumerationStatus.NotFound or DirectoryEnumerationStatus.LimitExceeded or DirectoryEnumerationStatus.SnapshotChanged or DirectoryEnumerationStatus.Failed => ToolTerminalStatus.InvocationFailed, _ => ToolTerminalStatus.InvocationFailed }, result.Status is DirectoryEnumerationStatus.Denied or DirectoryEnumerationStatus.NotFound ? SideEffectCertainty.DefinitelyNotPerformed : SideEffectCertainty.Unknown);
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
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
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
            if (maximumProperty.ValueKind != JsonValueKind.Number
                || !maximumProperty.TryGetInt32(out maximumEntries)
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
                || nextIndex.ValueKind != JsonValueKind.Number
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

    private static ToolInvocationResult Failed(string reason, string status, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
        new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false,
            reason,
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                "agentkit.directory.status",
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])))),
        []);
}

#pragma warning restore CS0612
