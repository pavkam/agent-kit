// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that the flattened correlation document reconstructs the exact member of the closed domain hierarchy.</summary>
/// <remarks>
/// JSON cannot express a closed class hierarchy, so this mirror carries the union of three disjoint member sets behind one
/// discriminator. The risk it must not realize is cross-contamination: an in-run document acquiring an admission receipt, an
/// after-run document acquiring a turn, or a lost discriminator silently decoding as a before-run correlation.
/// </remarks>
public sealed class JsonOperationCorrelationTests
{
    /// <summary>Verifies a null correlation is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonOperationCorrelation.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a before-run correlation reconstructs as the same concrete kind with the same identities.</summary>
    [Fact]
    public void ToDomain_WhenKindIsBeforeRun_ReproducesBeforeRunCorrelation()
    {
        var original = TestEvidenceFactory.BeforeRun(withAdmission: true);

        var restored = JsonOperationCorrelation.FromDomain(original).ToDomain();

        _ = restored.ShouldBeOfType<BeforeRunOperationCorrelation>();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies an in-run correlation reconstructs as the same concrete kind with its run and turn intact.</summary>
    [Fact]
    public void ToDomain_WhenKindIsInRun_ReproducesInRunCorrelation()
    {
        var original = TestEvidenceFactory.InRun(withTurn: true);

        var restored = JsonOperationCorrelation.FromDomain(original).ToDomain();

        _ = restored.ShouldBeOfType<InRunOperationCorrelation>();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies an after-run correlation reconstructs as the same concrete kind with its settled causal run.</summary>
    [Fact]
    public void ToDomain_WhenKindIsAfterRun_ReproducesAfterRunCorrelation()
    {
        var original = TestEvidenceFactory.AfterRun();

        var restored = JsonOperationCorrelation.FromDomain(original).ToDomain();

        _ = restored.ShouldBeOfType<AfterRunOperationCorrelation>();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies every kind survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEveryKind()
    {
        OperationCorrelation[] originals =
        [
            TestEvidenceFactory.BeforeRun(withAdmission: true),
            TestEvidenceFactory.InRun(withTurn: true),
            TestEvidenceFactory.AfterRun(),
        ];

        foreach (var original in originals)
        {
            var restored = TestCanonicalJson.Cycle(JsonOperationCorrelation.FromDomain(original)).ToDomain();

            restored.ShouldBe(original);
            restored.GetType().ShouldBe(original.GetType());
        }
    }

    /// <summary>Verifies a before-run correlation without an admission receipt does not acquire one.</summary>
    [Fact]
    public void FromDomain_WhenAdmissionIsAbsent_LeavesAdmissionIdNull()
    {
        var original = TestEvidenceFactory.BeforeRun(withAdmission: false);

        var document = JsonOperationCorrelation.FromDomain(original);

        document.AdmissionId.ShouldBeNull();
        TestCanonicalJson.Cycle(document).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies a before-run correlation carries no run or turn identity, which those kinds alone define.</summary>
    [Fact]
    public void FromDomain_WhenKindIsBeforeRun_LeavesRunAndTurnNull()
    {
        var document = JsonOperationCorrelation.FromDomain(TestEvidenceFactory.BeforeRun(withAdmission: true));

        document.Kind.ShouldBe(JsonOperationCorrelationKind.BeforeRun);
        document.RunId.ShouldBeNull();
        document.TurnId.ShouldBeNull();
    }

    /// <summary>Verifies a run-scoped in-run correlation is not upgraded to a turn-scoped one.</summary>
    [Fact]
    public void FromDomain_WhenTurnIsAbsent_LeavesTurnIdNull()
    {
        var original = TestEvidenceFactory.InRun(withTurn: false);

        var document = JsonOperationCorrelation.FromDomain(original);

        document.TurnId.ShouldBeNull();
        document.AdmissionId.ShouldBeNull();
        TestCanonicalJson.Cycle(document).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies an after-run correlation carries neither an admission receipt nor a turn.</summary>
    [Fact]
    public void FromDomain_WhenKindIsAfterRun_LeavesAdmissionAndTurnNull()
    {
        var document = JsonOperationCorrelation.FromDomain(TestEvidenceFactory.AfterRun());

        document.Kind.ShouldBe(JsonOperationCorrelationKind.AfterRun);
        document.AdmissionId.ShouldBeNull();
        document.TurnId.ShouldBeNull();
        _ = document.RunId.ShouldNotBeNull();
    }

    /// <summary>Verifies a lost or invalid discriminator fails closed instead of inventing a before-run correlation.</summary>
    [Fact]
    public void ToDomain_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonOperationCorrelation(
            default, Guid.Parse("33333333-3333-3333-3333-333333333333"), null, null, null);

        var exception = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);

        exception.ParamName.ShouldBe("Kind");
    }

    /// <summary>Verifies an in-run document missing its required run identity is refused rather than defaulted.</summary>
    [Fact]
    public void ToDomain_WhenInRunRunIdIsMissing_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonOperationCorrelation(
            JsonOperationCorrelationKind.InRun,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            null,
            null,
            null);

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an after-run document missing its causal run identity is refused rather than defaulted.</summary>
    [Fact]
    public void ToDomain_WhenAfterRunRunIdIsMissing_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonOperationCorrelation(
            JsonOperationCorrelationKind.AfterRun,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            null,
            null,
            null);

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an empty persisted operation identity is rejected instead of rebuilt as a default identity.</summary>
    [Fact]
    public void ToDomain_WhenOperationIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var document = new JsonOperationCorrelation(
            JsonOperationCorrelationKind.BeforeRun, Guid.Empty, null, null, null);

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies two documents decoded from byte-identical JSON compare equal, which store deduplication relies on.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonOperationCorrelation.FromDomain(TestEvidenceFactory.InRun(withTurn: true));

        TestCanonicalJson.Cycle(document).ShouldBe(TestCanonicalJson.Cycle(document));
    }
}
