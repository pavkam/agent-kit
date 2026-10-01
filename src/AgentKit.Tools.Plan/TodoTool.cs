// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

using AgentKit.Tools;

/// <summary>Provides a todo-named compatibility surface over the canonical session work plan.</summary>
/// <remarks>
/// This tool deliberately delegates to <see cref="PlanTool"/> so a harness exposing both names does not create
/// two competing mutable task lists. IDs, revisions, security evidence, and durable entries remain identical.
/// </remarks>
public sealed class TodoTool: IToolInvoker
{
    private readonly PlanTool _planTool;

    /// <summary>The stable compatibility tool identity.</summary>
    public static readonly ToolId Id = new("todo");

    /// <summary>Initializes the todo compatibility surface over the canonical plan implementation.</summary>
    /// <param name="store">The selected protected plan-state store.</param>
    /// <param name="authoritySelector">The security authority selector.</param>
    /// <param name="requestIds">The replaceable security-request identity source.</param>
    /// <param name="timeProvider">The deterministic authorization clock.</param>
    /// <param name="options">The captured model-facing bounds.</param>
    /// <param name="logger">The content-free logger the delegated plan invocation reports through.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    public TodoTool(
        IPlanStateStore store,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<PlanToolOptions> options,
        ILogger<PlanTool> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _planTool = new PlanTool(store, authoritySelector, requestIds, timeProvider, options, logger);
    }

    /// <summary>Gets the immutable descriptor shared with registration and discovery.</summary>
    public static ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "todo",
        "Compatibility name for the current session work plan. Reads and updates the same versioned state as plan; mutations require the previously observed revision.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), PlanTool.InputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.Mutating, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.plan"),
        ExtensionData.Empty);

    /// <summary>Gets the default toolset publication selecting this tool from the application tool source.</summary>
    public static ToolsetPublication DefaultToolset { get; } = new(
        new ToolsetKey("agentkit.tools.todo"),
        new ToolsetVersion(1),
        new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)),
        [new ToolsetSourceSelection(ApplicationToolSources.Default)],
        [new ToolAliasAssignment(new ToolAlias("todo"), new ToolIdentity(Id, Descriptor.Version))]);

    /// <inheritdoc/>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default) =>
        _planTool.InvokeAsync(Id, context, cancellationToken);
}
