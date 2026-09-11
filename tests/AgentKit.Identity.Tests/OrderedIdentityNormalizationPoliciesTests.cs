// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies OrderedIdentityNormalizationPolicies behavior and contracts.</summary>
public sealed class OrderedIdentityNormalizationPoliciesTests
{
    [Fact]
    public void RuntimeBindings_WhenDependencyIsNull_ThrowWithParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new OrderedIdentityNormalizationPolicies(null!)).ParamName.ShouldBe("bindings");
        Should.Throw<ArgumentNullException>(() => new OrderedIdentityNormalizationPolicies([null!])).ParamName.ShouldBe("binding");
    }
}
