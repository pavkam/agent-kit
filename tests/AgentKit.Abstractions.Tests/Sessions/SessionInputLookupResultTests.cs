// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionInputLookupResult behavior and contracts.</summary>
public sealed class SessionInputLookupResultTests
{
    [Fact]
    public void SessionInputLookupConflict_WhenInputIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionInputLookupConflict(default, "conflict"));
        exception.ParamName.ShouldBe("inputId");
    }

    [Fact]
    public void SessionInputLookupConflict_WhenSafeReasonIsBlank_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionInputLookupConflict(new InputId(Guid.NewGuid()), " "));
        exception.ParamName.ShouldBe("safeReason");
    }

    [Fact]
    public void SessionInputLookupConflict_With_WhenApplied_ProducesEqualCopy()
    {
        var inputId = new InputId(Guid.NewGuid());
        var original = new SessionInputLookupConflict(inputId, "conflict");
        var copy = original with { };
        copy.ShouldBe(original);
        original.InputId.ShouldBe(inputId);
        original.SafeReason.ShouldBe("conflict");
    }

    [Fact]
    public void SessionInputNotFound_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionInputNotFound();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void SessionInputReplayFound_WhenAdmittedInputIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionInputReplayFound(null!, SessionsTestData.BeforeRun(), SessionsTestData.Receipt()));
        exception.ParamName.ShouldBe("admittedInput");
    }

    [Fact]
    public void SessionInputReplayFound_WhenCorrelationIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionInputReplayFound(SessionsTestData.AdmittedInput(), null!, SessionsTestData.Receipt()));
        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void SessionInputReplayFound_WhenReceiptIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionInputReplayFound(SessionsTestData.AdmittedInput(), SessionsTestData.BeforeRun(), null!));
        exception.ParamName.ShouldBe("receipt");
    }

    [Fact]
    public void SessionInputReplayFound_With_WhenApplied_ProducesEqualCopy()
    {
        var admittedInput = SessionsTestData.AdmittedInput();
        var correlation = SessionsTestData.BeforeRun();
        var receipt = SessionsTestData.Receipt();
        var original = new SessionInputReplayFound(admittedInput, correlation, receipt);
        var copy = original with { };
        copy.ShouldBe(original);
        original.AdmittedInput.ShouldBe(admittedInput);
        original.Correlation.ShouldBe(correlation);
        original.Receipt.ShouldBe(receipt);
    }
}
