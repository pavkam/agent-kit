// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Http;

using System.Collections.Frozen;
using System.Net;

using AgentKit.Providers.Http;

/// <summary>Verifies the canonical HTTP status to <see cref="ProviderFailureKind"/> table and its override hook.</summary>
public sealed class HttpStatusFailureKindMapperTests
{
    [Theory]
    [InlineData(401, ProviderFailureKind.Authentication)]
    [InlineData(403, ProviderFailureKind.Authorization)]
    [InlineData(429, ProviderFailureKind.Throttling)]
    [InlineData(408, ProviderFailureKind.Timeout)]
    [InlineData(504, ProviderFailureKind.Timeout)]
    [InlineData(413, ProviderFailureKind.InvalidRequest)]
    [InlineData(529, ProviderFailureKind.Unavailable)]
    public void Map_WhenStatusHasExplicitRow_ReturnsThatKind(int statusCode, ProviderFailureKind expected)
    {
        var kind = HttpStatusFailureKindMapper.Map((HttpStatusCode) statusCode);

        kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(402)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(424)]
    [InlineData(498)]
    [InlineData(499)]
    public void Map_WhenOtherClientError_ReturnsInvalidRequest(int statusCode)
    {
        var kind = HttpStatusFailureKindMapper.Map((HttpStatusCode) statusCode);

        kind.ShouldBe(ProviderFailureKind.InvalidRequest);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(599)]
    public void Map_WhenOtherServerError_ReturnsUnavailable(int statusCode)
    {
        var kind = HttpStatusFailureKindMapper.Map((HttpStatusCode) statusCode);

        kind.ShouldBe(ProviderFailureKind.Unavailable);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(399)]
    public void Map_WhenNonErrorNonSuccessFamily_ReturnsProtocolViolation(int statusCode)
    {
        var kind = HttpStatusFailureKindMapper.Map((HttpStatusCode) statusCode);

        kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(600)]
    [InlineData(999)]
    public void Map_WhenOutsideHttpRange_ReturnsUnknown(int statusCode)
    {
        var kind = HttpStatusFailureKindMapper.Map((HttpStatusCode) statusCode);

        kind.ShouldBe(ProviderFailureKind.Unknown);
    }

    [Fact]
    public void Map_WhenOverridesIsNull_UsesCanonicalTable()
    {
        var kind = HttpStatusFailureKindMapper.Map((HttpStatusCode) 424, overrides: null);

        kind.ShouldBe(ProviderFailureKind.InvalidRequest);
    }

    [Fact]
    public void Map_WhenOverrideMatchesStatus_ReturnsOverrideKind()
    {
        var overrides = new Dictionary<HttpStatusCode, ProviderFailureKind>
        {
            [(HttpStatusCode) 424] = ProviderFailureKind.Unavailable,
            [(HttpStatusCode) 498] = ProviderFailureKind.Authentication,
        }.ToFrozenDictionary();

        HttpStatusFailureKindMapper.Map((HttpStatusCode) 424, overrides).ShouldBe(ProviderFailureKind.Unavailable);
        HttpStatusFailureKindMapper.Map((HttpStatusCode) 498, overrides).ShouldBe(ProviderFailureKind.Authentication);
    }

    [Fact]
    public void Map_WhenOverrideDoesNotMatchStatus_FallsBackToCanonicalTable()
    {
        var overrides = new Dictionary<HttpStatusCode, ProviderFailureKind>
        {
            [(HttpStatusCode) 424] = ProviderFailureKind.Unavailable,
        }.ToFrozenDictionary();

        HttpStatusFailureKindMapper.Map(HttpStatusCode.TooManyRequests, overrides).ShouldBe(ProviderFailureKind.Throttling);
        HttpStatusFailureKindMapper.Map(HttpStatusCode.GatewayTimeout, overrides).ShouldBe(ProviderFailureKind.Timeout);
    }

    [Fact]
    public void Map_WhenOverrideTargetsCanonicalRow_OverrideWins()
    {
        var overrides = new Dictionary<HttpStatusCode, ProviderFailureKind>
        {
            [HttpStatusCode.NotImplemented] = ProviderFailureKind.InvalidRequest,
        }.ToFrozenDictionary();

        HttpStatusFailureKindMapper.Map(HttpStatusCode.NotImplemented, overrides).ShouldBe(ProviderFailureKind.InvalidRequest);
    }
}
