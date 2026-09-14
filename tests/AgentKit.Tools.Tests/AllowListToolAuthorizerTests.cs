// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class AllowListToolAuthorizerTests
{
    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AllowListToolAuthorizer(null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAllowListContainsDefaultToolId_ThrowsArgumentException()
    {
        var options = new AgentToolsOptions();
        _ = options.AllowedToolIds.Add(default);

        var exception = Should.Throw<ArgumentException>(
            () => new AllowListToolAuthorizer(Options.Create(options)));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var authorizer = CreateAuthorizer();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => authorizer.AuthorizeAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenNoAllowListConfigured_DeniesEveryCall()
    {
        var authorizer = CreateAuthorizer();
        var request = new ToolAuthorizationRequest(TestFactory.ExecutionContext(), TestFactory.Descriptor("any"));

        var decision = await authorizer.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<ToolAuthorizationDenied>();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenToolInAllowList_GrantsCall()
    {
        var authorizer = CreateAuthorizer(o => o.AllowedToolIds.Add(new ToolId("allowed")));
        var request = new ToolAuthorizationRequest(TestFactory.ExecutionContext(), TestFactory.Descriptor("allowed"));

        var decision = await authorizer.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<ToolAuthorizationGranted>();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenToolNotInAllowList_DeniesCall()
    {
        var authorizer = CreateAuthorizer(o => o.AllowedToolIds.Add(new ToolId("allowed")));
        var request = new ToolAuthorizationRequest(TestFactory.ExecutionContext(), TestFactory.Descriptor("other"));

        var decision = await authorizer.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        var denied = decision.ShouldBeOfType<ToolAuthorizationDenied>();
        denied.SafeMessage.ShouldContain("other");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenOptionsMutatedAfterConstruction_DoesNotGrantNewTool()
    {
        var options = new AgentToolsOptions();
        var authorizer = new AllowListToolAuthorizer(Options.Create(options));
        _ = options.AllowedToolIds.Add(new ToolId("late"));
        var request = new ToolAuthorizationRequest(TestFactory.ExecutionContext(), TestFactory.Descriptor("late"));

        var decision = await authorizer.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<ToolAuthorizationDenied>();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllowAllRegisteredToolsTrue_GrantsCallNotInAllowList()
    {
        var authorizer = CreateAuthorizer(o => o.AllowAllRegisteredTools = true);
        var request = new ToolAuthorizationRequest(TestFactory.ExecutionContext(), TestFactory.Descriptor("not-allow-listed"));

        var decision = await authorizer.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<ToolAuthorizationGranted>();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllowAllRegisteredToolsFalse_DeniesCallNotInAllowList()
    {
        var authorizer = CreateAuthorizer(o => o.AllowAllRegisteredTools = false);
        var request = new ToolAuthorizationRequest(TestFactory.ExecutionContext(), TestFactory.Descriptor("not-allow-listed"));

        var decision = await authorizer.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<ToolAuthorizationDenied>();
    }

    private static AllowListToolAuthorizer CreateAuthorizer(Action<AgentToolsOptions>? configure = null)
    {
        var options = new AgentToolsOptions();
        configure?.Invoke(options);
        return new AllowListToolAuthorizer(Options.Create(options));
    }
}
