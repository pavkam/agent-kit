// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderAccountId behavior and contracts.</summary>
public sealed class ProviderAccountIdTests: Conformance.StringIdentityConformanceTests<ProviderAccountId>
{
    [Fact]
    public void CredentialSnapshot_WhenOptionalAccountDefaultOrSkewNegative_ThrowsExactParameter()
    {
        var account = Should.Throw<ArgumentOutOfRangeException>(() => Credential(accountId: default(ProviderAccountId)));
        account.ParamName.ShouldBe("accountId");
        var skew = Should.Throw<ArgumentOutOfRangeException>(() => Credential(refreshSkew: TimeSpan.FromTicks(-1)));
        skew.ParamName.ShouldBe("refreshSkew");
    }

    private static ProviderCredentialProfileReference CredentialReference() => new(new ProviderCredentialProfileKey("credential-profile"), new ProviderCredentialProfileVersion(1));
    private static ProviderCredentialProfileSnapshot Credential(ProviderAccountId? accountId = null, TimeSpan? refreshSkew = null) => new(CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), accountId ?? new ProviderAccountId("account"), refreshSkew ?? TimeSpan.FromMinutes(5), new ContentHash("sha256:credential"), ExtensionData.Empty);
    /// <inheritdoc/>
    protected override ProviderAccountId Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ProviderAccountId subject) => subject.Value;
    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new ProviderAccountId(null!)).ParamName.ShouldBe("value");

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextBlank_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ProviderAccountId(value));
        _ = exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }
}
