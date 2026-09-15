// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests;

/// <summary>Verifies <see cref="GoogleGeminiErrorMapping"/> behavior and contracts.</summary>
public sealed class GoogleGeminiErrorMappingTests
{
    [Theory]
    [InlineData("INVALID_ARGUMENT", ProviderFailureKind.InvalidRequest)]
    [InlineData("FAILED_PRECONDITION", ProviderFailureKind.InvalidRequest)]
    [InlineData("OUT_OF_RANGE", ProviderFailureKind.InvalidRequest)]
    [InlineData("NOT_FOUND", ProviderFailureKind.InvalidRequest)]
    [InlineData("UNAUTHENTICATED", ProviderFailureKind.Authentication)]
    [InlineData("PERMISSION_DENIED", ProviderFailureKind.Authorization)]
    [InlineData("RESOURCE_EXHAUSTED", ProviderFailureKind.Throttling)]
    [InlineData("DEADLINE_EXCEEDED", ProviderFailureKind.Timeout)]
    [InlineData("UNAVAILABLE", ProviderFailureKind.Unavailable)]
    [InlineData("INTERNAL", ProviderFailureKind.Unavailable)]
    [InlineData("ABORTED", ProviderFailureKind.Unavailable)]
    [InlineData("CANCELLED", ProviderFailureKind.Cancellation)]
    public void MapStatus_WhenCanonicalStatusIsKnown_MapsToNormalizedKind(string status, ProviderFailureKind expected) =>
        GoogleGeminiErrorMapping.MapStatus(status).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FUTURE_STATUS")]
    [InlineData("DATA_LOSS")]
    [InlineData("UNIMPLEMENTED")]
    public void MapStatus_WhenStatusIsMissingOrUnknown_ReturnsUnknown(string? status) =>
        GoogleGeminiErrorMapping.MapStatus(status).ShouldBe(ProviderFailureKind.Unknown);

    /// <summary>Verifies the mapping is exact and case-sensitive, matching Google's canonical upper-case vocabulary.</summary>
    [Fact]
    public void MapStatus_WhenStatusDiffersOnlyByCase_ReturnsUnknown() =>
        GoogleGeminiErrorMapping.MapStatus("unauthenticated").ShouldBe(ProviderFailureKind.Unknown);
}
