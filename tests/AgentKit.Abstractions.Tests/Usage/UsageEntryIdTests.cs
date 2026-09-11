// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;
/// <summary>Verifies UsageEntryId behavior and contracts.</summary>
public sealed class UsageEntryIdTests: Conformance.GuidIdentityConformanceTests<UsageEntryId>
{
    [Fact]
    public void Constructor_WhenUsageIdentityIsEmpty_RejectsExactArgument() => Should.Throw<ArgumentOutOfRangeException>(() => new UsageEntryId(Guid.Empty)).ParamName.ShouldBe("value");

    /// <inheritdoc/>
    protected override UsageEntryId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(UsageEntryId subject) => subject.Value;

    [Fact]
    public void Serialize_WhenIdentityRoundTrips_PreservesCanonicalValueAndRejectsInvalidJson()
    {
        var id = new UsageEntryId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        System.Text.Json.JsonSerializer.Deserialize<UsageEntryId>(System.Text.Json.JsonSerializer.Serialize(id)).ShouldBe(id);
        id.ToString().ShouldBe("00000000-0000-0000-0000-000000000001");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => System.Text.Json.JsonSerializer.Deserialize<UsageEntryId>("{\"Value\":\"00000000-0000-0000-0000-000000000000\"}"));
    }

}
