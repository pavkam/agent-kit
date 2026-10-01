// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationStoreAppendedTests
{
    [Fact]
    public void Constructor_WhenReceiptIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new EvaluationStoreAppended(null!, false)).ParamName.ShouldBe("receipt");

    [Fact]
    public void Constructor_WhenReplayIsFlagged_DistinguishesFreshFromReplayedAcknowledgements()
    {
        var receipt = new EvaluationResultReceipt(EvaluationTestData.RunId, new EvaluationCaseId("c"), 0, 1);

        new EvaluationStoreAppended(receipt, true).Replayed.ShouldBeTrue();
        new EvaluationStoreAppended(receipt, false).Replayed.ShouldBeFalse();
        new EvaluationStoreAppended(receipt, true).ShouldNotBe(new EvaluationStoreAppended(receipt, false));
    }
}
