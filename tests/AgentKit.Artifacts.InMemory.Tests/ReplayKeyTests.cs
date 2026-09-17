// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;



/// <summary>Verifies ReplayKey behavior and contracts.</summary>
public sealed class ReplayKeyTests
{
    [Theory]
    [InlineData("tenantId", typeof(ArgumentNullException))]
    [InlineData("nullOperation", typeof(ArgumentNullException))]
    [InlineData("emptyOperation", typeof(ArgumentException))]
    [InlineData("nullKey", typeof(ArgumentNullException))]
    [InlineData("emptyKey", typeof(ArgumentException))]
    public void Constructor_WhenValueIsInvalid_ThrowsExactParameter(string invalidField, Type expectedExceptionType)
    {
        Action action = invalidField switch
        {
            "tenantId" => () => _ = new ReplayKey(default, "prepare", "key"),
            "nullOperation" => () => _ = new ReplayKey(TenantId(), null!, "key"),
            "emptyOperation" => () => _ = new ReplayKey(TenantId(), " ", "key"),
            "nullKey" => () => _ = new ReplayKey(TenantId(), "prepare", null!),
            "emptyKey" => () => _ = new ReplayKey(TenantId(), "prepare", " "),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField)),
        };
        var expectedParameter = invalidField switch
        {
            "tenantId" => "tenantId",
            "nullOperation" or "emptyOperation" => "operation",
            _ => "key",
        };
        var exception = Should.Throw<ArgumentException>(action);
        exception.GetType().ShouldBe(expectedExceptionType);
        exception.ParamName.ShouldBe(expectedParameter);
    }

    [Fact]
    public void Keys_WhenValuesMatch_AreEqualOnlyWithinSameTenantOperationAndKey()
    {
        var replay = new ReplayKey(TenantId(), "prepare", "key");
        var equivalentReplay = new ReplayKey(TenantId(), "prepare", "key");
        var otherTenantReplay = new ReplayKey(new TenantId("other"), "prepare", "key");
        var otherOperationReplay = new ReplayKey(TenantId(), "finalize", "key");
        var otherKeyReplay = new ReplayKey(TenantId(), "prepare", "other-key");
        equivalentReplay.ShouldBe(replay);
        otherTenantReplay.ShouldNotBe(replay);
        otherOperationReplay.ShouldNotBe(replay);
        otherKeyReplay.ShouldNotBe(replay);
    }

    [Fact]
    public void Properties_WhenConstructed_ExposeTheExactCapturedValues()
    {
        var replay = new ReplayKey(TenantId(), "prepare", "key");

        replay.TenantId.ShouldBe(TenantId());
        replay.Operation.ShouldBe("prepare");
        replay.Key.ShouldBe("key");
    }

    private static TenantId TenantId() => new("tenant");
}
