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

    [Fact]
    public void Equals_WhenKeyAndAuthorityMatch_AreStructurallyEqual()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        var authority = new UninvokedSecurityAuthority();
        var first = new SecurityAuthorityBinding(key, authority);
        var second = new SecurityAuthorityBinding(key, authority);
        var different = new SecurityAuthorityBinding(new ComponentKey<ISecurityAuthority>("security.other"), authority);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(different);
    }

    [Fact]
    public void With_WhenCopyingWithoutChanges_RetainsTheOriginalKeyAndAuthority()
    {
        var key = new ComponentKey<ISecurityAuthority>("security.primary");
        var authority = new UninvokedSecurityAuthority();
        var original = new SecurityAuthorityBinding(key, authority);

        var copy = original with { };

        copy.Key.ShouldBe(key);
        copy.Authority.ShouldBeSameAs(authority);
        copy.ShouldNotBeSameAs(original);
    }
}
