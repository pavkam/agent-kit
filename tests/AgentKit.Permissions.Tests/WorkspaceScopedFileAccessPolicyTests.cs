// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies WorkspaceScopedFileAccessPolicy behavior and contracts.</summary>
public sealed class WorkspaceScopedFileAccessPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_WhenRequestIsNull_RejectsExactArgument()
    {
        var policy = new WorkspaceScopedFileAccessPolicy();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await policy.EvaluateAsync(
                null!,
                SecurityAuthorityTestData.PolicyContext(SecurityAuthorityTestData.CreateRequest()),
                TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("request");
    }

    [Theory]
    [InlineData(SecurityOperationKind.FileRead)]
    [InlineData(SecurityOperationKind.DirectoryRead)]
    [InlineData(SecurityOperationKind.FileSearch)]
    [InlineData(SecurityOperationKind.FileWrite)]
    [InlineData(SecurityOperationKind.DirectoryCreate)]
    public async Task EvaluateAsync_WhenKindIsGovernedAndResourceIsSafe_Allows(SecurityOperationKind kind)
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = Request(kind, ProtectedResourceKind.File, "notes/todo.txt");

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        result.Kind.ShouldBe(SecurityPolicyResultKind.Allow);
        result.Code.ShouldNotBeNullOrWhiteSpace();
        result.SafeMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(SecurityOperationKind.Process)]
    [InlineData(SecurityOperationKind.Network)]
    [InlineData(SecurityOperationKind.StateMutation)]
    [InlineData(SecurityOperationKind.StateRead)]
    [InlineData(SecurityOperationKind.Delegation)]
    [InlineData(SecurityOperationKind.Artifact)]
    public async Task EvaluateAsync_WhenKindIsNotGoverned_Abstains(SecurityOperationKind kind)
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = Request(kind, ProtectedResourceKind.File, "notes/todo.txt");

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        AssertAbstain(result);
    }

    [Fact]
    public async Task EvaluateAsync_WhenResourceKindIsNotFileOrDirectory_Abstains()
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = Request(SecurityOperationKind.FileRead, ProtectedResourceKind.NetworkEndpoint, "example.com:443");

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        AssertAbstain(result);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("c:\\notes\\todo.txt")]
    [InlineData(@"\\server\share\file.txt")]
    public async Task EvaluateAsync_WhenIdentifierIsRooted_Abstains(string identifier)
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = Request(SecurityOperationKind.FileRead, ProtectedResourceKind.File, identifier);

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        AssertAbstain(result);
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("notes/../../etc/passwd")]
    [InlineData("..")]
    public async Task EvaluateAsync_WhenIdentifierContainsATraversalSegment_Abstains(string identifier)
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = Request(SecurityOperationKind.FileWrite, ProtectedResourceKind.File, identifier);

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        AssertAbstain(result);
    }

    [Fact]
    public async Task EvaluateAsync_WhenDirectoryIdentifierIsDot_Allows()
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = Request(SecurityOperationKind.DirectoryRead, ProtectedResourceKind.Directory, ".");

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        result.Kind.ShouldBe(SecurityPolicyResultKind.Allow);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllResourcesAreSafe_Allows()
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = SecurityAuthorityTestData.CreateRequest() with
        {
            Kind = SecurityOperationKind.FileWrite,
            Resources = [
                new ProtectedResource(ProtectedResourceKind.File, "a/one.txt"),
                new ProtectedResource(ProtectedResourceKind.Directory, "a/b"),
            ],
        };

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        result.Kind.ShouldBe(SecurityPolicyResultKind.Allow);
    }

    [Fact]
    public async Task EvaluateAsync_WhenOneOfSeveralResourcesIsUnsafe_Abstains()
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = SecurityAuthorityTestData.CreateRequest() with
        {
            Kind = SecurityOperationKind.FileWrite,
            Resources = [
                new ProtectedResource(ProtectedResourceKind.File, "a/one.txt"),
                new ProtectedResource(ProtectedResourceKind.File, "/etc/passwd"),
            ],
        };

        var result = await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), TestContext.Current.CancellationToken);

        AssertAbstain(result);
    }

    [Fact]
    public async Task EvaluateAsync_WhenCancelledBeforeEvaluation_Throws()
    {
        var policy = new WorkspaceScopedFileAccessPolicy();
        var request = Request(SecurityOperationKind.FileRead, ProtectedResourceKind.File, "notes/todo.txt");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await policy.EvaluateAsync(request, SecurityAuthorityTestData.PolicyContext(request), cancellation.Token));
    }

    private static void AssertAbstain(SecurityPolicyResult result)
    {
        result.Kind.ShouldBe(SecurityPolicyResultKind.Abstain);
        result.Code.ShouldBeNull();
        result.SafeMessage.ShouldBeNull();
    }

    private static SecurityRequest Request(SecurityOperationKind kind, ProtectedResourceKind resourceKind, string identifier) =>
        SecurityAuthorityTestData.CreateRequest() with
        {
            Kind = kind,
            Resources = [new ProtectedResource(resourceKind, identifier)],
        };
}
