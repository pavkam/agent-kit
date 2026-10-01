// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluatorResultTests
{
    private static EvaluatorResult Build(
        EvaluatorKey? key = null,
        long version = 1,
        EvaluationOutcome? outcome = null,
        TimeSpan? duration = null) =>
        new(key ?? new EvaluatorKey("k"), new EvaluatorVersion(version), outcome ?? new EvaluationSkipped("s"), duration ?? TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenKeyOrVersionIsDefault_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => Build(key: default(EvaluatorKey))).ParamName.ShouldBe("key");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluatorResult(new EvaluatorKey("k"), default, new EvaluationSkipped("s"), TimeSpan.Zero))
            .ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenOutcomeIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new EvaluatorResult(new EvaluatorKey("k"), new EvaluatorVersion(1), null!, TimeSpan.Zero))
            .ParamName.ShouldBe("outcome");

    [Fact]
    public void Constructor_WhenDurationIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(duration: TimeSpan.FromTicks(-1))).ParamName.ShouldBe("duration");

    [Fact]
    public void Equals_WhenOutcomeEvidenceMatchesByValue_IsEqualAndHashesAlike()
    {
        var left = Build(outcome: new EvaluationPassed(null, "ok", [new EvaluationEvidence("a", "1")]));
        var right = Build(outcome: new EvaluationPassed(null, "ok", [new EvaluationEvidence("a", "1")]));

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(Build(outcome: new EvaluationPassed(null, "ok", [new EvaluationEvidence("a", "2")])));
    }
}
