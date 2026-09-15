// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

using AgentKit.TestSupport;

/// <summary>Verifies approval scope bounds and structural equality.</summary>
public sealed class ApprovalScopeBindingTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenUsesExceedRequest_ThrowsBeforeBinding()
    {
        var request = CreateRequest(requestedUses: 1);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalScopeBinding(
            request,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            _now,
            _now.AddMinutes(1),
            2));

        exception.ParamName.ShouldBe("allowedUses");
    }

    [Fact]
    public void Constructor_WhenExpiryExceedsRequestDeadline_ThrowsBeforeBinding()
    {
        var request = CreateRequest();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalScopeBinding(
            request,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            _now,
            request.Deadline.AddSeconds(1),
            1));

        exception.ParamName.ShouldBe("expiresAt");
    }

    private static SecurityRequest CreateRequest(int requestedUses = 1)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("11000000-0000-0000-0000-000000000001")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("21000000-0000-0000-0000-000000000002")),
                null));
        return new SecurityRequest(
            new SecurityRequestId(Guid.Parse("31000000-0000-0000-0000-000000000003")),
            scope,
            null,
            TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("requester"),
                ExecutionSubjectKind.Human),
            new ComponentId("test"),
            SecurityOperationKind.FileWrite,
            SecurityEffect.CreateOrReplace,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint("sha256:input"),
            _now.AddMinutes(5),
            requestedUses);
    }
}
