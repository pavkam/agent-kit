// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

/// <summary>Test double that adapts <see cref="FakeToolInvoker"/> to <see cref="IToolExecutor"/>.</summary>
internal sealed class FakeToolExecutor(FakeToolInvoker orchestrator): IToolExecutor
{
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
            var argumentText = call.RawArguments.IsDefaultOrEmpty
                ? "{}"
                : Encoding.UTF8.GetString(call.RawArguments.AsSpan());
            using var document = JsonDocument.Parse(argumentText);
            var arguments = document.RootElement.Clone();
            var legacyContext = new ToolExecutionContext(
                call.AgentId,
                call.SessionId,
                call.CallId,
                (InRunOperationCorrelation) call.Authorization.Scope.Correlation,
                call.Authorization.Identity,
                call.Authorization,
                capability.Session.Profile);
            var legacyRequest = new LegacyToolCallRequest(
                new ToolReference(call.ProviderAlias, null, null),
                legacyContext,
                arguments,
                call.RequestedAt);
            var resolved = await orchestrator.InvokeAsync(legacyRequest, cancellationToken).ConfigureAwait(false);
            results.Add(TestToolCallResults.FromResolved(call, resolved));
        }

        return new ToolBatchResult(results.ToImmutable());
    }
}
