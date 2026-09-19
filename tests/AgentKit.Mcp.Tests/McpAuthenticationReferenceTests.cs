// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpAuthenticationReference"/>.</summary>
public sealed class McpAuthenticationReferenceTests
{
    [Theory]
    [InlineData(null, "audience", "credentialProfileKey")]
    [InlineData("", "audience", "credentialProfileKey")]
    [InlineData("   ", "audience", "credentialProfileKey")]
    [InlineData("profile", null, "audience")]
    [InlineData("profile", " ", "audience")]
    public void Constructor_WhenTextIsMissing_ThrowsArgumentException(string? profile, string? audience, string expectedParamName)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpAuthenticationReference(profile!, audience!));
        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Fact]
    public void Constructor_WhenReferenceIsPresent_DoesNotStoreASecret()
    {
        var reference = new McpAuthenticationReference("oauth-profile", "https://mcp.example");
        reference.CredentialProfileKey.ShouldBe("oauth-profile");
        reference.Audience.ShouldBe("https://mcp.example");
        reference.ToString().ShouldNotContain("secret");
    }
}
