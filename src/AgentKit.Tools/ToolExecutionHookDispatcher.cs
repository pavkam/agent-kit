// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Text.Json;

/// <summary>Dispatches tool lifecycle hooks from the executor pipeline.</summary>
internal static class ToolExecutionHookDispatcher
{
    internal static bool CatalogIncludesPoint(HookCatalogSnapshot catalog, HookPointId point) =>
        catalog.Registrations.Any(registration => registration.Point.Equals(point));

    internal static async ValueTask<BeforeToolHookOutcome> DispatchBeforeToolInvocationAsync(
        IHookDispatcher dispatcher,
        ToolExecutionHookBinding hooks,
        AgentId agentId,
        SessionId sessionId,
        ToolCallPart call,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(call);
        if (!CatalogIncludesPoint(hooks.Scope.Catalog, AgentHookPoints.BeforeToolInvocation))
        {
            return new BeforeToolHookOutcome(call.Arguments, null);
        }

        var dispatch = hooks.CreateDispatchMetadata(AgentHookPoints.BeforeToolInvocation, hooks.TurnCorrelation);
        var hookArgs = new BeforeToolInvocationEventArgs(dispatch, agentId, sessionId, call);
        var hookContext = hooks.Scope.CreateDispatch(dispatch);
        await dispatcher.DispatchAsync(
            AgentHookPointDefinitions.BeforeToolInvocation,
            hookContext,
            hookArgs,
            HookFailureMode.FailOperation,
            cancellationToken).ConfigureAwait(false);
        return new BeforeToolHookOutcome(hookArgs.Arguments, hookArgs.Veto);
    }

    internal static async ValueTask<ToolCallResult> DispatchToolResultAsync(
        IHookDispatcher? dispatcher,
        ToolExecutionHookBinding? hooks,
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        ToolCallResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (dispatcher is null || hooks is null || !CatalogIncludesPoint(hooks.Scope.Catalog, AgentHookPoints.ToolResult))
        {
            return result;
        }

        var dispatch = hooks.CreateDispatchMetadata(AgentHookPoints.ToolResult, hooks.TurnCorrelation);
        var hookArgs = new ToolResultHookEventArgs(dispatch, agentId, sessionId, runId, result);
        var hookContext = hooks.Scope.CreateDispatch(dispatch);
        await dispatcher.DispatchAsync(
            AgentHookPointDefinitions.ToolResult,
            hookContext,
            hookArgs,
            HookFailureMode.FailOperation,
            cancellationToken).ConfigureAwait(false);
        return hookArgs.ContentReplacement is { } replacement
            ? result with { Content = replacement }
            : result;
    }

    internal static ToolCallPart CreateCallPart(ToolCatalogSnapshot snapshot, ToolCallRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        if (!snapshot.ProviderAliases.TryGetValue(request.ProviderAlias, out var identity))
        {
            identity = new ToolIdentity(new ToolId(request.ProviderAlias.Value), new ToolVersion("0"));
        }

        _ = snapshot.Tools.FirstOrDefault(tool => tool.Id == identity.Id && tool.Version == identity.Version) is { } descriptor;
        var toolRef = new ToolReference(
            request.ProviderAlias,
            descriptor?.Id,
            descriptor?.Version);
        var arguments = ParseRawArguments(request.RawArguments);
        return new ToolCallPart(request.CallId, toolRef, arguments);
    }

    internal static ImmutableArray<byte> ToRawArguments(JsonElement arguments) =>
        arguments.ValueKind is JsonValueKind.Undefined
            ? ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes("{}"))
            : ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes(arguments.GetRawText()));

    private static JsonElement ParseRawArguments(ImmutableArray<byte> rawArguments)
    {
        return rawArguments.IsDefaultOrEmpty
            ? JsonDocument.Parse("{}").RootElement
            : JsonDocument.Parse(System.Text.Encoding.UTF8.GetBytes(rawArguments)).RootElement;
    }

    internal sealed record BeforeToolHookOutcome(JsonElement Arguments, ToolInvocationVeto? Veto);
}
