// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class SecurityProfilePublicationUnavailableTests: Conformance.SingleMessageLeafConformanceTests<SecurityProfilePublicationUnavailable>
{
    [Fact]
    public void Constructor_WhenSafeReasonIsInvalid_ThrowsWithExactParameterName()
    {
        var nullReason = Should.Throw<ArgumentNullException>(() => new SecurityProfilePublicationUnavailable(null!));
        nullReason.GetType().ShouldBe(typeof(ArgumentNullException));
        nullReason.ParamName.ShouldBe("safeReason");
        var blankReason = Should.Throw<ArgumentException>(() => new SecurityProfilePublicationUnavailable(" "));
        blankReason.GetType().ShouldBe(typeof(ArgumentException));
        blankReason.ParamName.ShouldBe("safeReason");
    }

    /// <inheritdoc/>
    protected override SecurityProfilePublicationUnavailable Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(SecurityProfilePublicationUnavailable subject) => subject.SafeReason;
}
