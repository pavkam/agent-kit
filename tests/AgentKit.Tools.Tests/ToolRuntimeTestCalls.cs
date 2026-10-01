// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Text.Json;

using AgentKit.TestSupport;

/// <summary>Deterministic validated calls and authorization evidence shared by the tool-runtime unit tests.</summary>
internal static class ToolRuntimeTestCalls
{
    internal static ToolExecutionPolicyReference Standard { get; } =
        new(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1));

    internal static AgentId AgentId { get; } = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    internal static SessionId SessionId { get; } = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    internal static RunId RunId { get; } = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    internal static TurnId TurnId { get; } = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    internal static OperationId OperationId { get; } = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));

    internal static SecurityAuthorizationContext Authorization()
    {
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return TestSecurityEvidence.Authorization(AgentId, SessionId, new InRunOperationCorrelation(OperationId, RunId, TurnId), identity);
    }

    internal static ValidatedToolCall Validated(ToolExecutionPolicyReference? policy = null, ToolExecutionHints? hints = null, int suffix = 1)
    {
        using var document = JsonDocument.Parse("{}");
        var tool = new ToolDescriptor(
            new ToolId("tool.read"), new ToolVersion("1"), "read", "Read.",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            outputSchema: null, new ToolEffects(ToolEffect.ReadOnly, null, null),
            hints ?? new ToolExecutionHints(ToolSchedulingMode.ParallelSafe, null, null, null),
            new ToolSourceId("source.tests"), ExtensionData.Empty);
        return new ValidatedToolCall(
            AgentId, SessionId, RunId, TurnId, OperationId, new ToolCallId(Guid.Parse($"66666666-6666-6666-6666-66666666666{suffix}")),
            Authorization(), new ToolCatalogVersion("catalog-1"), new ToolAlias("read"), tool, tool.Version, policy ?? Standard, 0,
            JsonDocument.Parse("{}").RootElement, new InputFingerprint("sha256:validated"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    }

    internal static ToolExecutionPolicyContext Context() =>
        new(AgentId, SessionId, RunId, new ToolCatalogVersion("catalog-1"), DateTimeOffset.UnixEpoch);

    internal static ToolInvocationContext Context(ToolEffects effects, IdempotencyKey? key = null)
    {
        using var document = JsonDocument.Parse("{}");
        var tool = new ToolDescriptor(
            new ToolId("tool.write"), new ToolVersion("1"), "write", "Write.",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            outputSchema: null, effects, new ToolExecutionHints(ToolSchedulingMode.Sequential, null, null, null),
            new ToolSourceId("source.tests"), ExtensionData.Empty);
        var authorization = Authorization();
        var callId = new ToolCallId(Guid.Parse("66666666-6666-6666-6666-666666666661"));
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            new SecurityRequestId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            authorization.Scope, authorization.Identity, authorization, ToolInvocationSecurityBinding.SecurityAudience,
            SecurityOperationKind.StateRead, SecurityEffect.Observe,
            [ToolInvocationSecurityBinding.Resource(tool.Id, tool.Version)],
            ToolInvocationSecurityBinding.InvocationFingerprint(callId, new InputFingerprint("sha256:test")),
            authorization.PolicySnapshot.Version, new SecurityRevocationVersion(1), DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
        return new ToolInvocationContext(
            AgentId, SessionId, RunId, TurnId, OperationId, callId, tool, tool.Version, JsonDocument.Parse("{}").RootElement, grant,
            attempt: 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1),
            NoopToolProgressReporter.Instance, sessionProfile: null, key);
    }
}
