// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests;

/// <summary>Verifies AnthropicErrorMapping behavior and contracts.</summary>
public sealed class AnthropicErrorMappingTests
{
    [Theory]
    [InlineData("invalid_request_error", ProviderFailureKind.InvalidRequest)]
    [InlineData("authentication_error", ProviderFailureKind.Authentication)]
    [InlineData("permission_error", ProviderFailureKind.Authorization)]
    [InlineData("not_found_error", ProviderFailureKind.InvalidRequest)]
    [InlineData("request_too_large", ProviderFailureKind.InvalidRequest)]
    [InlineData("rate_limit_error", ProviderFailureKind.Throttling)]
    [InlineData("overloaded_error", ProviderFailureKind.Unavailable)]
    [InlineData("api_error", ProviderFailureKind.Unavailable)]
    [InlineData("some_future_error_type", ProviderFailureKind.Unknown)]
    [InlineData(null, ProviderFailureKind.Unknown)]
    public void MapErrorType_MapsEveryKnownAndUnknownErrorTypeToExpectedKind(string? anthropicErrorType, ProviderFailureKind expected) =>
        AnthropicErrorMapping.MapErrorType(anthropicErrorType).ShouldBe(expected);
}
