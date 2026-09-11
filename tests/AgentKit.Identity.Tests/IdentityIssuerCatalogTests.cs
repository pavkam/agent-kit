// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies IdentityIssuerCatalog behavior and contracts.</summary>
public sealed class IdentityIssuerCatalogTests
{
    [Fact]
    public void RuntimeBindings_WhenDependencyIsNull_ThrowWithParameterName()
    {
        var registration = new IdentityIssuerRegistration(new IdentityIssuerId("issuer"));
        var issuer = new TestIssuer(new TestIssuerSettings(DateTimeOffset.MinValue, DateTimeOffset.MaxValue), new IdentityIssuerId("issuer"));
        Should.Throw<ArgumentNullException>(() => new IdentityIssuerCatalog(null!)).ParamName.ShouldBe("bindings");
        Should.Throw<ArgumentNullException>(() => new IdentityIssuerCatalog([null!])).ParamName.ShouldBe("binding");
        _ = Should.Throw<ArgumentException>(() => new IdentityIssuerCatalog([new IdentityIssuerBinding(issuer, registration), new IdentityIssuerBinding(issuer, registration)]));
        Should.Throw<ArgumentException>(() => new IdentityIssuerCatalog([]).Find(default)).ParamName.ShouldBe("issuerId");
    }
}
