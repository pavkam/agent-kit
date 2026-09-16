// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies AuthorizedSessionStoreRequest behavior and contracts.</summary>
public sealed class AuthorizedSessionStoreRequestTests
{
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedSessionStoreRequest<SessionOperationContext>(null!, StoreKey(), Grant(), Intent()));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenStoreKeyIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedSessionStoreRequest<SessionOperationContext>(SessionsTestData.BeforeRunContext(), default, Grant(), Intent()));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("storeKey");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedSessionStoreRequest<SessionOperationContext>(SessionsTestData.BeforeRunContext(), StoreKey(), null!, Intent()));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Constructor_WhenIntentIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AuthorizedSessionStoreRequest<SessionOperationContext>(SessionsTestData.BeforeRunContext(), StoreKey(), Grant(), null!));
        exception.ParamName.ShouldBe("intent");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = SessionsTestData.BeforeRunContext();
        var storeKey = StoreKey();
        var grant = Grant();
        var intent = Intent();
        var authorized = new AuthorizedSessionStoreRequest<SessionOperationContext>(request, storeKey, grant, intent);
        authorized.Request.ShouldBe(request);
        authorized.StoreKey.ShouldBe(storeKey);
        authorized.Grant.ShouldBe(grant);
        authorized.Intent.ShouldBe(intent);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AuthorizedSessionStoreRequest<SessionOperationContext>(SessionsTestData.BeforeRunContext(), StoreKey(), Grant(), Intent());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionStoreKey StoreKey() => new("store");
    private static SecurityGrant Grant() => SessionsTestData.Grant();
    private static SecurityEnforcementIntent Intent() =>
        new(new SecurityEnforcementIntentId(Guid.Parse("b0000000-0000-0000-0000-000000000002")), null);
}
