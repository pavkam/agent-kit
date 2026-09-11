// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SecurityAuthorizationCaptureUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SecurityAuthorizationCaptureUnavailable>
{
    [Fact]
    public void Constructor_WhenSafeReasonIsInvalid_ThrowsWithExactParameterName()
    {
        var nullReason = Should.Throw<ArgumentNullException>(() => new SecurityAuthorizationCaptureUnavailable(null!));
        nullReason.GetType().ShouldBe(typeof(ArgumentNullException));
        nullReason.ParamName.ShouldBe("safeReason");
        var blankReason = Should.Throw<ArgumentException>(() => new SecurityAuthorizationCaptureUnavailable(" "));
        blankReason.GetType().ShouldBe(typeof(ArgumentException));
        blankReason.ParamName.ShouldBe("safeReason");
    }

    /// <inheritdoc/>
    protected override SecurityAuthorizationCaptureUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SecurityAuthorizationCaptureUnavailable subject) => subject.SafeReason;
}
