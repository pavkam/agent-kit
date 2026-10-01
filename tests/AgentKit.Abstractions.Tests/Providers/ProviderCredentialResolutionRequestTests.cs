// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;
using AgentKit.TestSupport;

/// <summary>Verifies the validation and secrecy of <see cref="ProviderCredentialResolutionRequest"/>.</summary>
public sealed class ProviderCredentialResolutionRequestTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProviderCredentialResolutionRequest Valid() =>
        ProviderCredentialProbe.CreateRequest(new StaticProviderCredentialSource(new ApiKeyProviderCredential("s")), Now);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesEveryValue()
    {
        var template = Valid();

        var request = new ProviderCredentialResolutionRequest(
            template.Endpoint, template.Credential, template.Operation, 2, Now, template.CredentialGrant);

        request.Endpoint.ShouldBeSameAs(template.Endpoint);
        request.Credential.ShouldBeSameAs(template.Credential);
        request.Operation.ShouldBeSameAs(template.Operation);
        request.Attempt.ShouldBe(2);
        request.Deadline.ShouldBe(Now);
        request.CredentialGrant.ShouldBeSameAs(template.CredentialGrant);
    }

    [Fact]
    public void Constructor_WhenReferenceArgumentIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var t = Valid();

        Should.Throw<ArgumentNullException>(() => new ProviderCredentialResolutionRequest(null!, t.Credential, t.Operation, 1, Now, t.CredentialGrant)).ParamName.ShouldBe("endpoint");
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialResolutionRequest(t.Endpoint, null!, t.Operation, 1, Now, t.CredentialGrant)).ParamName.ShouldBe("credential");
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialResolutionRequest(t.Endpoint, t.Credential, null!, 1, Now, t.CredentialGrant)).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialResolutionRequest(t.Endpoint, t.Credential, t.Operation, 1, Now, null!)).ParamName.ShouldBe("credentialGrant");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenAttemptIsBelowOne_ThrowsArgumentOutOfRangeException(int attempt)
    {
        var t = Valid();

        Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialResolutionRequest(t.Endpoint, t.Credential, t.Operation, attempt, Now, t.CredentialGrant))
            .ParamName.ShouldBe("attempt");
    }

    [Fact]
    public void Constructor_WhenAttemptIsOne_DoesNotThrow() =>
        _ = new ProviderCredentialResolutionRequest(Valid().Endpoint, Valid().Credential, Valid().Operation, 1, Now, Valid().CredentialGrant);
}
