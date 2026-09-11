// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using AgentKit.TestSupport;

public sealed class SecurityAuthorityBindingTests
{
    [Fact]
    public void Constructor_WhenKeyOrAuthorityIsInvalid_ThrowsWithExactParameterNames()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        var invalidKey = Should.Throw<ArgumentNullException>(() => new SecurityAuthorityBinding(default, new UninvokedSecurityAuthority()));
        var invalidAuthority = Should.Throw<ArgumentNullException>(() => new SecurityAuthorityBinding(key, null!));
        invalidKey.GetType().ShouldBe(typeof(ArgumentNullException));
        invalidKey.ParamName.ShouldBe("key");
        invalidAuthority.GetType().ShouldBe(typeof(ArgumentNullException));
        invalidAuthority.ParamName.ShouldBe("authority");
    }
}
