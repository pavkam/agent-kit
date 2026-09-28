// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests;

using System.Net;

using AgentKit.Providers.OpenAICompatible;

/// <summary>Verifies OpenAI-compatible error metadata maps to <see cref="ProviderFailureKind"/>.</summary>
public sealed class OpenAIProviderFailureKindMapperTests
{
    [Theory]
    [InlineData("invalid_request_error", "context_length_exceeded", ProviderFailureKind.ContextLengthExceeded)]
    [InlineData(null, "context_length_exceeded", ProviderFailureKind.ContextLengthExceeded)]
    [InlineData("context_length_exceeded", null, ProviderFailureKind.ContextLengthExceeded)]
    [InlineData("invalid_request_error", "string_above_max_length", ProviderFailureKind.ContextLengthExceeded)]
    public void MapFromErrorBody_WhenContextLimitCode_ReturnsContextLengthExceeded(
        string? errorType,
        string? errorCode,
        ProviderFailureKind expected) =>
        OpenAIProviderFailureKindMapper.MapFromErrorBody(errorType, errorCode).ShouldBe(expected);

    [Fact]
    public void Map_WhenHttp413_ReturnsContextLengthExceeded() =>
        OpenAIProviderFailureKindMapper.Map(HttpStatusCode.RequestEntityTooLarge, providerCode: null)
            .ShouldBe(ProviderFailureKind.ContextLengthExceeded);

    [Fact]
    public void Map_WhenProviderCodeIsContextLengthExceeded_ReturnsContextLengthExceededOnAnyStatus() =>
        OpenAIProviderFailureKindMapper.Map(HttpStatusCode.BadRequest, "context_length_exceeded")
            .ShouldBe(ProviderFailureKind.ContextLengthExceeded);
}
