// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text.Json;

using AgentKit;
using AgentKit.TestSupport;

/// <summary>
/// Deterministic builders for the spec-shaped tool-runtime batch and executor contract values shared across the
/// Tools fixture files under workstream 4. Every identity is a fixed GUID so equality assertions never depend on
/// generation order. Identity properties carry a <c>Test</c> prefix so a consuming file's <c>using static</c>
/// import cannot collide with the identically named <see cref="AgentKit"/> identity types themselves.
/// </summary>
internal static class ToolRuntimeTestFixture
{
    public static AgentId TestAgentId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000001"));
    public static SessionId TestSessionId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000002"));
    public static RunId TestRunId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000003"));
    public static TurnId TestTurnId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000004"));
    public static OperationId TestOperationId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000005"));
    public static ToolCallId CallId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-000000000006"));

    public static InRunOperationCorrelation Correlation() => new(TestOperationId, TestRunId, TestTurnId);

    public static ExecutionIdentity Identity() =>
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static SecurityAuthorizationScope Scope() => new(TestAgentId, TestSessionId, Correlation());

    public static SecurityAuthorizationContext Authorization() => new(
        new SecurityProfileKey("test-security"),
        new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("b0000000-0000-0000-0000-000000000007")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:test-policy")),
        new ComponentKey<ISecurityAuthority>("test-authority"),
        new AgentDefinitionRevision(1),
        new ConfigurationVersion(1),
        Scope(),
        Identity());

    public static ProtectedResource Resource() => new(ProtectedResourceKind.ApplicationState, "tool:test");

    public static SecurityGrant Grant() => new(
        new GrantId(Guid.Parse("b0000000-0000-0000-0000-000000000008")),
        new SecurityRequestId(Guid.Parse("b0000000-0000-0000-0000-000000000009")),
        Scope(),
        Identity(),
        Authorization(),
        new ComponentId("tool"),
        SecurityOperationKind.StateRead,
        SecurityEffect.Observe,
        [Resource()],
        new InputFingerprint("sha256:input"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1),
        1);

    public static ToolAlias ProviderAlias() => new("provider-tool");

    public static ToolCatalogVersion CatalogVersion() => new("catalog-1");

    public static ToolId ToolId() => new("tool");

    public static ToolVersion ToolVersion() => new("1.0");

    public static ToolDescriptor Descriptor(ToolId? id = null, ToolVersion? version = null) => new(
        id ?? ToolId(),
        version ?? ToolVersion(),
        "Tool",
        "A test tool.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), JsonDocument.Parse("{}").RootElement),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, null, null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
        new ToolSourceId("agentkit.tools.tests"),
        ExtensionData.Empty);

    public static ToolCallRequest CallRequest(int sourceOrdinal = 0) => new(
        TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), CatalogVersion(),
        sourceOrdinal, ProviderAlias(), [1, 2, 3], DateTimeOffset.UnixEpoch);

    public static ToolInvocationContext InvocationContext(ToolDescriptor? tool = null, int attempt = 1) => new(
        TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, tool ?? Descriptor(),
        (tool ?? Descriptor()).Version, JsonDocument.Parse("{}").RootElement, Grant(), attempt,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), Progress());

    public static IToolProgressReporter Progress() => new NoopProgressReporter();

    public static IToolInvokerLease InvokerLease(ToolDescriptor? tool = null) => new FakeInvokerLease(tool ?? Descriptor());

    public static ToolExecutionHints ExecutionHints() => new(ToolSchedulingMode.Unspecified, null, null, null);

    public static ToolBatchEntry BatchEntry(int sourceOrdinal = 0) => new(
        InvocationContext(), InvokerLease(), ExecutionHints(), sourceOrdinal);

    public static SessionExecutionCapability SessionCapability() => new(
        TestSecurityEvidence.SessionProfile(), new UnsupportedSessionCoordinator(), new UnsupportedSessionRunCoordinator());

    public static BudgetExecutionCapability BudgetCapability() => new(
        new BudgetProfileKey("standard"), new BudgetProfileVersion(1), Identity(), Correlation(),
        new FakeBudgetScope(new BudgetScopeId(Guid.Parse("b0000000-0000-0000-0000-00000000000a")), BudgetAddress()));

    public static BudgetScopeAddress BudgetAddress() =>
        new(new TenantId("tenant"), new PrincipalId("principal"), TestAgentId, TestSessionId, TestRunId, TestOperationId);

    public static ToolExecutionCapability ExecutionCapability() => new(SessionCapability(), BudgetCapability());

    /// <summary>Builds a minimal pre-invocation rejection result usable as a <see cref="ToolBatchResult"/> entry.</summary>
    public static ToolCallResult RejectedCallResult() => new(
        TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, Authorization(), grantId: null, acceptance: null,
        ProviderAlias(), toolId: null, toolVersion: null, effects: null, externalIdempotencyKey: null,
        new ToolCallAdmissionEvidence(CatalogVersion(), 0, new InputFingerprint("sha256:raw")),
        ToolTerminalStatus.UnknownTool, content: [], new ToolError(ToolErrorKind.Tool, "Unknown tool.", null, null, ExtensionData.Empty),
        SideEffectCertainty.DefinitelyNotPerformed, usage: null, retryable: false,
        new ToolResultNormalizationSnapshot(
            new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(1)),
            new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1)),
            executionPolicy: null,
            new ToolResultNormalizationAlgorithmVersion(1),
            new ToolResultBounds(1024, 4),
            ToolResultProjectionTransformations.None,
            ExtensionData.Empty),
        new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty),
        new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1)),
        DateTimeOffset.UnixEpoch, invocationStartedAt: null, DateTimeOffset.UnixEpoch, ExtensionData.Empty);

    private sealed class NoopProgressReporter: IToolProgressReporter
    {
        public ValueTask ReportAsync(string message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FakeInvokerLease(ToolDescriptor tool): IToolInvokerLease
    {
        public ToolDescriptor Tool { get; } = tool;
        public ToolSourceVersion SourceVersion { get; } = new("1");
        public IToolInvoker Invoker => throw new NotSupportedException("This test double does not support invocation.");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeBudgetScope(BudgetScopeId id, BudgetScopeAddress address): IBudgetScope
    {
        public BudgetScopeId Id { get; } = id;
        public BudgetScopeAddress Address { get; } = address;

        public ValueTask<BudgetReservationResult> ReserveAsync(
            BudgetReservationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
            ImmutableArray<BudgetReservationRequest> requests, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
