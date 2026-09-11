// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

using AgentKit;

/// <summary>Verifies OutputDefinition behavior and contracts.</summary>
public sealed class OutputDefinitionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenIdentityOrVersionIsDefault_ThrowsArgumentOutOfRangeException(bool defaultIdentity)
    {
        var exception = ShouldThrowExact<ArgumentOutOfRangeException>(() => CreateDefinitionCore(defaultIdentity ? default : new OutputDefinitionId("output"), defaultIdentity ? new OutputDefinitionVersion("1") : default, [], []));
        exception.ParamName.ShouldBe(defaultIdentity ? "id" : "version");
    }

    [Fact]
    public void Constructor_WhenAlternativesContainsNull_ThrowsArgumentException()
    {
        ImmutableArray<OutputAlternative> alternatives = [null!];
        var exception = ShouldThrowExact<ArgumentException>(() => CreateDefinition(alternatives: alternatives));
        exception.ParamName.ShouldBe("alternatives");
    }

    [Fact]
    public void Constructor_WhenValidatorsContainsDefaultReference_ThrowsArgumentNullException()
    {
        var exception = ShouldThrowExact<ArgumentNullException>(() => CreateDefinition(validators: [default]));
        exception.ParamName.ShouldBe("validators");
    }

    [Theory]
    [InlineData("validationPolicy")]
    [InlineData("retryPolicy")]
    public void Constructor_WhenPolicyIsNull_ThrowsArgumentNullException(string parameter)
    {
        var exception = ShouldThrowExact<ArgumentNullException>(() => _ = new OutputDefinition(new OutputDefinitionId("output"), new OutputDefinitionVersion("1"), "Output", OutputMode.Text, schema: null, runtimeType: null, [], [], parameter == "validationPolicy" ? null! : OutputValidationPolicy.RejectOnFirstFailure, parameter == "retryPolicy" ? null! : OutputRetryPolicy.None, OutputEndStrategy.Graceful));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("alternatives")]
    [InlineData("validators")]
    public void Constructor_WhenCollectionIsDefault_ThrowsArgumentException(string parameter)
    {
        var exception = ShouldThrowExact<ArgumentException>(() => CreateDefinitionCore(new OutputDefinitionId("output"), new OutputDefinitionVersion("1"), parameter == "alternatives" ? default : [], parameter == "validators" ? default : []));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Version")]
    public void With_WhenIdentityOrVersionIsDefault_ThrowsArgumentOutOfRangeException(string property)
    {
        var definition = CreateDefinition();
        var exception = ShouldThrowExact<ArgumentOutOfRangeException>(() => _ = property == "Id" ? definition with { Id = default } : definition with { Version = default });
        exception.ParamName.ShouldBe(property);
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData(" ", typeof(ArgumentException))]
    public void With_WhenNameIsInvalid_ThrowsExactException(string? name, Type exceptionType)
    {
        var definition = CreateDefinition();
        var exception = ShouldThrowExact(() => _ = definition with { Name = name! }, exceptionType);
        exception.ParamName.ShouldBe("Name");
    }

    [Theory]
    [InlineData("Mode")]
    [InlineData("EndStrategy")]
    public void With_WhenEnumIsUndefined_ThrowsArgumentOutOfRangeException(string property)
    {
        var definition = CreateDefinition();
        var exception = ShouldThrowExact<ArgumentOutOfRangeException>(() => _ = property == "Mode" ? definition with { Mode = (OutputMode) int.MaxValue } : definition with { EndStrategy = (OutputEndStrategy) int.MaxValue });
        exception.ParamName.ShouldBe(property);
    }

    [Fact]
    public void With_WhenAlternativesContainsNull_ThrowsArgumentException()
    {
        var definition = CreateDefinition();
        var exception = ShouldThrowExact<ArgumentException>(() => _ = definition with { Alternatives = [null!] });
        exception.ParamName.ShouldBe("Alternatives");
    }

    [Fact]
    public void With_WhenValidatorsContainsDefaultReference_ThrowsArgumentNullException()
    {
        var definition = CreateDefinition();
        var exception = ShouldThrowExact<ArgumentNullException>(() => _ = definition with { Validators = [default] });
        exception.ParamName.ShouldBe("Validators");
    }

    [Theory]
    [InlineData("Alternatives")]
    [InlineData("Validators")]
    public void With_WhenCollectionIsDefault_ThrowsArgumentException(string property)
    {
        var definition = CreateDefinition();
        var exception = ShouldThrowExact<ArgumentException>(() => _ = property == "Alternatives" ? definition with { Alternatives = default } : definition with { Validators = default });
        exception.ParamName.ShouldBe(property);
    }

    [Theory]
    [InlineData("ValidationPolicy")]
    [InlineData("RetryPolicy")]
    public void With_WhenPolicyIsNull_ThrowsArgumentNullException(string property)
    {
        var definition = CreateDefinition();
        var exception = ShouldThrowExact<ArgumentNullException>(() => _ = property == "ValidationPolicy" ? definition with { ValidationPolicy = null! } : definition with { RetryPolicy = null! });
        exception.ParamName.ShouldBe(property);
    }

    [Fact]
    public void With_WhenValuesAreValid_CreatesEqualCopyAndAcceptsEmptyCollections()
    {
        var definition = CreateDefinition();
        var copy = definition with
        {
            Alternatives = [],
            Validators = [],
        };
        copy.ShouldBe(definition);
        copy.GetHashCode().ShouldBe(definition.GetHashCode());
    }

    [Fact]
    public void With_WhenValuesChange_CreatesChangedCopyAndPreservesOriginal()
    {
        var definition = CreateDefinition();
        var changed = definition with
        {
            Name = "Changed",
            Mode = OutputMode.Prompted,
            Validators = [new OutputValidatorReference("semantic")],
            EndStrategy = OutputEndStrategy.Exhaustive,
        };
        changed.Name.ShouldBe("Changed");
        changed.Mode.ShouldBe(OutputMode.Prompted);
        changed.Validators.ShouldHaveSingleItem().Name.ShouldBe("semantic");
        changed.EndStrategy.ShouldBe(OutputEndStrategy.Exhaustive);
        definition.Name.ShouldBe("Output");
        definition.Mode.ShouldBe(OutputMode.Text);
        definition.Validators.ShouldBeEmpty();
        definition.EndStrategy.ShouldBe(OutputEndStrategy.Graceful);
    }

    private static OutputDefinition CreateDefinition(OutputDefinitionId? id = null, OutputDefinitionVersion? version = null, ImmutableArray<OutputAlternative>? alternatives = null, ImmutableArray<OutputValidatorReference>? validators = null) => CreateDefinitionCore(id ?? new OutputDefinitionId("output"), version ?? new OutputDefinitionVersion("1"), alternatives ?? [], validators ?? []);
    private static OutputDefinition CreateDefinitionCore(OutputDefinitionId id, OutputDefinitionVersion version, ImmutableArray<OutputAlternative> alternatives, ImmutableArray<OutputValidatorReference> validators) => new(id, version, "Output", OutputMode.Text, schema: null, runtimeType: null, alternatives, validators, OutputValidationPolicy.RejectOnFirstFailure, OutputRetryPolicy.None, OutputEndStrategy.Graceful);
    private static TException ShouldThrowExact<TException>(Action action)
        where TException : Exception
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        return exception;
    }

    private static ArgumentException ShouldThrowExact(Action action, Type exceptionType)
    {
        var exception = Should.Throw<ArgumentException>(action);
        exception.GetType().ShouldBe(exceptionType);
        return exception;
    }
}
