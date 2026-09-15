// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Signing;

/// <summary>
/// Verifies that <see cref="AwsSigV4Credential"/> guards its arguments and
/// never renders <see cref="AwsSigV4Credential.SecretAccessKey"/> or
/// <see cref="AwsSigV4Credential.SessionToken"/> through
/// <see cref="object.ToString"/>, while keeping the non-secret
/// <see cref="AwsSigV4Credential.AccessKeyId"/> visible for diagnostics.
/// </summary>
public sealed class AwsSigV4CredentialTests
{
    private const string AccessKeyId = "AKIAIOSFODNN7EXAMPLE";
    private const string SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
    private const string SessionToken = "FwoGZXIvYXdzEBYaDHNlc3Npb24tdG9rZW4=";

    [Fact]
    public void Constructor_WhenAccessKeyIdIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AwsSigV4Credential(null!, SecretAccessKey, null));

        exception.ParamName.ShouldBe("accessKeyId");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenAccessKeyIdIsBlank_ThrowsArgumentException(string accessKeyId)
    {
        var exception = Should.Throw<ArgumentException>(() => new AwsSigV4Credential(accessKeyId, SecretAccessKey, null));

        exception.ParamName.ShouldBe("accessKeyId");
    }

    [Fact]
    public void Constructor_WhenSecretAccessKeyIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AwsSigV4Credential(AccessKeyId, null!, null));

        exception.ParamName.ShouldBe("secretAccessKey");
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    public void Constructor_WhenSecretAccessKeyIsBlank_ThrowsArgumentException(string secretAccessKey)
    {
        var exception = Should.Throw<ArgumentException>(() => new AwsSigV4Credential(AccessKeyId, secretAccessKey, null));

        exception.ParamName.ShouldBe("secretAccessKey");
    }

    [Fact]
    public void Constructor_WhenSessionTokenIsNull_IsAccepted()
    {
        var credential = new AwsSigV4Credential(AccessKeyId, SecretAccessKey, null);

        credential.SessionToken.ShouldBeNull();
        credential.SecretAccessKey.ShouldBe(SecretAccessKey);
    }

    [Fact]
    public void ToString_WhenSessionTokenPresent_RedactsBothSecretsButShowsAccessKeyId()
    {
        var credential = new AwsSigV4Credential(AccessKeyId, SecretAccessKey, SessionToken);

        var text = credential.ToString();

        text.ShouldNotContain(SecretAccessKey);
        text.ShouldNotContain(SessionToken);
        text.ShouldBe(
            "AwsSigV4Credential { AccessKeyId = AKIAIOSFODNN7EXAMPLE, SecretAccessKey = [REDACTED], SessionToken = [REDACTED] }");
    }

    [Fact]
    public void ToString_WhenSessionTokenAbsent_PrintsNullForSessionToken()
    {
        var credential = new AwsSigV4Credential(AccessKeyId, SecretAccessKey, null);

        var text = credential.ToString();

        text.ShouldNotContain(SecretAccessKey);
        text.ShouldBe(
            "AwsSigV4Credential { AccessKeyId = AKIAIOSFODNN7EXAMPLE, SecretAccessKey = [REDACTED], SessionToken = null }");
    }

    [Fact]
    public void ToString_WhenInterpolated_StillRedactsSecrets()
    {
        var credential = new AwsSigV4Credential(AccessKeyId, SecretAccessKey, SessionToken);

        var text = FormattableString.Invariant($"credential={credential}");

        text.ShouldNotContain(SecretAccessKey);
        text.ShouldNotContain(SessionToken);
        text.ShouldContain(AccessKeyId);
        text.ShouldContain(AwsSigV4Credential.RedactionMarker);
    }

    [Fact]
    public void Equals_WhenSecretsDiffer_ReturnsFalse()
    {
        var first = new AwsSigV4Credential(AccessKeyId, SecretAccessKey, null);
        var second = new AwsSigV4Credential(AccessKeyId, "other-secret", null);

        first.ShouldNotBe(second);
        new AwsSigV4Credential(AccessKeyId, SecretAccessKey, null).ShouldBe(first);
    }
}
