// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

internal static class HostTestData
{
    public static ArtifactReference ArtifactReference(ArtifactId? id = null, string version = "1") => new(
        id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new ArtifactVersion(version),
        new ArtifactDirectoryId("output"),
        new ArtifactProfileKey("test"),
        new ArtifactProfileVersion(1),
        Identity().TenantId,
        new ArtifactOwnerId("session:owner"),
        Identity().PrincipalId,
        "text/plain",
        7,
        new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch),
        ArtifactDataClassification.Internal,
        ArtifactOwnershipKind.Session,
        ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false),
        DateTimeOffset.UnixEpoch);

    public static ExecutionIdentity Identity() =>
        TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    public static SecurityAuthorizationScope Scope() => new(
        new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        null,
        new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), null));

    public static ProcessResolveRequest ResolveRequest(
        ImmutableArray<string>? arguments = null,
        ImmutableArray<ProcessEnvironmentVariable>? environment = null,
        ImmutableArray<ProcessReadOnlyRoot>? readOnlyRoots = null) => new(
        new ProcessOperationId(Guid.Parse("11000000-0000-0000-0000-000000000001")),
        "/bin/sh",
        arguments ?? ["-lc", "command"],
        null,
        environment ?? [new ProcessEnvironmentVariable("SAFE_NAME", "value")],
        [],
        new SandboxProfileId("workspace-no-network-v1"),
        ProcessWorkspaceAccess.ReadWrite,
        ProcessSideEffectClass.WorkspaceMutation,
        ProcessChildPolicy.AllowSandboxed,
        new ProcessResourceLimits(TimeSpan.FromSeconds(10), 1024, TimeSpan.FromSeconds(1)))
        {
            ReadOnlyRoots = readOnlyRoots ?? [],
        };

    public static ResolvedProcessIntent ResolvedIntent() => new(
        ResolveRequest(),
        "/usr/bin/sh",
        new ContentHash("sha256:executable"),
        "/workspace",
        "/workspace",
        new ContentHash("sha256:environment"),
        new ContentHash("sha256:stdin"));
}
