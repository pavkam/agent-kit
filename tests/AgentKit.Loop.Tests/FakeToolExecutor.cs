// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using System.Collections.Immutable;
using System.Text.Json;

/// <summary>Test double that records spec-shaped tool calls and returns synthetic terminal results.</summary>
internal sealed class FakeToolExecutor: IToolExecutor
{
    private readonly Func<ToolCallRequest, ToolInvocationResult> _handler;
    private readonly IHookDispatcher? _hookDispatcher;

    /// <summary>Initializes a new instance of the <see cref="FakeToolExecutor"/> class.</summary>
    /// <param name="handler">Produces the invocation result for each received call.</param>
    /// <param name="hookDispatcher">Optional hook dispatcher applied before the handler.</param>
    public FakeToolExecutor(
        Func<ToolCallRequest, ToolInvocationResult>? handler = null,
        IHookDispatcher? hookDispatcher = null)
    {
        _handler = handler ?? (_ => TestFactory.SuccessResult());
        _hookDispatcher = hookDispatcher;
    }

    /// <summary>Gets every request this fake received, in call order.</summary>
    public List<ToolCallRequest> ReceivedRequests { get; } = [];

    /// <inheritdoc/>
    public async Task<ToolBatchResult> ExecuteAsync(
        IToolCatalogCapture capture,
        ImmutableArray<ToolCallRequest> calls,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(capability);
        var results = ImmutableArray.CreateBuilder<ToolCallResult>(calls.Length);
        foreach (var call in calls)
        {
            ArgumentNullException.ThrowIfNull(call);
            cancellationToken.ThrowIfCancellationRequested();
            var request = call;
            if (capability.Hooks is { } hooks && _hookDispatcher is not null
                && hooks.Scope.Catalog.Registrations.Any(static registration =>
                    registration.Point.Equals(AgentHookPoints.BeforeToolInvocation)))
            {
                var dispatch = hooks.CreateDispatchMetadata(AgentHookPoints.BeforeToolInvocation, hooks.TurnCorrelation);
                var callPart = CreateCallPart(capture.Snapshot, request);
                var hookArgs = new BeforeToolInvocationEventArgs(dispatch, request.AgentId, request.SessionId, callPart);
                var hookContext = hooks.Scope.CreateDispatch(dispatch);
                await _hookDispatcher.DispatchAsync(
                    AgentHookPointDefinitions.BeforeToolInvocation,
                    hookContext,
                    hookArgs,
                    HookFailureMode.FailOperation,
                    cancellationToken).ConfigureAwait(false);
                if (hookArgs.Veto is { } veto)
                {
                    results.Add(TestToolCallResults.FromPreInvocation(
                        request,
                        ToolTerminalStatus.Unsupported,
                        $"The call was vetoed before invocation: {veto.SafeReason}"));
                    continue;
                }

                request = WithRawArguments(request, ToRawArguments(hookArgs.Arguments));
            }

            ReceivedRequests.Add(request);
            var invocation = _handler(request);
            results.Add(TestToolCallResults.FromInvocation(request, invocation));
        }

        return new ToolBatchResult(results.ToImmutable());
    }

    private static ToolCallPart CreateCallPart(ToolCatalogSnapshot snapshot, ToolCallRequest request)
    {
        ToolId? resolvedId = null;
        ToolVersion? resolvedVersion = null;
        if (snapshot.ProviderAliases.TryGetValue(request.ProviderAlias, out var identity))
        {
            foreach (var tool in snapshot.Tools)
            {
                if (tool.Id == identity.Id && tool.Version == identity.Version)
                {
                    resolvedId = tool.Id;
                    resolvedVersion = tool.Version;
                    break;
                }
            }
        }

        var toolRef = new ToolReference(request.ProviderAlias, resolvedId, resolvedVersion);
        var arguments = ParseRawArguments(request.RawArguments);
        return new ToolCallPart(request.CallId, toolRef, arguments, providerCallId: null, ExtensionData.Empty);
    }

    private static ToolCallRequest WithRawArguments(ToolCallRequest request, ImmutableArray<byte> rawArguments) =>
        new(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.TurnId,
            request.OperationId,
            request.CallId,
            request.Authorization,
            request.CatalogVersion,
            request.SourceOrdinal,
            request.ProviderAlias,
            rawArguments,
            request.RequestedAt);

    private static ImmutableArray<byte> ToRawArguments(JsonElement arguments) =>
        arguments.ValueKind is JsonValueKind.Undefined
            ? ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes("{}"))
            : ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes(arguments.GetRawText()));

    private static JsonElement ParseRawArguments(ImmutableArray<byte> rawArguments) =>
        rawArguments.IsDefaultOrEmpty
            ? JsonDocument.Parse("{}").RootElement
            : JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(rawArguments.AsSpan())).RootElement;
}
