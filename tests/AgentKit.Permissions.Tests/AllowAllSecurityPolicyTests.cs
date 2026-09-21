// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies AllowAllSecurityPolicy behavior and contracts.</summary>
public sealed class AllowAllSecurityPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_WhenRequestIsNull_RejectsExactArgument()
    {
        var policy = new AllowAllSecurityPolicy();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await policy.EvaluateAsync(
                null!,
                SecurityAuthorityTestData.PolicyContext(SecurityAuthorityTestData.CreateRequest()),
                TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("request");
    }

    [Theory]
    [InlineData(SecurityOperationKind.FileRead)]
    [InlineData(SecurityOperationKind.Process)]
    [InlineData(SecurityOperationKind.StateMutation)]
    [InlineData(SecurityOperationKind.StateRead)]
    [InlineData(SecurityOperationKind.Network)]
    [InlineData(SecurityOperationKind.Delegation)]
    [InlineData(SecurityOperationKind.Artifact)]
    public async Task EvaluateAsync_WhenGivenAnyOperationKind_Allows(SecurityOperationKind kind)
    {
        var policy = new AllowAllSecurityPolicy();
        var request = SecurityAuthorityTestData.CreateRequest() with { Kind = kind };

        var result = await policy.EvaluateAsync(
            request,
            SecurityAuthorityTestData.PolicyContext(request),
            TestContext.Current.CancellationToken);

        result.Kind.ShouldBe(SecurityPolicyResultKind.Allow);
        result.Code.ShouldNotBeNullOrWhiteSpace();
        result.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EvaluateAsync_WhenCancelledBeforeEvaluation_Throws()
    {
        var policy = new AllowAllSecurityPolicy();
        var request = SecurityAuthorityTestData.CreateRequest();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), cancellation.Token));
    }
}
