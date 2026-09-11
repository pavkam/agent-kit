// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using AgentKit;

/// <summary>Verifies NormalizedHost behavior and contracts.</summary>
public sealed class NormalizedHostTests: Conformance.StringIdentityConformanceTests<NormalizedHost>
{
    [Theory]
    [InlineData("bad host")]
    [InlineData("fe80::1%4")]
    [InlineData("/")]
    public void NormalizedHost_WhenHostInvalid_ThrowsWithExactParameter(string value)
    {
        Action action = () =>
        {
            _ = new NormalizedHost(value);
        };
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("value");
    }

    [Fact]
    public void NormalizedHost_WhenUnicodeAndTrailingDot_CanonicalizesToAsciiDnsIdentity()
    {
        var host = new NormalizedHost("BÜCHER.example.");
        host.Value.ShouldBe("xn--bcher-kva.example");
    }

    /// <inheritdoc/>
    protected override NormalizedHost Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(NormalizedHost subject) => subject.Value;
}
