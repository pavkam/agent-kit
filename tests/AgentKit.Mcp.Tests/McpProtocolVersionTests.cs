// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

public sealed class McpProtocolVersionTests
{
    [Theory]
    [InlineData(2025, 11, 25, McpProtocolEra.Legacy)]
    [InlineData(2026, 7, 28, McpProtocolEra.Modern)]
    [InlineData(2027, 1, 1, McpProtocolEra.Modern)]
    public void Era_WhenVersionProvided_DistinguishesWireLifecycle(
        int year,
        int month,
        int day,
        McpProtocolEra expected)
    {
        var version = new McpProtocolVersion(new DateOnly(year, month, day));

        var result = version.Era;

        result.ShouldBe(expected);
        version.ToString().ShouldBe($"{year:D4}-{month:D2}-{day:D2}");
    }

    [Fact]
    public void Constructor_WhenDateIsDefault_ThrowsBeforeConstruction()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpProtocolVersion(default));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Parse_WhenTextIsMissing_ThrowsForValue(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => McpProtocolVersion.Parse(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("2026/07/28")]
    [InlineData("2026-7-28")]
    [InlineData("26-07-28")]
    [InlineData("2026-07-28T00:00:00Z")]
    [InlineData(" 2026-07-28")]
    [InlineData("2026-07-28 ")]
    [InlineData("2026-02-29")]
    [InlineData("not-a-version")]
    public void Parse_WhenTextIsNotCanonical_ThrowsFormatException(string value) =>
        _ = Should.Throw<FormatException>(() => McpProtocolVersion.Parse(value));

    [Theory]
    [InlineData("2024-11-05", McpProtocolEra.Legacy)]
    [InlineData("2025-03-26", McpProtocolEra.Legacy)]
    [InlineData("2025-06-18", McpProtocolEra.Legacy)]
    [InlineData("2025-11-25", McpProtocolEra.Legacy)]
    [InlineData("2026-07-28", McpProtocolEra.Modern)]
    public void Parse_WhenRevisionIsSupported_RoundTripsCanonicalText(string value, McpProtocolEra era)
    {
        var version = McpProtocolVersion.Parse(value);

        version.ToString().ShouldBe(value);
        version.Era.ShouldBe(era);
    }

    [Fact]
    public void SupportedBySdk_WhenRead_ContainsOrderedUniqueKnownRevisions()
    {
        var versions = McpProtocolVersions.SupportedBySdk;

        versions.ShouldBe([
            McpProtocolVersions.November2024,
            McpProtocolVersions.March2025,
            McpProtocolVersions.June2025,
            McpProtocolVersions.November2025,
            McpProtocolVersions.July2026
        ]);
        versions.Distinct().Count().ShouldBe(versions.Length);
    }

    [Fact]
    public void CompareTo_WhenRevisionsSpanEraBoundary_OrdersByRevisionDate()
    {
        McpProtocolVersions.November2025.CompareTo(McpProtocolVersions.July2026).ShouldBeLessThan(0);
        McpProtocolVersions.July2026.CompareTo(McpProtocolVersions.November2025).ShouldBeGreaterThan(0);
        McpProtocolVersions.July2026.CompareTo(McpProtocolVersions.July2026).ShouldBe(0);
        (McpProtocolVersions.November2025 < McpProtocolVersions.July2026).ShouldBeTrue();
        (McpProtocolVersions.November2025 <= McpProtocolVersions.November2025).ShouldBeTrue();
        (McpProtocolVersions.July2026 > McpProtocolVersions.November2025).ShouldBeTrue();
        (McpProtocolVersions.July2026 >= McpProtocolVersions.July2026).ShouldBeTrue();
    }
}
