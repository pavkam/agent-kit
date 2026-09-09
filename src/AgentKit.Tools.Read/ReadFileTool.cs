// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

/// <summary>
/// A tool that reads a text file through an injected <see cref="IFileSystem"/>,
/// with optional 1-indexed line-range selection.
/// </summary>
/// <remarks>
/// This tool never touches the host file system directly: every read goes
/// through the injected <see cref="IFileSystem"/>, which re-validates the
/// requested path against its own configured boundary regardless of
/// whatever authorization already let the call reach this point.
/// </remarks>
public sealed class ReadFileTool: ITool
{
    /// <summary>The stable identity this tool registers under.</summary>
    public static readonly ToolId Id = new("read_file");

    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "path": { "type": "string", "description": "The file path, relative to the sandboxed working directory." },
            "offset": { "type": "integer", "minimum": 1, "description": "The 1-indexed line number to start reading from." },
            "limit": { "type": "integer", "minimum": 1, "description": "The maximum number of lines to return." }
          },
          "required": ["path"]
        }
        """).RootElement;

    private readonly IFileSystem _fileSystem;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ReadFileTool"/> class.</summary>
    /// <param name="fileSystem">The file system this tool reads through.</param>
    /// <param name="securityAuthority">The system-wide authority used after path and argument normalization.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The deterministic clock used to bound authorization.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public ReadFileTool(
        IFileSystem fileSystem,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _fileSystem = fileSystem;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "read_file",
        "Reads a text file within the sandboxed working directory, optionally limited to a line range.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.read"),
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ToolArguments.TryGetRequiredString(request.Arguments, "path", out var pathText, out var pathError))
        {
            return Failed(pathError);
        }

        FileSystemPath path;
        try
        {
            path = new FileSystemPath(pathText);
        }
        catch (ArgumentException ex)
        {
            return Failed($"Invalid path: {ex.Message}");
        }

        if (!ToolArguments.TryGetOptionalInt(request.Arguments, "offset", out var offset, out var offsetError))
        {
            return Failed(offsetError);
        }

        if (!ToolArguments.TryGetOptionalInt(request.Arguments, "limit", out var limit, out var limitError))
        {
            return Failed(limitError);
        }

        if (offset is < 1 || limit is < 1)
        {
            return Failed("'offset' and 'limit' must be positive integers when provided.");
        }

        var context = request.Context;
        var securityRequest = new SecurityRequest(
            _requestIds.Create(),
            new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
            context.ToolCallId,
            context.Identity,
            _fileSystem.SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(path)],
            FileSecurityBinding.ReadFingerprint(path),
            _timeProvider.GetUtcNow().AddMinutes(1));
        var decision = await _securityAuthority.AuthorizeAsync(securityRequest, cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied authorizationDenied)
        {
            return Failed(authorizationDenied.Denial.SafeMessage);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failed("The security authority returned an unsupported decision.");
        }

        var result = await _fileSystem.ReadAsync(new FileReadRequest(path, allowed.Grant), cancellationToken).ConfigureAwait(false);

        return result switch
        {
            FileRead read => Success(ApplyRange(read.Content, offset, limit)),
            FileNotFound => Failed($"No file exists at '{pathText}'."),
            FileReadDenied denied => Failed(denied.SafeMessage),
            FileReadFailed failed => Failed(failed.SafeMessage),
            _ => Failed("The file system returned an unrecognized outcome.")
        };
    }

    private static string ApplyRange(string content, int? offset, int? limit)
    {
        if (offset is null && limit is null)
        {
            return content;
        }

        var lines = content.ReplaceLineEndings("\n").Split('\n');
        var startIndex = Math.Min(Math.Max((offset ?? 1) - 1, 0), lines.Length);
        var count = Math.Min(limit ?? int.MaxValue, lines.Length - startIndex);

        return string.Join('\n', lines.Skip(startIndex).Take(Math.Max(count, 0)));
    }

    private static ToolInvocationResult Success(string content) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty),
        [new TextPart(content, TextSemantics.Plain, ExtensionData.Empty)]);

    private static ToolInvocationResult Failed(string reason) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, reason, ExtensionData.Empty),
        []);
}
