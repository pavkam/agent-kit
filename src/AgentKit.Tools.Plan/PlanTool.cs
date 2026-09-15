// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Reads and optimistically updates one typed session-backed work plan.</summary>
public sealed class PlanTool: ITool
{
    /// <summary>Gets the shared planning schema used by compatibility surfaces.</summary>
    internal static JsonElement InputSchema { get; } = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "action": { "type": "string", "enum": ["get", "replace", "set_status"] },
            "title": { "type": ["string", "null"] },
            "items": {
              "type": ["array", "null"],
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string", "minLength": 1 },
                  "text": { "type": "string", "minLength": 1 },
                  "status": { "type": "string", "enum": ["pending", "in_progress", "completed", "blocked"] }
                },
                "required": ["id", "text", "status"],
                "additionalProperties": false
              }
            },
            "item_id": { "type": ["string", "null"] },
            "status": { "type": ["string", "null"], "enum": ["pending", "in_progress", "completed", "blocked", null] },
            "expected_revision": { "type": ["integer", "null"], "minimum": 1 }
          },
          "required": ["action"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IPlanStateStore _store;
    private readonly ISecurityAuthority _securityAuthority;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumTitleCharacters;
    private readonly int _maximumItemCharacters;
    private readonly int _maximumItemIdCharacters;
    private readonly int _maximumItems;

    /// <summary>The stable tool identity.</summary>
    public static readonly ToolId Id = new("plan");

    /// <summary>Initializes the plan tool over one protected plan-state store.</summary>
    /// <param name="store">The selected protected plan-state store.</param>
    /// <param name="securityAuthority">The system-wide security authority.</param>
    /// <param name="requestIds">The replaceable security-request identity source.</param>
    /// <param name="timeProvider">The deterministic authorization clock.</param>
    /// <param name="options">The captured model-facing bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    public PlanTool(
        IPlanStateStore store,
        ISecurityAuthority securityAuthority,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<PlanToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(securityAuthority);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _store = store;
        _securityAuthority = securityAuthority;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _maximumTitleCharacters = options.Value.MaximumTitleCharacters;
        _maximumItemCharacters = options.Value.MaximumItemCharacters;
        _maximumItemIdCharacters = options.Value.MaximumItemIdCharacters;
        _maximumItems = options.Value.MaximumItems;
    }

    /// <summary>Gets the immutable descriptor shared with exact presentation formatting.</summary>
    /// <value>The source-owned identity, schema, effects, and hints for this tool.</value>
    internal static ToolDescriptor PresentationDescriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "plan",
        "Reads, replaces, or advances the typed work plan for the current session. Mutations require the revision returned by the previous result; keep at most one item in progress.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), InputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.Mutating, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.plan"),
        ExtensionData.Empty);

    /// <inheritdoc/>
    public ToolDescriptor Descriptor => PresentationDescriptor;

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Context.SessionId is not { } sessionId)
        {
            return Failure("The plan tool requires a session.", "SessionRequired", ToolTerminalStatus.Unsupported, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (request.Context.SessionProfile is not { } sessionProfile)
        {
            return Failure("The plan tool requires a captured session profile.", "SessionProfileRequired", ToolTerminalStatus.Unsupported, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (!TryParse(
                request.Arguments,
                out var action,
                out var title,
                out var items,
                out var itemId,
                out var status,
                out var expectedRevision,
                out var error))
        {
            return Failure(error!, "InvalidArguments", ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var context = request.Context;
        var operationContext = new SessionOperationContext(
            context.AgentId,
            sessionId,
            executionLaneId: null,
            context.Correlation,
            context.Identity,
            context.Authorization);
        var address = operationContext.ToAddress();
        var kind = action == "get" ? SecurityOperationKind.StateRead : SecurityOperationKind.StateMutation;
        var effect = action == "get" ? SecurityEffect.Observe : SecurityEffect.Mutate;
        var fingerprint = action switch
        {
            "get" => PlanSecurityBinding.ReadFingerprint(address),
            "replace" => PlanSecurityBinding.ReplaceFingerprint(address, title!, items, expectedRevision),
            "set_status" => PlanSecurityBinding.StatusFingerprint(address, itemId, status, expectedRevision!.Value),
            _ => throw new UnreachableException(),
        };
        var decision = await _securityAuthority.AuthorizeAsync(
            new SecurityRequest(
                _requestIds.Create(),
                new SecurityAuthorizationScope(context.AgentId, sessionId, context.Correlation),
                context.ToolCallId,
                context.Identity,
                context.Authorization,
                _store.SecurityAudience,
                kind,
                effect,
                [PlanSecurityBinding.Resource(address)],
                fingerprint,
                _timeProvider.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return Rejected(denied.Denial.SafeMessage, "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Rejected("The security authority returned an unsupported decision.", "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var result = action switch
        {
            "get" => await _store.ReadAsync(
                new PlanReadRequest(operationContext, sessionProfile, context.ToolCallId, allowed.Grant),
                cancellationToken).ConfigureAwait(false),
            "replace" => await _store.ReplaceAsync(
                new PlanReplaceRequest(
                    operationContext,
                    sessionProfile,
                    context.ToolCallId,
                    title!,
                    items,
                    expectedRevision,
                    allowed.Grant),
                cancellationToken).ConfigureAwait(false),
            "set_status" => await _store.SetStatusAsync(
                new PlanStatusRequest(
                    operationContext,
                    sessionProfile,
                    context.ToolCallId,
                    itemId,
                    status,
                    expectedRevision!.Value,
                    allowed.Grant),
                cancellationToken).ConfigureAwait(false),
            _ => throw new UnreachableException(),
        };
        return Project(result);
    }

    private static ToolInvocationResult Project(PlanStateResult result) => result switch
    {
        PlanStateFound found => Success(JsonSerializer.Serialize(new
        {
            plan = new
            {
                id = found.Plan.Id.ToString(),
                revision = found.Plan.Revision.Value,
                title = found.Plan.Title,
                items = found.Plan.Items.Select(static item => new
                {
                    id = item.Id.Value,
                    text = item.Text,
                    status = StatusText(item.Status),
                }),
            },
        }), "Current"),
        PlanStateMissing => Success(/*lang=json,strict*/ "{\"plan\":null}", "Missing"),
        PlanStateConflict conflict => Failure(
            conflict.CurrentRevision is { } revision
                ? $"The plan changed; its current revision is {revision.Value}. Read it and retry."
                : "The plan changed; read it and retry.",
            "Conflict", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed),
        PlanStateDenied denied => Rejected(denied.SafeMessage, "Denied", ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed),
        PlanStateFailed failed => Failure(failed.SafeMessage, "Failed", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown),
        _ => Failure("The plan store returned an unsupported result.", "Failed", ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown),
    };

    private bool TryParse(
        JsonElement arguments,
        out string? action,
        out string? title,
        out ImmutableArray<WorkPlanItem> items,
        out PlanItemId itemId,
        out PlanItemStatus status,
        out PlanRevision? expectedRevision,
        out string? error)
    {
        action = null;
        title = null;
        items = [];
        itemId = default;
        status = default;
        expectedRevision = null;
        error = null;
        if (arguments.ValueKind != JsonValueKind.Object
            || arguments.EnumerateObject().Any(static property => property.Name is not (
                "action" or "title" or "items" or "item_id" or "status" or "expected_revision"))
            || !arguments.TryGetProperty("action", out var actionProperty)
            || actionProperty.ValueKind != JsonValueKind.String)
        {
            error = "A supported action and its exact argument shape are required.";
            return false;
        }

        action = actionProperty.GetString();
        try
        {
            if (action == "get")
            {
                if (arguments.EnumerateObject().Count() == 1)
                {
                    return true;
                }

                error = "Get accepts only the action property.";
                return false;
            }

            expectedRevision = OptionalRevision(arguments);
            if (action == "replace")
            {
                if (!RequiredString(arguments, "title", _maximumTitleCharacters, out title)
                    || !TryItems(arguments, out items)
                    || HasNonNull(arguments, "item_id")
                    || HasNonNull(arguments, "status"))
                {
                    error = "Replace requires only a bounded title, one or more unique items, and an optional expected revision.";
                    return false;
                }

                return true;
            }

            if (action == "set_status"
                && RequiredString(arguments, "item_id", _maximumItemIdCharacters, out var itemIdText)
                && TryStatus(arguments, "status", out status)
                && expectedRevision.HasValue
                && !HasNonNull(arguments, "title")
                && !HasNonNull(arguments, "items"))
            {
                itemId = new PlanItemId(itemIdText!);
                return true;
            }
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }

        error = "A supported action and its exact argument shape are required.";
        return false;
    }

    private bool TryItems(JsonElement arguments, out ImmutableArray<WorkPlanItem> items)
    {
        items = [];
        if (!arguments.TryGetProperty("items", out var property)
            || property.ValueKind != JsonValueKind.Array
            || property.GetArrayLength() < 1
            || property.GetArrayLength() > _maximumItems)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<WorkPlanItem>(property.GetArrayLength());
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || item.EnumerateObject().Any(static member => member.Name is not ("id" or "text" or "status"))
                || item.EnumerateObject().Count() != 3
                || !RequiredString(item, "id", _maximumItemIdCharacters, out var id)
                || !RequiredString(item, "text", _maximumItemCharacters, out var text)
                || !TryStatus(item, "status", out var itemStatus))
            {
                return false;
            }

            builder.Add(new WorkPlanItem(new PlanItemId(id!), text!, itemStatus));
        }

        items = builder.MoveToImmutable();
        try
        {
            ArgumentException.ThrowIfDuplicatePlanItemIds(items);
            ArgumentException.ThrowIfMultipleInProgressPlanItems(items);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static PlanRevision? OptionalRevision(JsonElement arguments)
    {
        return !arguments.TryGetProperty("expected_revision", out var property)
            || property.ValueKind == JsonValueKind.Null
                ? null
                : property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value)
                    ? new PlanRevision(value)
                    : throw new ArgumentException(
                        "The expected_revision property must be a positive integer or null.",
                        nameof(arguments));
    }

    private static bool RequiredString(JsonElement value, string name, int maximum, out string? result)
    {
        result = null;
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        result = property.GetString();
        return !string.IsNullOrWhiteSpace(result) && result.Length <= maximum;
    }

    private static bool TryStatus(JsonElement value, string name, out PlanItemStatus status)
    {
        status = default;
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        status = property.GetString() switch
        {
            "pending" => PlanItemStatus.Pending,
            "in_progress" => PlanItemStatus.InProgress,
            "completed" => PlanItemStatus.Completed,
            "blocked" => PlanItemStatus.Blocked,
            _ => default,
        };
        return property.GetString() is "pending" or "in_progress" or "completed" or "blocked";
    }

    private static bool HasNonNull(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null;

    private static string StatusText(PlanItemStatus status) => status switch
    {
        PlanItemStatus.Pending => "pending",
        PlanItemStatus.InProgress => "in_progress",
        PlanItemStatus.Completed => "completed",
        PlanItemStatus.Blocked => "blocked",
        _ => throw new UnreachableException(),
    };

    private static void ValidateOptions(PlanToolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumTitleCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumItemCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumItemIdCharacters);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumItems, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaximumItems, 50);
    }

    private static ToolInvocationResult Success(string json, string status) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, OutcomeStatus(status)),
        [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)]);

    private static ToolInvocationResult Failure(string reason, string status, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
        new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, OutcomeStatus(status)),
        []);

    private static ToolInvocationResult Rejected(string reason, string status, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
        new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, OutcomeStatus(status)),
        []);

    private static ExtensionData OutcomeStatus(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.plan.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));
}
