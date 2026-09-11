// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Lists a captured skill catalog or activates one skill through an exact protected file snapshot.</summary>
public sealed class SkillTool: ITool
{
    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "action": { "type": "string", "enum": ["list", "activate"] },
            "id": { "type": ["string", "null"] }
          },
          "required": ["action"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IFileSnapshotReader _reader;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly ISkillCatalog _catalog;
    private readonly long _maximumBytes;
    private readonly int _maximumCharacters;

    /// <summary>The stable tool identity.</summary>
    public static readonly ToolId Id = new("skill");

    /// <summary>Initializes the activation tool over the same captured catalog used for discovery context.</summary>
    /// <param name="reader">The exact protected snapshot boundary.</param>
    /// <param name="securityAuthority">The system-wide security authority.</param>
    /// <param name="requestIds">The replaceable security-request identity source.</param>
    /// <param name="timeProvider">The deterministic authorization clock.</param>
    /// <param name="catalog">The shared immutable skill catalog.</param>
    /// <param name="options">The host activation bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    public SkillTool(
        IFileSnapshotReader reader,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        ISkillCatalog catalog,
        IOptions<SkillToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumBytes, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumCharacters, nameof(options));
        _reader = reader;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _catalog = catalog;
        _maximumBytes = options.Value.MaximumBytes;
        _maximumCharacters = options.Value.MaximumCharacters;
    }

    /// <inheritdoc/>
    public ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "skill",
        "Lists the captured skill catalog or activates one skill by stable ID. Activation reads guidance only; it never installs, migrates, executes, or grants instruction authority.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.skill"),
        ExtensionData.Empty);

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryArguments(request.Arguments, out var action, out var id))
        {
            return Failure("'action' must be list without an ID or activate with one stable ID.", "InvalidArguments", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (action == "list")
        {
            return Success(_catalog.RenderInventory(), "Listed");
        }

        var skill = _catalog.Snapshot.Skills.FirstOrDefault(candidate => candidate.Id == id);
        if (skill is null)
        {
            return Failure("No captured skill has that identity.", "NotFound", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var context = request.Context;
        var fingerprint = FileSecurityBinding.SnapshotFingerprint(skill.Path, _maximumBytes);
        var decision = await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, context.SessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
                _reader.SecurityAudience,
                SecurityOperationKind.FileRead,
                SecurityEffect.Observe,
                [FileSecurityBinding.Resource(skill.Path)],
                fingerprint,
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is not SecurityAllowed allowed)
        {
            return decision is SecurityDenied denied
                ? Failure(denied.Denial.SafeMessage, "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed)
                : Failure("The security authority returned an unsupported decision.", "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var snapshot = await _reader.ReadSnapshotAsync(new FileSnapshotRequest(skill.Path, _maximumBytes, allowed.Grant), cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != FileSnapshotStatus.Success || snapshot.ContentFingerprint is not { } actualHash)
        {
            return Failure(
                snapshot.SafeMessage ?? "The skill could not be read as one complete snapshot.",
                snapshot.Status == FileSnapshotStatus.Success ? "Failed" : snapshot.Status.ToString(), snapshot.Status is FileSnapshotStatus.Denied ? ToolTerminalStatus.Denied : ToolTerminalStatus.InvocationFailed, snapshot.Status is FileSnapshotStatus.Denied or FileSnapshotStatus.NotFound ? SideEffectCertainty.DefinitelyNotPerformed : SideEffectCertainty.Unknown);
        }

        if (skill.ExpectedContentHash is { } expectedHash && expectedHash != actualHash)
        {
            return Failure("The skill content did not match its configured integrity fingerprint.", "IntegrityMismatch", ToolTerminalStatus.ResultNormalizationFailed, SideEffectCertainty.DefinitelyPerformed);
        }

        string content;
        try
        {
            var bytes = snapshot.Content.AsSpan();
            if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
            {
                bytes = bytes[3..];
            }

            content = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Failure("The captured skill is not valid UTF-8 text.", "InvalidEncoding", ToolTerminalStatus.ResultNormalizationFailed, SideEffectCertainty.DefinitelyPerformed);
        }

        var truncated = content.Length > _maximumCharacters;
        return Success(JsonSerializer.Serialize(new
        {
            catalog_version = _catalog.Snapshot.Version,
            id = skill.Id.Value,
            skill.Name,
            skill.Description,
            trust = skill.Trust.ToString(),
            content_hash = actualHash.Value,
            bytes = snapshot.Content.Length,
            truncated,
            instruction_authority = false,
            content = truncated ? content[.._maximumCharacters] : content,
        }), "Activated");
    }

    private static bool TryArguments(JsonElement arguments, out string? action, out SkillId id)
    {
        action = null;
        id = default;
        if (arguments.ValueKind != JsonValueKind.Object
            || arguments.EnumerateObject().Any(static property => property.Name is not ("action" or "id"))
            || !arguments.TryGetProperty("action", out var actionProperty)
            || actionProperty.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        action = actionProperty.GetString();
        var hasId = arguments.TryGetProperty("id", out var idProperty) && idProperty.ValueKind != JsonValueKind.Null;
        if ((action == "list" && hasId) || (action == "activate" && !hasId) || action is not ("list" or "activate") || (hasId && idProperty.ValueKind != JsonValueKind.String))
        {
            return false;
        }

        if (!hasId)
        {
            return true;
        }

        try
        {
            id = new SkillId(idProperty.GetString()!);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ToolInvocationResult Success(string json, string status) => new(new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, Status(status)), [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)]);
    private static ToolInvocationResult Failure(string reason, string status, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, Status(status)), []);
    private static ExtensionData Status(string status) => new(ImmutableDictionary<string, ExtensionValue>.Empty.Add("agentkit.skill.status", new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));
}
