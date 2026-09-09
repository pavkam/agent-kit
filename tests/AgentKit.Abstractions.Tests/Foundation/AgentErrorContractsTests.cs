// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

using System.Reflection;

using AgentKit;

public sealed class AgentErrorContractsTests
{
    public static TheoryData<Type> TextIdentityTypes =>
    [
        typeof(AgentErrorCode),
        typeof(ErrorOrigin),
    ];

    public static TheoryData<SideEffectCertainty> DefinedCertainties =>
        [.. Enum.GetValues<SideEffectCertainty>()];

    [Theory]
    [MemberData(nameof(TextIdentityTypes))]
    public void Constructor_WhenIdentityTextNull_ThrowsArgumentNullException(Type identityType)
    {
        var exception = Should.Throw<TargetInvocationException>(
            () => Activator.CreateInstance(identityType, [null]));

        var argumentException = exception.InnerException.ShouldBeOfType<ArgumentNullException>();
        argumentException.ParamName.ShouldBe("value");
    }

    [Theory]
    [MemberData(nameof(TextIdentityTypes))]
    public void Constructor_WhenIdentityTextBlank_ThrowsArgumentException(Type identityType)
    {
        foreach (var text in new[] { string.Empty, " ", "\t" })
        {
            var exception = Should.Throw<TargetInvocationException>(
                () => Activator.CreateInstance(identityType, text));

            var argumentException = exception.InnerException.ShouldBeOfType<ArgumentException>();
            argumentException.GetType().ShouldBe(typeof(ArgumentException));
            argumentException.ParamName.ShouldBe("value");
        }
    }

    [Theory]
    [MemberData(nameof(TextIdentityTypes))]
    public void Constructor_WhenIdentityTextValid_PreservesOrdinalValueEqualityAndDefaultFormatting(Type identityType)
    {
        var value = Activator.CreateInstance(identityType, "Provider-A")!;
        var same = Activator.CreateInstance(identityType, "Provider-A")!;
        var differentCase = Activator.CreateInstance(identityType, "provider-a")!;
        var defaultValue = Activator.CreateInstance(identityType)!;

        identityType.GetProperty("Value")!.GetValue(value).ShouldBe("Provider-A");
        value.ShouldBe(same);
        value.GetHashCode().ShouldBe(same.GetHashCode());
        value.ShouldNotBe(differentCase);
        value.ToString().ShouldBe("Provider-A");
        defaultValue.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void AgentErrorCodes_WhenEnumerated_PublishEveryUniqueDocumentedMachineValue()
    {
        string[] expected =
        [
            "Unknown",
            "InvalidInput", "Conflict", "InvalidState", "SessionBusy", "CorruptState",
            "InvalidConfiguration", "UntrustedConfiguration", "MissingDependency",
            "UnsupportedCapability", "IncompatibleModel", "IncompatibleSchema",
            "AuthenticationFailed", "CredentialUnavailable",
            "AuthorizationDenied", "ApprovalRequired", "ApprovalExpired",
            "InvalidProviderRequest", "RateLimited", "ProviderUnavailable", "ContentFiltered",
            "Timeout", "ConnectionFailed", "ProtocolViolation", "TruncatedStream",
            "UnknownTool", "InvalidToolArguments", "ToolFailed", "ToolInterrupted", "ToolOutcomeUnknown",
            "OutputValidationFailed", "StructuredOutputMissing",
            "RequestLimit", "TokenLimit", "CostLimit", "ToolLimit", "ContextLimit", "QueueCapacity",
            "StoreUnavailable", "ConcurrencyConflict", "MigrationRequired",
            "RecoveryIncompatible", "LeaseLost", "ReconciliationRequired",
            "Cancelled", "ObserverFailed", "ExtensionFailed", "InternalFailure",
        ];
        var properties = typeof(AgentErrorCodes).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var actual = properties.Select(property => ((AgentErrorCode) property.GetValue(null)!).Value!).ToArray();

        actual.ShouldBe(expected, ignoreOrder: true);
        actual.Distinct(StringComparer.Ordinal).Count().ShouldBe(actual.Length);
        properties.ShouldAllBe(property => property.SetMethod == null);
    }

    [Fact]
    public void AgentError_Constructor_WhenCodeDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new AgentError(
                default,
                "Safe failure",
                false,
                SideEffectCertainty.NotApplicable,
                new ErrorOrigin("agentkit.tests"),
                null,
                null,
                null,
                null,
                ExtensionData.Empty));

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
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => Create(sideEffectCertainty: (SideEffectCertainty) 99));

        exception.ParamName.ShouldBe("sideEffectCertainty");
    }

    [Theory]
    [MemberData(nameof(DefinedCertainties))]
    public void AgentError_Constructor_WhenCertaintyDefined_PreservesIt(SideEffectCertainty certainty) =>
        Create(sideEffectCertainty: certainty).SideEffectCertainty.ShouldBe(certainty);

    [Fact]
    public void AgentError_Constructor_WhenOriginDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new AgentError(
                AgentErrorCodes.Unknown,
                "Safe failure",
                false,
                SideEffectCertainty.NotApplicable,
                default,
                null,
                null,
                null,
                null,
                ExtensionData.Empty));

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
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => Create(externalRequestId: default(ExternalRequestId)));

        exception.ParamName.ShouldBe("externalRequestId");
    }

    [Fact]
    public void AgentError_Constructor_WhenRetryAfterNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => Create(retryAfter: TimeSpan.FromTicks(-1)));

        exception.ParamName.ShouldBe("retryAfter");
    }

    [Fact]
    public void AgentError_Constructor_WhenDiagnosticsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new AgentError(
                AgentErrorCodes.Unknown,
                "Safe failure",
                false,
                SideEffectCertainty.NotApplicable,
                new ErrorOrigin("agentkit.tests"),
                null,
                null,
                null,
                null,
                null!));

        exception.ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void AgentError_Constructor_WhenOptionalValuesAbsentAndRetryDelayZero_PreservesBoundaries()
    {
        var error = Create(
            externalCode: null,
            operationId: null,
            externalRequestId: null,
            retryAfter: TimeSpan.Zero);

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
        var diagnostics = new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                "safe",
                new ExtensionValue([.. "\"evidence\""u8.ToArray()])));
        var error = Create(
            code: new AgentErrorCode("VendorFutureFailure"),
            origin: new ErrorOrigin("custom.mapper/V2"),
            safeMessage: "Safe failure",
            isRetryable: true,
            sideEffectCertainty: SideEffectCertainty.PartiallyPerformed,
            externalCode: "E-Future",
            operationId: operationId,
            externalRequestId: externalRequestId,
            retryAfter: TimeSpan.MaxValue,
            diagnostics: diagnostics);
        var same = Create(
            code: new AgentErrorCode("VendorFutureFailure"),
            origin: new ErrorOrigin("custom.mapper/V2"),
            safeMessage: "Safe failure",
            isRetryable: true,
            sideEffectCertainty: SideEffectCertainty.PartiallyPerformed,
            externalCode: "E-Future",
            operationId: operationId,
            externalRequestId: externalRequestId,
            retryAfter: TimeSpan.MaxValue,
            diagnostics: diagnostics);
        var copy = error with { };

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

    private static AgentError Create(
        AgentErrorCode? code = null,
        string safeMessage = "Safe failure",
        bool isRetryable = false,
        SideEffectCertainty sideEffectCertainty = SideEffectCertainty.NotApplicable,
        ErrorOrigin? origin = null,
        string? externalCode = "E1",
        OperationId? operationId = null,
        ExternalRequestId? externalRequestId = null,
        TimeSpan? retryAfter = null,
        ExtensionData? diagnostics = null) =>
        new(
            code ?? AgentErrorCodes.Unknown,
            safeMessage,
            isRetryable,
            sideEffectCertainty,
            origin ?? new ErrorOrigin("agentkit.tests"),
            externalCode,
            operationId,
            externalRequestId,
            retryAfter,
            diagnostics ?? ExtensionData.Empty);
}
