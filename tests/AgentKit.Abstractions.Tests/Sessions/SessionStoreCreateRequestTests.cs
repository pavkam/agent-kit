// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionStoreCreateRequest behavior and contracts.</summary>
public sealed class SessionStoreCreateRequestTests
{
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreCreateRequest(null!, SessionsTestData.Address(), Context()));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenAddressIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreCreateRequest(SessionsTestData.CreateRequest(), null!, Context()));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionStoreCreateRequest(SessionsTestData.CreateRequest(), SessionsTestData.Address(), null!));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenAddressAgentIdDiffersFromRequest_ThrowsExactArgumentException()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), SessionsTestData.SessionId);
        var exception = Should.Throw<ArgumentException>(() => new SessionStoreCreateRequest(SessionsTestData.CreateRequest(), address, Context()));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void Constructor_WhenContextIsLaneBound_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionStoreCreateRequest(SessionsTestData.CreateRequest(), SessionsTestData.Address(), SessionsTestData.BeforeRunContext(laneBound: true)));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = SessionsTestData.CreateRequest();
        var address = SessionsTestData.Address();
        var context = Context();
        var storeRequest = new SessionStoreCreateRequest(request, address, context);
        storeRequest.Request.ShouldBe(request);
        storeRequest.Address.ShouldBe(address);
        storeRequest.Context.ShouldBe(context);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionStoreCreateRequest(SessionsTestData.CreateRequest(), SessionsTestData.Address(), Context());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static SessionOperationContext Context() =>
        new(SessionsTestData.AgentId, SessionsTestData.SessionId, null, SessionsTestData.BeforeRun(), SessionsTestData.Identity(),
            SessionsTestData.Authorization(SessionsTestData.BeforeRun(), SessionsTestData.SessionId));
}
