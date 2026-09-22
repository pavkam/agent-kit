// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;
using AgentKit.TestSupport;

/// <summary>Runs the shared approval-store contract against the durable SQLite adapter.</summary>
public sealed class SqliteApprovalStoreTests: ApprovalStoreConformanceTests<SqliteApprovalStoreConformanceFixture>
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    protected override SqliteApprovalStoreConformanceFixture CreateFixture() => new();

    /// <summary>Verifies the durable SQLite approval store declares durable, trusted-control-plane capabilities.</summary>
    [Fact]
    public void Capabilities_WhenAccessed_DeclaresDurableTrustedControlPlane()
    {
        var directory = TestTemporaryDirectory.Create();
        try
        {
            var store = CreateStore(Path.Combine(directory, "approvals.db"));
            store.Capabilities.IsDurable.ShouldBeTrue();
            store.Capabilities.ProvidesTrustedControlPlane.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies approval evidence survives process-level store recreation against the same database.</summary>
    [Fact]
    public async Task CreateAndResolveAsync_WhenStoreIsDisposedAndReopened_RetainsEvidence()
    {
        var directory = TestTemporaryDirectory.Create();
        var databasePath = Path.Combine(directory, "approvals.db");
        var instanceId = new SqliteApprovalStoreInstanceId(Guid.NewGuid());
        var target = new SqliteApprovalStoreTarget(
            databasePath, instanceId, SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        var settings = SqliteApprovalStoreSettings.CreateDefault();
        var request = CreateRequest();
        var response = CreateResponse(request);

        var store = CreateStore(target, settings);
        await store.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        (await store.CreateAsync(request, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreCreateResult.Created);
        (await store.ResolveAsync(response, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreResolveResult.Resolved);

        var reopened = CreateStore(target, settings);
        await reopened.InitializeTrustedAsync(cancellationToken: TestContext.Current.CancellationToken);
        var retained = await reopened.ReadAsync(request.Id, TestContext.Current.CancellationToken);
        retained.Request.ShouldBe(request);
        retained.Response.ShouldBe(response);

        Directory.Delete(directory, recursive: true);
    }

    /// <summary>Verifies create before initialization fails closed.</summary>
    [Fact]
    public async Task CreateAsync_WhenUninitialized_ThrowsUnavailable()
    {
        var directory = TestTemporaryDirectory.Create();
        try
        {
            var store = CreateStore(Path.Combine(directory, "approvals.db"));
            var request = CreateRequest();

            var exception = await Should.ThrowAsync<SecurityGrantStoreUnavailableException>(
                async () => await store.CreateAsync(request, TestContext.Current.CancellationToken));

            exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static SqliteApprovalStore CreateStore(string databasePath) => CreateStore(
        new SqliteApprovalStoreTarget(
            databasePath,
            new SqliteApprovalStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ApplyKnownMigrations),
        SqliteApprovalStoreSettings.CreateDefault());

    private static SqliteApprovalStore CreateStore(
        SqliteApprovalStoreTarget target, SqliteApprovalStoreSettings settings) =>
        new(target, settings, TimeProvider.System);

    private static ApprovalRequest CreateRequest()
    {
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("requester"), ExecutionSubjectKind.Human);
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000002")), null));
        var securityRequest = new SecurityRequest(
            new SecurityRequestId(Guid.Parse("30000000-0000-0000-0000-000000000003")), scope, null,
            identity, new ComponentId("test"), SecurityOperationKind.FileWrite, SecurityEffect.CreateOrReplace,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint("sha256:input"), _now.AddMinutes(5));
        var binding = new ApprovalScopeBinding(securityRequest, new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1), _now, _now.AddMinutes(5), 1);
        return new ApprovalRequest(new ApprovalRequestId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            binding, "Write workspace file", _now);
    }

    private static ApprovalResponse CreateResponse(ApprovalRequest request) => new(
        new ApprovalResponseId(Guid.Parse("50000000-0000-0000-0000-000000000005")), request.Id,
        request.Binding, ApprovalResolution.Approved,
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("approver"), ExecutionSubjectKind.Human),
        request.CreatedAt.AddSeconds(1));
}
