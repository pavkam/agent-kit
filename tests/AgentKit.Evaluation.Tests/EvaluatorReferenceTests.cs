// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluatorReferenceTests
{
    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new EvaluatorReference(default));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenRequiredVersionIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new EvaluatorReference(new EvaluatorKey("schema"), default(EvaluatorVersion)));

        exception.ParamName.ShouldBe("requiredVersion");
    }

    [Fact]
    public void Constructor_WhenVersionIsOmitted_AcceptsAnyRegisteredVersion()
    {
        var reference = new EvaluatorReference(new EvaluatorKey("schema"));

        reference.Key.ShouldBe(new EvaluatorKey("schema"));
        reference.RequiredVersion.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenVersionIsPinned_RecordsIt() =>
        new EvaluatorReference(new EvaluatorKey("schema"), new EvaluatorVersion(3)).RequiredVersion.ShouldBe(new EvaluatorVersion(3));
}
