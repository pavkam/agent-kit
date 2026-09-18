// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using System.Collections.Immutable;

/// <summary>Verifies exact terminal evidence and consistency of its portable projection.</summary>
public sealed class ToolCallOutcomeTests
{
    [Theory]
    [InlineData(ToolTerminalStatus.Succeeded, ToolCallOutcomeKind.Success)]
    [InlineData(ToolTerminalStatus.UnknownTool, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.InvalidArguments, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.Unsupported, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.Denied, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.ApprovalDenied, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.ApprovalExpired, ToolCallOutcomeKind.Rejected)]
    [InlineData(ToolTerminalStatus.InvocationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.TimedOut, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.Cancelled, ToolCallOutcomeKind.Cancelled)]
    [InlineData(ToolTerminalStatus.Interrupted, ToolCallOutcomeKind.Cancelled)]
    [InlineData(ToolTerminalStatus.ResultNormalizationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.ResultSerializationFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.ProtocolFailed, ToolCallOutcomeKind.Failed)]
    [InlineData(ToolTerminalStatus.ResourceLimitExceeded, ToolCallOutcomeKind.Rejected)]
    public void Constructor_WhenKindContradictsSourceStatus_RejectsEveryIncorrectCategory(ToolTerminalStatus status, ToolCallOutcomeKind expected)
    {
        // Arrange / Act / Assert
        foreach (var kind in new[] { ToolCallOutcomeKind.Success, ToolCallOutcomeKind.Failed, ToolCallOutcomeKind.Rejected, ToolCallOutcomeKind.Cancelled })
        {
            if (kind == expected)
            {
                var outcome = new ToolCallOutcome(kind, status, SideEffectCertainty.Unknown, false, null, ExtensionData.Empty);
                outcome.SourceStatus.ShouldBe(status);
                outcome.Kind.ShouldBe(expected);
            }
            else
            {
                var error = Should.Throw<ArgumentException>(() => new ToolCallOutcome(kind, status, SideEffectCertainty.Unknown, false, null, ExtensionData.Empty));
                error.GetType().ShouldBe(typeof(ArgumentException));
                error.ParamName.ShouldBe("kind");
            }
        }
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(int.MaxValue)]
    public void Constructor_WhenSourceStatusIsUnknown_PreservesNumericEvidenceAndRequiresFailure(int rawStatus)
    {
        // Arrange
        var status = (ToolTerminalStatus) rawStatus;

        // Act
        var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Failed, status, SideEffectCertainty.PartiallyPerformed, true, null, ExtensionData.Empty);

        // Assert
        outcome.SourceStatus.ShouldBe(status);
        outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.PartiallyPerformed);
        outcome.Retryable.ShouldBeTrue();
        foreach (var kind in new[] { ToolCallOutcomeKind.Success, ToolCallOutcomeKind.Rejected, ToolCallOutcomeKind.Cancelled })
        {
            Should.Throw<ArgumentException>(() => new ToolCallOutcome(kind, status, SideEffectCertainty.Unknown, false, null, ExtensionData.Empty)).ParamName.ShouldBe("kind");
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void Constructor_WhenKindIsUndefined_RejectsExactParameter(int rawKind)
    {
        var error = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallOutcome((ToolCallOutcomeKind) rawKind, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, false, null, ExtensionData.Empty));
        error.ParamName.ShouldBe("kind");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(int.MaxValue)]
    public void Constructor_WhenCertaintyIsUndefinedOrInapplicable_RejectsExactParameter(int rawCertainty)
    {
        var error = Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, (SideEffectCertainty) rawCertainty, false, null, ExtensionData.Empty));
        error.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Theory]
    [InlineData(SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(SideEffectCertainty.Unknown)]
    [InlineData(SideEffectCertainty.DefinitelyPerformed)]
    [InlineData(SideEffectCertainty.PartiallyPerformed)]
    public void Constructor_WhenCancellationHasEffectEvidence_PreservesCertaintyAndRetryAdvice(SideEffectCertainty certainty)
    {
        foreach (var retryable in new[] { false, true })
        {
            var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Cancelled, ToolTerminalStatus.Cancelled, certainty, retryable, "Stopped", ExtensionData.Empty);
            outcome.SideEffectCertainty.ShouldBe(certainty);
            outcome.Retryable.ShouldBe(retryable);
            outcome.FailureReason.ShouldBe("Stopped");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("failure")]
    public void Constructor_WhenSuccessHasFailureText_RejectsExactParameter(string reason) =>
        Should.Throw<ArgumentException>(() => new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, reason, ExtensionData.Empty)).ParamName.ShouldBe("failureReason");

    [Fact]
    public void Constructor_WhenExtensionsAreNullOrMalformed_RejectsExactParameter()
    {
        var badBuffer = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("test", default));
        foreach (var extensions in new ExtensionData[] { null! })
        {
            Should.Throw<ArgumentNullException>(() => Create(extensions: extensions)).ParamName.ShouldBe("extensions");
        }
        Should.Throw<ArgumentException>(() => Create(extensions: badBuffer)).ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Equals_WhenEvidenceIsReconstructed_UsesEveryRetainedField()
    {
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("test", new ExtensionValue([1, 2])));
        var value = Create(extensions: extensions);
        var equal = Create(extensions: new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("test", new ExtensionValue([1, 2]))));
        value.ShouldBe(equal);
        value.GetHashCode().ShouldBe(equal.GetHashCode());
        value.Extensions.ShouldBe(extensions);
        (value with { }).ShouldBe(value);
        value.Equals(null).ShouldBeFalse();
        ToolCallOutcome[] different =
        [
            Create(status: ToolTerminalStatus.TimedOut, extensions: extensions),
            Create(certainty: SideEffectCertainty.PartiallyPerformed, extensions: extensions),
            Create(retryable: true, extensions: extensions),
            Create(reason: "different", extensions: extensions),
            Create(extensions: ExtensionData.Empty),
        ];
        foreach (var other in different)
        {
            value.ShouldNotBe(other);
        }
    }

    private static ToolCallOutcome Create(ToolTerminalStatus status = ToolTerminalStatus.InvocationFailed,
        SideEffectCertainty certainty = SideEffectCertainty.Unknown, bool retryable = false,
        string? reason = "failed", ExtensionData? extensions = null) =>
        new(ToolCallOutcomeKind.Failed, status, certainty, retryable, reason, extensions!);
}
