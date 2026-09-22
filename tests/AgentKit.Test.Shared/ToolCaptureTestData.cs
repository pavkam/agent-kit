// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;
using System.Text.Json;

/// <summary>Creates typed deterministic source publications and invoker bindings for capture contract tests.</summary>
public static class ToolCaptureTestData
{
    /// <summary>Creates an immutable same-source descriptor with an owned canonical input schema.</summary>
    /// <param name="source">The nonblank stable source identity.</param>
    /// <param name="id">The nonblank canonical identity.</param><param name="version">The nonblank exact tool version.</param><param name="description">The descriptor content, optionally containing a redaction sentinel.</param>
    /// <returns>The immutable test descriptor.</returns>
    public static ToolDescriptor Descriptor(string id = "tool.read", string version = "1", string description = "Read captured data", string source = "source.tests")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        using var document = JsonDocument.Parse("{}");
        return new ToolDescriptor(new ToolId(id), new ToolVersion(version), "read", description,
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            null, new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null), new ToolSourceId(source), ExtensionData.Empty);
    }

    /// <summary>Captures ordered descriptors under one explicitly selected test source version.</summary>
    /// <param name="tools">The initialized ordered descriptors.</param><param name="version">The nonblank exact source version.</param>
    /// <returns>A validated immutable source publication.</returns>
    public static ToolProviderSnapshot Snapshot(ImmutableArray<ToolDescriptor> tools, string version = "source-1")
    {
        ArgumentException.ThrowIfContainsNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return new ToolProviderSnapshot(new ToolSourceId("source.tests"), new ToolSourceVersion(version), tools);
    }

    /// <summary>Creates one exact typed binding without invoking or reading metadata from the invoker.</summary>
    /// <param name="tool">The nonnull captured descriptor.</param><param name="invoker">The nonnull bound instance.</param>
    /// <returns>An immutable single-entry binding map with exact domain comparers.</returns>
    public static ImmutableDictionary<ToolIdentity, IToolInvoker> Bindings(ToolDescriptor tool, IToolInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(invoker);
        return ImmutableDictionary<ToolIdentity, IToolInvoker>.Empty.Add(new ToolIdentity(tool.Id, tool.Version), invoker);
    }
    /// <summary>Builds a deterministic run-bound catalog selecting an explicit descriptor subset.</summary>
    /// <param name="sources">Initialized exact source publications, including empty sources.</param><param name="tools">Initialized selected descriptors.</param><param name="version">The nonblank catalog version.</param>
    /// <returns>The locally validated selection evidence; this helper acquires no live binding.</returns>
    public static ToolCatalogSnapshot Catalog(ImmutableArray<ToolProviderSnapshot> sources, ImmutableArray<ToolDescriptor> tools, string version = "catalog-1")
    {
        ArgumentException.ThrowIfContainsNull(sources);
        ArgumentException.ThrowIfContainsNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return new ToolCatalogSnapshot(
            new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SecurityPolicyVersion(1), new ContentHash("sha256:test")),
            new AgentDefinitionRevision(0), new ConfigurationVersion(1), new ToolCatalogVersion(version),
            sources.ToImmutableDictionary(static source => source.SourceId, static source => source.SourceVersion), tools,
            tools.ToImmutableDictionary(static tool => new ToolIdentity(tool.Id, tool.Version), static _ => new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1))), []);
    }

    /// <summary>Creates a locally coherent discovery request with explicit deterministic security and configuration evidence.</summary>
    /// <param name="principal">The nonblank test principal used for correlation-isolation scenarios.</param>
    /// <param name="runId">An optional explicit run identity; null selects the deterministic test run.</param>
    /// <returns>A complete immutable request with no authored toolset selection or authority grant.</returns>
    public static ToolDiscoveryRequest Discovery(string principal = "principal", RunId? runId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        var agent = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var session = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var run = runId ?? new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        ArgumentOutOfRangeException.ThrowIfEqual(run, default, nameof(runId));
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId(principal), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444")), run, null);
        var authorization = TestSecurityEvidence.Authorization(agent, session, correlation, identity);
        return new ToolDiscoveryRequest(agent, session, run, identity, authorization, authorization.AgentDefinitionRevision,
            new EffectiveConfigurationSnapshot(authorization.ConfigurationVersion, new ContentHash("sha256:configuration"), [], []), [],
            new ModelCapabilities(true, true, true, true, false, false, false, ExtensionData.Empty));
    }

    /// <summary>Builds a minimal spec-shaped invocation context for capture and lease tests.</summary>
    /// <param name="tool">The resolved descriptor, or a deterministic default when null.</param>
    /// <returns>A structurally valid context whose grant retains authorization evidence.</returns>
    public static ToolInvocationContext InvocationContext(ToolDescriptor? tool = null)
    {
        var descriptor = tool ?? Descriptor();
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var runId = new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var turnId = new TurnId(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        var operationId = new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var callId = new ToolCallId(Guid.Parse("66666666-6666-6666-6666-666666666666"));
        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        using var arguments = JsonDocument.Parse("{}");
        var grant = new SecurityGrant(
            new GrantId(Guid.Parse("77777777-7777-7777-7777-777777777777")),
            new SecurityRequestId(Guid.Parse("88888888-8888-8888-8888-888888888888")),
            authorization.Scope,
            identity,
            authorization,
            new ComponentId("tool"),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, "tool:test")],
            new InputFingerprint("sha256:input"),
            authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            1);
        return new ToolInvocationContext(
            agentId,
            sessionId,
            runId,
            turnId,
            operationId,
            callId,
            descriptor,
            descriptor.Version,
            arguments.RootElement.Clone(),
            grant,
            attempt: 1,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            NoopProgressReporter.Instance);
    }

    private sealed class NoopProgressReporter: IToolProgressReporter
    {
        internal static NoopProgressReporter Instance { get; } = new();

        public ValueTask ReportAsync(string message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
