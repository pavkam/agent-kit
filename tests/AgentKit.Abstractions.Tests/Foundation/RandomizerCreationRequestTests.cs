// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

/// <summary>Verifies RandomizerCreationRequest behavior and contracts.</summary>
public sealed class RandomizerCreationRequestTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var operation = new OperationId(Guid.NewGuid());
        var purpose = new RandomizerPurpose("retry-jitter");

        var request = new RandomizerCreationRequest(operation, purpose);

        request.OperationId.ShouldBe(operation);
        request.Purpose.ShouldBe(purpose);
    }

    [Fact]
    public void Constructor_WhenOperationIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RandomizerCreationRequest(default, new RandomizerPurpose("p")))
            .ParamName.ShouldBe("operationId");

    [Fact]
    public void Constructor_WhenPurposeIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new RandomizerCreationRequest(new OperationId(Guid.NewGuid()), default))
            .ParamName.ShouldBe("purpose");
}
