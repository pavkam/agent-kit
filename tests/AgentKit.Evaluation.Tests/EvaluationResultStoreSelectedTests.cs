// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationResultStoreSelectedTests
{
    private sealed class NullStore: IEvaluationResultStore
    {
        public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => new EvaluationResultStoreSelected(default, new NullStore())).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new EvaluationResultStoreSelected(new EvaluationResultStoreKey("s"), null!)).ParamName.ShouldBe("store");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_BorrowsTheSuppliedStore()
    {
        var store = new NullStore();

        var selection = new EvaluationResultStoreSelected(new EvaluationResultStoreKey("s"), store);

        selection.Store.ShouldBeSameAs(store);
        selection.Key.ShouldBe(new EvaluationResultStoreKey("s"));
    }
}
