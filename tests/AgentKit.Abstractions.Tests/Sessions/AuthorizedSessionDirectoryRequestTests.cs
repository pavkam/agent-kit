// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies AuthorizedSessionDirectoryRequest behavior and contracts.</summary>
public sealed class AuthorizedSessionDirectoryRequestTests
{
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(null!, Grant(), Intent()));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(SessionsTestData.BeforeRunContext(), null!, Intent()));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIntentIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedSessionDirectoryRequest<SessionOperationContext>(SessionsTestData.BeforeRunContext(), Grant(), null!));
        exception.ParamName.ShouldBe("intent");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = SessionsTestData.BeforeRunContext();
        var grant = Grant();
        var intent = Intent();
        var authorized = new AuthorizedSessionDirectoryRequest<SessionOperationContext>(request, grant, intent);
        authorized.Request.ShouldBe(request);
        authorized.Grant.ShouldBe(grant);
        authorized.Intent.ShouldBe(intent);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AuthorizedSessionDirectoryRequest<SessionOperationContext>(SessionsTestData.BeforeRunContext(), Grant(), Intent());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SecurityGrant Grant() => SessionsTestData.Grant();

    private static SecurityEnforcementIntent Intent() =>
        new(new SecurityEnforcementIntentId(Guid.Parse("b0000000-0000-0000-0000-000000000001")), null);
}
