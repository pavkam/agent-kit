// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

public sealed class SecurityAuthorizationCapturedTests
{
    [Fact]
    public void Constructor_WhenAuthorizationIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptured(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Properties_WhenApiShapeIsInspected_ExposeNoSetters() =>
        typeof(SecurityAuthorizationCaptured).GetProperties().ShouldAllBe(static property => property.SetMethod == null);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var authorization = SecurityAbstractionsTestData.Authorization();
        var captured = new SecurityAuthorizationCaptured(authorization);
        captured.Authorization.ShouldBe(authorization);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuthorizationCaptured(SecurityAbstractionsTestData.Authorization());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
