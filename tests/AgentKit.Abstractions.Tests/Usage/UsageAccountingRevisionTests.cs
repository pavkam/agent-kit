// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Usage;
/// <summary>Verifies UsageAccountingRevision behavior and contracts.</summary>
public sealed class UsageAccountingRevisionTests: Conformance.LongIdentityConformanceTests<UsageAccountingRevision>
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenAccountingRevisionIsNotPositive_RejectsExactArgument(long value) => Should.Throw<ArgumentOutOfRangeException>(() => new UsageAccountingRevision(value)).ParamName.ShouldBe("value");

    /// <inheritdoc/>
    protected override UsageAccountingRevision Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(UsageAccountingRevision subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;

    [Fact]
    public void Serialize_WhenRevisionRoundTrips_PreservesCanonicalValueAndRejectsInvalidJson()
    {
        System.Text.Json.JsonSerializer.Deserialize<UsageAccountingRevision>(System.Text.Json.JsonSerializer.Serialize(new UsageAccountingRevision(long.MaxValue))).Value.ShouldBe(long.MaxValue);
        new UsageAccountingRevision(1).ToString().ShouldBe("1");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => System.Text.Json.JsonSerializer.Deserialize<UsageAccountingRevision>("{\"Value\":0}"));
    }

}
