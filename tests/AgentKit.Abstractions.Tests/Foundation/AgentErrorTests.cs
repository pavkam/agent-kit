// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

using AgentKit;

/// <summary>Verifies AgentError behavior and contracts.</summary>
public sealed class AgentErrorTests
{
    public static TheoryData<SideEffectCertainty> DefinedCertainties => [.. Enum.GetValues<SideEffectCertainty>()];

    [Fact]
    public void AgentError_Constructor_WhenCodeDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentError(default, "Safe failure", false, SideEffectCertainty.NotApplicable, new ErrorOrigin("agentkit.tests"), null, null, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("code");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AgentError_Constructor_WhenSafeMessageInvalid_ThrowsExactArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => Create(safeMessage: safeMessage!));
        exception.GetType().ShouldBe(safeMessage is null ? typeof(ArgumentNullException) : typeof(ArgumentException));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void AgentError_Constructor_WhenCertaintyUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(sideEffectCertainty: (SideEffectCertainty) 99));
        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Theory]
    [MemberData(nameof(DefinedCertainties))]
    public void AgentError_Constructor_WhenCertaintyDefined_PreservesIt(SideEffectCertainty certainty) => Create(sideEffectCertainty: certainty).SideEffectCertainty.ShouldBe(certainty);
    [Fact]
    public void AgentError_Constructor_WhenOriginDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentError(AgentErrorCodes.Unknown, "Safe failure", false, SideEffectCertainty.NotApplicable, default, null, null, null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("origin");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AgentError_Constructor_WhenExternalCodeBlank_ThrowsArgumentException(string externalCode)
    {
        var exception = Should.Throw<ArgumentException>(() => Create(externalCode: externalCode));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("externalCode");
    }

    [Fact]
    public void AgentError_Constructor_WhenOperationIdPresentAndDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(operationId: default(OperationId)));
        exception.ParamName.ShouldBe("operationId");
    }

    [Fact]
    public void AgentError_Constructor_WhenExternalRequestIdPresentAndDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(externalRequestId: default(ExternalRequestId)));
        exception.ParamName.ShouldBe("externalRequestId");
    }

    [Fact]
    public void AgentError_Constructor_WhenRetryAfterNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Create(retryAfter: TimeSpan.FromTicks(-1)));
        exception.ParamName.ShouldBe("retryAfter");
    }

    [Fact]
    public void AgentError_Constructor_WhenDiagnosticsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AgentError(AgentErrorCodes.Unknown, "Safe failure", false, SideEffectCertainty.NotApplicable, new ErrorOrigin("agentkit.tests"), null, null, null, null, null!));
        exception.ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void AgentError_Constructor_WhenOptionalValuesAbsentAndRetryDelayZero_PreservesBoundaries()
    {
        var error = Create(externalCode: null, operationId: null, externalRequestId: null, retryAfter: TimeSpan.Zero);
        error.ExternalCode.ShouldBeNull();
        error.OperationId.ShouldBeNull();
        error.ExternalRequestId.ShouldBeNull();
        error.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void AgentError_Constructor_WhenUnknownValuesSupplied_PreservesExactEvidenceEqualityAndCopy()
    {
        var operationId = new OperationId(Guid.Parse("91637253-c285-49fa-a07b-7d1dc7ed5349"));
        var externalRequestId = new ExternalRequestId("Req-A");
        var diagnostics = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("safe", new ExtensionValue([.. "\"evidence\""u8.ToArray()])));
        var error = Create(code: new AgentErrorCode("VendorFutureFailure"), origin: new ErrorOrigin("custom.mapper/V2"), safeMessage: "Safe failure", isRetryable: true, sideEffectCertainty: SideEffectCertainty.PartiallyPerformed, externalCode: "E-Future", operationId: operationId, externalRequestId: externalRequestId, retryAfter: TimeSpan.MaxValue, diagnostics: diagnostics);
        var same = Create(code: new AgentErrorCode("VendorFutureFailure"), origin: new ErrorOrigin("custom.mapper/V2"), safeMessage: "Safe failure", isRetryable: true, sideEffectCertainty: SideEffectCertainty.PartiallyPerformed, externalCode: "E-Future", operationId: operationId, externalRequestId: externalRequestId, retryAfter: TimeSpan.MaxValue, diagnostics: diagnostics);
        var copy = error with
        {
        };
        error.Code.Value.ShouldBe("VendorFutureFailure");
        error.Origin.Value.ShouldBe("custom.mapper/V2");
        error.SafeMessage.ShouldBe("Safe failure");
        error.IsRetryable.ShouldBeTrue();
        error.SideEffectCertainty.ShouldBe(SideEffectCertainty.PartiallyPerformed);
        error.ExternalCode.ShouldBe("E-Future");
        error.OperationId.ShouldBe(operationId);
        error.ExternalRequestId.ShouldBe(externalRequestId);
        error.RetryAfter.ShouldBe(TimeSpan.MaxValue);
        error.Diagnostics.ShouldBe(diagnostics);
        error.ShouldBe(same);
        error.GetHashCode().ShouldBe(same.GetHashCode());
        copy.ShouldBe(error);
        copy.ShouldNotBeSameAs(error);
    }

    private static AgentError Create(AgentErrorCode? code = null, string safeMessage = "Safe failure", bool isRetryable = false, SideEffectCertainty sideEffectCertainty = SideEffectCertainty.NotApplicable, ErrorOrigin? origin = null, string? externalCode = "E1", OperationId? operationId = null, ExternalRequestId? externalRequestId = null, TimeSpan? retryAfter = null, ExtensionData? diagnostics = null) => new(code ?? AgentErrorCodes.Unknown, safeMessage, isRetryable, sideEffectCertainty, origin ?? new ErrorOrigin("agentkit.tests"), externalCode, operationId, externalRequestId, retryAfter, diagnostics ?? ExtensionData.Empty);
}
