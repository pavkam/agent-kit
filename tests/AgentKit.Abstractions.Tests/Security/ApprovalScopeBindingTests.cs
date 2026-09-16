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

    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ApprovalScopeBinding(null!, new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), _now, _now.AddMinutes(1), 1)).ParamName.ShouldBe("request");

    [Fact]
    public void Constructor_WhenAllowedUsesIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalScopeBinding(CreateRequest(), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), _now, _now.AddMinutes(1), 0)).ParamName.ShouldBe("allowedUses");

    [Fact]
    public void Constructor_WhenExpiresAtIsNotAfterNotBefore_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalScopeBinding(CreateRequest(), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), _now, _now, 1)).ParamName.ShouldBe("expiresAt");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = CreateRequest();
        var binding = Binding(request);
        binding.Request.ShouldBe(request);
        binding.PolicyVersion.ShouldBe(new SecurityPolicyVersion(1));
        binding.RevocationVersion.ShouldBe(new SecurityRevocationVersion(1));
        binding.NotBefore.ShouldBe(_now);
        binding.ExpiresAt.ShouldBe(_now.AddMinutes(1));
        binding.AllowedUses.ShouldBe(1);
    }

    [Fact]
    public void Equality_WhenSameValues_InstancesAreEqualWithMatchingHashCode()
    {
        var request = CreateRequest();
        var first = Binding(request);
        var second = Binding(request);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOtherIsNull_IsNotEqual() => Binding(CreateRequest()).Equals(null).ShouldBeFalse();

    [Fact]
    public void Equality_WhenPolicyVersionDiffers_IsNotEqual()
    {
        var request = CreateRequest();
        Binding(request).ShouldNotBe(Binding(request, policyVersion: new SecurityPolicyVersion(2)));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Binding(CreateRequest());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ApprovalScopeBinding Binding(SecurityRequest request, SecurityPolicyVersion? policyVersion = null) =>
        new(request, policyVersion ?? new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), _now, _now.AddMinutes(1), 1);

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
