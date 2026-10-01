// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class DefaultEvaluatorCatalogTests
{
    [Fact]
    public void Constructor_WhenServicesAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DefaultEvaluatorCatalog(null!)).ParamName.ShouldBe("services");

    [Fact]
    public void Find_WhenKeyIsBlank_ThrowsArgumentException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Should.Throw<ArgumentException>(() => new DefaultEvaluatorCatalog(provider).Find(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void Find_WhenEvaluatorIsRegisteredUnderTheKey_ReturnsIt()
    {
        var evaluator = new ScriptedEvaluator("known");
        using var provider = new ServiceCollection().AddKeyedSingleton<IEvaluator>("known", evaluator).BuildServiceProvider();

        new DefaultEvaluatorCatalog(provider).Find(new EvaluatorKey("known")).ShouldBeSameAs(evaluator);
    }

    [Fact]
    public void Find_WhenNothingIsRegistered_ReturnsNull()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        new DefaultEvaluatorCatalog(provider).Find(new EvaluatorKey("absent")).ShouldBeNull();
    }

    [Fact]
    public void Find_WhenTheDescriptorKeyDiffersFromTheRegistrationKey_ThrowsInsteadOfChoosing()
    {
        using var provider = new ServiceCollection().AddKeyedSingleton<IEvaluator>("registered", new ScriptedEvaluator("declared")).BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(() => new DefaultEvaluatorCatalog(provider).Find(new EvaluatorKey("registered")));

        exception.Message.ShouldContain("registered");
        exception.Message.ShouldContain("declared");
    }
}
