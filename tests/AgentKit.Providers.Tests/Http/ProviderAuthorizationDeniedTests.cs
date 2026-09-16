// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using AgentKit.Providers.Http;

/// <summary>Verifies ProviderAuthorizationDenied behavior and contracts.</summary>
public sealed class ProviderAuthorizationDeniedTests
{
    private static ProviderFailure CreateFailure(string safeMessage = "Denied.") =>
        new(
            ProviderFailureKind.Authentication,
            new ProviderId("test-provider"),
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause: null,
            ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderAuthorizationDenied(null!));

        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void Constructor_WhenFailureIsValid_ExposesFailure()
    {
        var failure = CreateFailure();

        var denied = new ProviderAuthorizationDenied(failure);

        denied.Failure.ShouldBeSameAs(failure);
    }

    [Fact]
    public void WithExpression_WhenReplacingFailure_ProducesIndependentCopy()
    {
        var original = new ProviderAuthorizationDenied(CreateFailure("first"));
        var replacementFailure = CreateFailure("second");

        var copy = original with { Failure = replacementFailure };

        copy.Failure.ShouldBeSameAs(replacementFailure);
        original.Failure.SafeMessage.ShouldBe("first");
        copy.ShouldNotBe(original);
    }
}
