// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

using AgentKit.Tools;

/// <summary>Lists one deterministic, snapshot-bound page of child paths from an authorized directory.</summary>
public sealed class ListDirectoryTool: IToolInvoker
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

    private static readonly ToolLeafLogEvents _logEvents = new(ListDirectoryToolLog.Completed, ListDirectoryToolLog.Cancelled, ListDirectoryToolLog.Faulted);
    private readonly ILogger<ListDirectoryTool> _logger;
    private readonly IFileSystemSelector _fileSystemSelector;
    private readonly IFilePathNormalizer _pathNormalizer;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly ListDirectoryToolOptions _options;

    /// <summary>Initializes a directory-listing tool.</summary>
    /// <param name="fileSystemSelector">Selects the keyed directory-enumeration profile.</param>
    /// <param name="pathNormalizer">Normalizes model-supplied paths before authorization.</param>
    /// <param name="authoritySelector">The security authority selector.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="options">The validated page and profile options.</param>
    /// <param name="logger">The content-free logger the invocation observation reports through.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentException"><see cref="ListDirectoryToolOptions.HostRootPath"/> is not configured.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured page bound is not positive or the default exceeds the maximum.</exception>
    public ListDirectoryTool(
        IFileSystemSelector fileSystemSelector,
        IFilePathNormalizer pathNormalizer,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<ListDirectoryToolOptions> options,
        ILogger<ListDirectoryTool> logger)
    {
        ArgumentNullException.ThrowIfNull(fileSystemSelector);
        ArgumentNullException.ThrowIfNull(pathNormalizer);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.HostRootPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.DefaultPageEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumPageEntries);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.Value.DefaultPageEntries, options.Value.MaximumPageEntries);
        _fileSystemSelector = fileSystemSelector;
        _pathNormalizer = pathNormalizer;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ToolLeafObservation.RunAsync(Id, context.CallId, _logger, _logEvents, () => InvokeObservedAsync(context, cancellationToken));
    }

    private async ValueTask<ToolInvocationResult> InvokeObservedAsync(ToolInvocationContext context, CancellationToken cancellationToken)
    {
        var authorization = context.InvocationGrant.Authorization
            ?? throw new InvalidOperationException("Tool invocations require grants that retain complete authorization evidence.");
        return await InvokeCoreAsync(authorization, context.CallId, context.Arguments, cancellationToken);
    }

    private async ValueTask<ToolInvocationResult> InvokeCoreAsync(
            SecurityAuthorizationContext authorization,
            ToolCallId callId,
            JsonElement arguments,
            CancellationToken cancellationToken)
    {
        if (!TryParse(arguments, out var pathText, out var maximumEntries, out var cursor, out var error))
        {
            return Failed(error!, "invalid_arguments", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var normalized = _pathNormalizer.Normalize(
            new FilePathInput(_options.RootId, string.IsNullOrWhiteSpace(pathText) ? "." : pathText),
            _options.PathPolicy);
        if (normalized is not FilePathNormalizationSuccess normalizedPath)
        {
            var message = normalized is FilePathNormalizationFailed failed
                ? failed.SafeMessage
                : "The path could not be normalized.";
            return Failed(message, "invalid_arguments", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var selection = await _fileSystemSelector
            .SelectAsync(_options.ProfileKey, FileSystemCapability.Enumerate, cancellationToken)
            .ConfigureAwait(false);
        if (selection is FileSystemProfileMissing)
        {
            return Failed("The configured file-system profile is not registered.", "denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (selection is FileSystemCapabilityUnsupported unsupported)
        {
            return Failed(
                $"The file-system profile does not support directory enumeration ({unsupported.RequiredCapability}).",
                "denied",
                ToolTerminalStatus.Denied,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (selection is not FileSystemDirectoryReaderSelected readerSelected)
        {
            return Failed("The file-system profile could not resolve directory enumeration.", "denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        FileSystemPath? directoryPath = normalizedPath.Path.Value is "."
            ? null
            : new FileSystemPath(normalizedPath.Path.Value);

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
                readerSelected.DirectoryReader.SecurityAudience,
                SecurityOperationKind.DirectoryRead,
                SecurityEffect.Observe,
                [DirectorySecurityBinding.Resource(directoryPath)],
                DirectorySecurityBinding.Fingerprint(directoryPath),
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

        var operation = new AuthorizedDirectoryEnumeration(
            FileHostTargetBinding.Resolve(_options.RootId, normalizedPath.Path, _options.HostRootPath),
            allowed.Grant);
        var childPaths = new List<string>();
        try
        {
            await foreach (var entry in readerSelected.DirectoryReader.EnumerateAsync(operation, cancellationToken).ConfigureAwait(false))
            {
                childPaths.Add(directoryPath is null ? entry.Name.Value : $"{directoryPath.Value.Value}/{entry.Name.Value}");
            }
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failed(exception.Message, "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }
        catch (DirectoryNotFoundException exception)
        {
            return Failed(exception.Message, "NotFound", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }
        catch (IOException exception)
        {
            return Failed(exception.Message, "Failed", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown);
        }

        // The reader contract orders entries by ordinal name; page identity must not depend on that being honoured.
        childPaths.Sort(StringComparer.Ordinal);
        var snapshot = SnapshotFingerprint(childPaths);
        var start = cursor?.NextIndex ?? 0;
        if (cursor is not null && (cursor.SnapshotFingerprint != snapshot || start > childPaths.Count))
        {
            return Failed(
                "The directory changed after the supplied continuation was issued.",
                "SnapshotChanged",
                ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.Unknown);
        }

        var page = childPaths.Skip(start).Take(maximumEntries).ToArray();
        var next = start + page.Length;
        var json = JsonSerializer.Serialize(new
        {
            entries = page,
            snapshot = snapshot.Value,
            continuation = next < childPaths.Count
                ? new
                {
                    snapshot = snapshot.Value,
                    next_index = next,
                }
                : null,
        });
        return new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
            [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)]);
    }

    /// <summary>Computes the exact fingerprint of one complete ordered listing so a continuation can detect change.</summary>
    /// <param name="orderedPaths">The complete listing in ordinal order.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint over each length-prefixed UTF-8 path.</returns>
    private static ContentHash SnapshotFingerprint(IEnumerable<string> orderedPaths)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var path in orderedPaths)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(path);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }

        return new ContentHash($"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}");
    }

    private bool TryParse(
        JsonElement arguments,
        out string? pathText,
        out int maximumEntries,
        out DirectoryEnumerationCursor? cursor,
        out string? error)
    {
        pathText = null;
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

            pathText = pathProperty.GetString();
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
