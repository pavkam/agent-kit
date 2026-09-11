// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ProviderEndpointId behavior and contracts.</summary>
public sealed class ProviderEndpointIdTests: Conformance.StringIdentityConformanceTests<ProviderEndpointId>
{
    /// <inheritdoc/>
    protected override ProviderEndpointId Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ProviderEndpointId subject) => subject.Value;
    [Fact]
    public void StringIdentities_WhenTextDiffersOnlyByCase_PreserveOrdinalValues()
    {
        new ProviderEndpointId("Endpoint").ShouldNotBe(new ProviderEndpointId("endpoint"));
        default(ProviderEndpointId).ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new ProviderEndpointId(null!)).ParamName.ShouldBe("value");

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextBlank_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ProviderEndpointId(value));
        _ = exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }
}
