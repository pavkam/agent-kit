// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies InputAdmittedSessionEntry behavior and contracts.</summary>
public sealed class InputAdmittedSessionEntryTests
{
    [Fact]
    public void Constructor_WhenInputIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Entry(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("input");
    }

    [Fact]
    public void Constructor_WhenInputAgentIdDiffers_ThrowsExactArgumentException()
    {
        var input = new AdmittedInput(SessionsTestData.AdmissionId, new AgentId(Guid.NewGuid()), SessionsTestData.SessionId,
            SessionsTestData.LaneId, SessionsTestData.Identity(), new SessionSequence(1), SessionsTestData.Input(),
            SessionsTestData.Input(), SessionsTestData.Preprocessing(), DateTimeOffset.UnixEpoch);
        var exception = Should.Throw<ArgumentException>(() => Entry(input));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("input");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var input = SessionsTestData.AdmittedInput();
        var entry = Entry(input);
        entry.Input.ShouldBe(input);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Entry(SessionsTestData.AdmittedInput());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static InputAdmittedSessionEntry Entry(AdmittedInput input) =>
        new(SessionsTestData.EntryId, SessionsTestData.Address(), SessionsTestData.BeforeRun(), SessionsTestData.BranchId,
            new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), input);
}
