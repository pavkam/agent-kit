// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityAuthoritySelectionResult derived behavior and contracts.</summary>
public sealed class SecurityAuthoritySelectionResultTests
{
    [Fact]
    public void SecurityAuthoritySelected_WhenAuthorizationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityAuthoritySelected(null!, new FakeAuthority())).ParamName.ShouldBe("authorization");

    [Fact]
    public void SecurityAuthoritySelected_WhenAuthorityIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityAuthoritySelected(SecurityAbstractionsTestData.Authorization(), null!)).ParamName.ShouldBe("authority");

    [Fact]
    public void SecurityAuthoritySelected_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var authorization = SecurityAbstractionsTestData.Authorization();
        var authority = new FakeAuthority();
        var selected = new SecurityAuthoritySelected(authorization, authority);
        selected.Authorization.ShouldBe(authorization);
        selected.Authority.ShouldBeSameAs(authority);
    }

    [Fact]
    public void SecurityAuthoritySelected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuthoritySelected(SecurityAbstractionsTestData.Authorization(), new FakeAuthority());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SecurityAuthoritySelectionUnavailable_WhenAuthorizationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SecurityAuthoritySelectionUnavailable(null!, "unavailable")).ParamName.ShouldBe("authorization");

    [Fact]
    public void SecurityAuthoritySelectionUnavailable_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityAuthoritySelectionUnavailable(SecurityAbstractionsTestData.Authorization(), " ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void SecurityAuthoritySelectionUnavailable_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var authorization = SecurityAbstractionsTestData.Authorization();
        var result = new SecurityAuthoritySelectionUnavailable(authorization, "unavailable");
        result.Authorization.ShouldBe(authorization);
        result.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void SecurityAuthoritySelectionUnavailable_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuthoritySelectionUnavailable(SecurityAbstractionsTestData.Authorization(), "unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private sealed class FakeAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
