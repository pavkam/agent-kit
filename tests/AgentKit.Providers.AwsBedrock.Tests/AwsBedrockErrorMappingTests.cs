// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests;

using System.Net;

/// <summary>Verifies AwsBedrockErrorMapping behavior and contracts.</summary>
public sealed class AwsBedrockErrorMappingTests
{
    [Theory]
    [InlineData("ValidationException", ProviderFailureKind.InvalidRequest)]
    [InlineData("AccessDeniedException", ProviderFailureKind.Authorization)]
    [InlineData("ExpiredTokenException", ProviderFailureKind.Authentication)]
    [InlineData("UnrecognizedClientException", ProviderFailureKind.Authentication)]
    [InlineData("IncompleteSignature", ProviderFailureKind.Authentication)]
    [InlineData("NotAuthorized", ProviderFailureKind.Authentication)]
    [InlineData("ResourceNotFoundException", ProviderFailureKind.InvalidRequest)]
    [InlineData("ModelTimeoutException", ProviderFailureKind.Timeout)]
    [InlineData("RequestTimeoutException", ProviderFailureKind.Timeout)]
    [InlineData("ModelErrorException", ProviderFailureKind.Unavailable)]
    [InlineData("ModelStreamErrorException", ProviderFailureKind.Unavailable)]
    [InlineData("ModelNotReadyException", ProviderFailureKind.Throttling)]
    [InlineData("ThrottlingException", ProviderFailureKind.Throttling)]
    [InlineData("ServiceQuotaExceededException", ProviderFailureKind.InvalidRequest)]
    [InlineData("RequestEntityTooLargeException", ProviderFailureKind.InvalidRequest)]
    [InlineData("InternalServerException", ProviderFailureKind.Unavailable)]
    [InlineData("InternalFailure", ProviderFailureKind.Unavailable)]
    [InlineData("ServiceUnavailableException", ProviderFailureKind.Unavailable)]
    [InlineData("ServiceUnavailable", ProviderFailureKind.Unavailable)]
    public void MapExceptionName_WhenNameIsRecognized_ReturnsMappedKind(string exceptionName, ProviderFailureKind expected) =>
        AwsBedrockErrorMapping.MapExceptionName(exceptionName).ShouldBe(expected);

    /// <summary>Verifies the streaming dialect's camelCase-first-letter exception-name vocabulary maps identically via case-insensitive comparison.</summary>
    [Theory]
    [InlineData("throttlingException", ProviderFailureKind.Throttling)]
    [InlineData("accessDeniedException", ProviderFailureKind.Authorization)]
    [InlineData("modelErrorException", ProviderFailureKind.Unavailable)]
    public void MapExceptionName_WhenNameIsStreamingDialectCase_ReturnsMappedKind(string exceptionName, ProviderFailureKind expected) =>
        AwsBedrockErrorMapping.MapExceptionName(exceptionName).ShouldBe(expected);

    /// <summary>Verifies a trailing ":&lt;documentation-url&gt;" suffix, per the restJson1 protocol convention, is stripped before matching.</summary>
    [Fact]
    public void MapExceptionName_WhenNameHasDocumentationUrlSuffix_StripsSuffixBeforeMatching() =>
        AwsBedrockErrorMapping.MapExceptionName("ThrottlingException:https://docs.aws.amazon.com/errors#throttling")
            .ShouldBe(ProviderFailureKind.Throttling);

    [Fact]
    public void MapExceptionName_WhenNameIsUnrecognized_ReturnsUnknown() =>
        AwsBedrockErrorMapping.MapExceptionName("SomeFutureException").ShouldBe(ProviderFailureKind.Unknown);

    [Fact]
    public void MapExceptionName_WhenNameIsNull_ReturnsUnknown() =>
        AwsBedrockErrorMapping.MapExceptionName(null).ShouldBe(ProviderFailureKind.Unknown);

    [Fact]
    public void MapExceptionName_WhenNameIsEmpty_ReturnsUnknown() =>
        AwsBedrockErrorMapping.MapExceptionName(string.Empty).ShouldBe(ProviderFailureKind.Unknown);

    /// <summary>Verifies Bedrock's 424 Failed Dependency override sits on top of the shared HTTP status table.</summary>
    [Fact]
    public void MapStatusCode_WhenStatusIsFailedDependency_ReturnsUnavailable() =>
        AwsBedrockErrorMapping.MapStatusCode(HttpStatusCode.FailedDependency).ShouldBe(ProviderFailureKind.Unavailable);

    /// <summary>Verifies a status code with no Bedrock-specific override falls back to the shared canonical mapping.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProviderFailureKind.Authentication)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderFailureKind.Throttling)]
    [InlineData(HttpStatusCode.InternalServerError, ProviderFailureKind.Unavailable)]
    public void MapStatusCode_WhenNoBedrockOverrideExists_UsesSharedCanonicalMapping(HttpStatusCode statusCode, ProviderFailureKind expected) =>
        AwsBedrockErrorMapping.MapStatusCode(statusCode).ShouldBe(expected);
}
