// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies DefaultIdentityValidationPolicy behavior and contracts.</summary>
public sealed class DefaultIdentityValidationPolicyTests
{
    [Theory]
    [InlineData(0, "issuers")]
    [InlineData(1, "timeProvider")]
    [InlineData(2, "options")]
    public void DefaultIdentityValidationPolicy_WhenDependencyIsNull_ThrowsWithParameterName(int dependency, string parameterName)
    {
        var issuers = new IdentityIssuerCatalog([]);
        var clock = new FakeTimeProvider();
        var options = new AgentIdentityOptionsSnapshot(false, 1, TimeSpan.Zero, TimeSpan.FromHours(1));
        var exception = Should.Throw<ArgumentNullException>(() => _ = dependency switch
        {
            0 => new DefaultIdentityValidationPolicy(null!, clock, options),
            1 => new DefaultIdentityValidationPolicy(issuers, null!, options),
            _ => new DefaultIdentityValidationPolicy(issuers, clock, null!),
        });
        exception.ParamName.ShouldBe(parameterName);
    }
}
