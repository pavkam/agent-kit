// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessRunRequest behavior and contracts.</summary>
public sealed class ProcessRunRequestTests
{
    [Fact]
    public void Constructor_WhenIntentIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ProcessRunRequest(null!, SecurityTestData.Grant())).ParamName.ShouldBe("intent");

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ProcessRunRequest(HostTestData.ResolvedIntent(), null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var intent = HostTestData.ResolvedIntent();
        var grant = SecurityTestData.Grant();
        var request = new ProcessRunRequest(intent, grant);
        request.Intent.ShouldBeSameAs(intent);
        request.Grant.ShouldBeSameAs(grant);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProcessRunRequest(HostTestData.ResolvedIntent(), SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
