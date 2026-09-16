// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies IdentityIssuerBinding behavior and contracts.</summary>
public sealed class IdentityIssuerBindingTests
{
    [Fact]
    public void RuntimeBindings_WhenDependencyIsNull_ThrowWithParameterName()
    {
        var registration = new IdentityIssuerRegistration(new IdentityIssuerId("issuer"));
        var issuer = new TestIssuer(new TestIssuerSettings(DateTimeOffset.MinValue, DateTimeOffset.MaxValue), new IdentityIssuerId("issuer"));
        Should.Throw<ArgumentNullException>(() => new IdentityIssuerBinding(null!, registration)).ParamName.ShouldBe("issuer");
        Should.Throw<ArgumentNullException>(() => new IdentityIssuerBinding(issuer, null!)).ParamName.ShouldBe("registration");
    }

    [Fact]
    public void Properties_WhenConstructed_ExposeTheExactCapturedValues()
    {
        var registration = new IdentityIssuerRegistration(new IdentityIssuerId("issuer"));
        var issuer = new TestIssuer(new TestIssuerSettings(DateTimeOffset.MinValue, DateTimeOffset.MaxValue), new IdentityIssuerId("issuer"));

        var binding = new IdentityIssuerBinding(issuer, registration);

        binding.Issuer.ShouldBeSameAs(issuer);
        binding.Registration.ShouldBeSameAs(registration);
        binding.ToString().ShouldContain(nameof(IdentityIssuerBinding));
        (binding with { }).ShouldBe(binding);
    }
}
