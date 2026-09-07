// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Exercises recovery decision guards and the closure of the decision,
/// evidence-result, and decode-result hierarchies.
/// </summary>
public sealed class RecoveryDecisionTests
{
    [Fact]
    public void RecoveryReconcileOperation_Constructor_WhenReferenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new RecoveryReconcileOperation(null!));

        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void RecoveryCommitRecordedResult_Constructor_WhenResultIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new RecoveryCommitRecordedResult(null!));

        exception.ParamName.ShouldBe("result");
    }

    [Fact]
    public void RecoveryRequiresOperator_Constructor_WhenReasonIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryRequiresOperator(" "));

        exception.ParamName.ShouldBe("safeReason");
    }

    [Fact]
    public void RecoveryNotPossible_Constructor_WhenReasonIsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryNotPossible(null!));

        exception.ParamName.ShouldBe("safeReason");
    }

    [Fact]
    public void RecoveryRetryOperation_Constructor_WhenOptionalsOmitted_AreNull()
    {
        var decision = new RecoveryRetryOperation();

        decision.NotBefore.ShouldBeNull();
        decision.ExternalIdempotencyKey.ShouldBeNull();
    }

    [Fact]
    public void RecoveryRetryOperation_Constructor_PreservesNotBeforeAndKey()
    {
        var decision = new RecoveryRetryOperation(
            DurabilityTestData.Now,
            new IdempotencyKey("external"));

        decision.NotBefore.ShouldBe(DurabilityTestData.Now);
        decision.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("external"));
    }

    [Fact]
    public void RecoveryDecision_Hierarchy_ContainsOnlyTheSixDeclaredKinds()
    {
        var kinds = typeof(RecoveryDecision).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(RecoveryDecision)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        kinds.ShouldBe([
            nameof(RecoveryCommitRecordedResult),
            nameof(RecoveryNotPossible),
            nameof(RecoveryReconcileOperation),
            nameof(RecoveryRequiresOperator),
            nameof(RecoveryRetryOperation),
            nameof(RecoveryStartOperation),
        ]);
    }

    [Fact]
    public void RecoveryEvidenceLoaded_Constructor_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new RecoveryEvidenceLoaded(null!));

        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void RecoveryEvidenceNotFound_Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new RecoveryEvidenceNotFound(null!));

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void RecoveryEvidenceUnavailable_Constructor_WhenMessageIsEmpty_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new RecoveryEvidenceUnavailable(string.Empty));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void RecoveryEvidenceResult_Hierarchy_ContainsOnlyTheThreeDeclaredKinds()
    {
        var kinds = typeof(RecoveryEvidenceResult).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(RecoveryEvidenceResult)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        kinds.ShouldBe([
            nameof(RecoveryEvidenceLoaded),
            nameof(RecoveryEvidenceNotFound),
            nameof(RecoveryEvidenceUnavailable),
        ]);
    }

    [Fact]
    public void DurableDecodeIncompatible_Constructor_WhenReasonIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new DurableDecodeIncompatible<string>(new SchemaVersion("v1"), "  "));

        exception.ParamName.ShouldBe("safeReason");
    }

    [Fact]
    public void DurableDecoded_Constructor_PreservesState() =>
        new DurableDecoded<string>("state").State.ShouldBe("state");

    [Fact]
    public void DurableDecodeResult_Hierarchy_ContainsOnlyTheTwoDeclaredKinds()
    {
        var kinds = typeof(DurableDecodeResult<string>).Assembly
            .GetTypes()
            .Where(type =>
                type.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(DurableDecodeResult<>))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        kinds.ShouldBe(["DurableDecodeIncompatible`1", "DurableDecoded`1"]);
    }
}
