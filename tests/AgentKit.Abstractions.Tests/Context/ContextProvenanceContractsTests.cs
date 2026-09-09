// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

using System.Reflection;

using AgentKit;

public sealed class ContextProvenanceContractsTests
{
    public static TheoryData<Type> StringValueTypes =>
    [
        typeof(ContextSourceNamespace),
        typeof(ContextSourceKey),
        typeof(ContextSourceVersion),
    ];

    [Theory]
    [MemberData(nameof(StringValueTypes))]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException(Type valueType)
    {
        var exception = Should.Throw<TargetInvocationException>(
            () => Activator.CreateInstance(valueType, [null]));

        var argumentException = exception.InnerException.ShouldBeOfType<ArgumentNullException>();
        argumentException.ParamName.ShouldBe("value");
    }

    [Theory]
    [MemberData(nameof(StringValueTypes))]
    public void Constructor_WhenTextEmptyOrWhitespace_ThrowsArgumentException(Type valueType)
    {
        foreach (var value in new[] { string.Empty, " ", "\t" })
        {
            var exception = Should.Throw<TargetInvocationException>(
                () => Activator.CreateInstance(valueType, value));

            var argumentException = exception.InnerException.ShouldBeOfType<ArgumentException>();
            argumentException.GetType().ShouldBe(typeof(ArgumentException));
            argumentException.ParamName.ShouldBe("value");
        }
    }

    [Theory]
    [MemberData(nameof(StringValueTypes))]
    public void Constructor_WhenTextValid_PreservesExactOrdinalValueEqualityAndDefaultFormatting(Type valueType)
    {
        var upper = Activator.CreateInstance(valueType, "Source-A")!;
        var same = Activator.CreateInstance(valueType, "Source-A")!;
        var lower = Activator.CreateInstance(valueType, "source-a")!;
        var defaultValue = Activator.CreateInstance(valueType)!;

        valueType.GetProperty("Value")!.GetValue(upper).ShouldBe("Source-A");
        upper.ShouldBe(same);
        upper.GetHashCode().ShouldBe(same.GetHashCode());
        upper.ShouldNotBe(lower);
        upper.ToString().ShouldBe("Source-A");
        defaultValue.ToString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void ContextContributorCatalogVersion_Constructor_WhenNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ContextContributorCatalogVersion(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ContextContributorCatalogVersion_Constructor_WhenPositive_PreservesBoundaryEqualityAndInvariantFormatting()
    {
        var version = new ContextContributorCatalogVersion(long.MaxValue);
        var same = new ContextContributorCatalogVersion(long.MaxValue);

        version.Value.ShouldBe(long.MaxValue);
        version.ShouldBe(same);
        version.GetHashCode().ShouldBe(same.GetHashCode());
        version.ToString().ShouldBe("9223372036854775807");
        default(ContextContributorCatalogVersion).Value.ShouldBe(0);
        default(ContextContributorCatalogVersion).ToString().ShouldBe("0");
    }

    [Fact]
    public void ContextSourceReference_Constructor_WhenNamespaceDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ContextSourceReference(default, Key(), Version()));

        exception.ParamName.ShouldBe("sourceNamespace");
    }

    [Fact]
    public void ContextSourceReference_Constructor_WhenKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ContextSourceReference(Namespace(), default, Version()));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ContextSourceReference_Constructor_WhenVersionDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ContextSourceReference(Namespace(), Key(), default));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ContextSourceReference_Constructor_WhenValid_RetainsExactFieldsEqualityHashAndCopy()
    {
        var reference = new ContextSourceReference(Namespace(), Key(), Version());
        var same = new ContextSourceReference(Namespace(), Key(), Version());
        var copy = reference with { };

        reference.Namespace.ShouldBe(Namespace());
        reference.Key.ShouldBe(Key());
        reference.Version.ShouldBe(Version());
        reference.ShouldBe(same);
        reference.GetHashCode().ShouldBe(same.GetHashCode());
        copy.ShouldBe(reference);
        copy.ShouldNotBeSameAs(reference);
    }

    [Fact]
    public void ContextSourceReference_Equality_WhenAnyFieldDiffers_IsNotEqual()
    {
        var reference = new ContextSourceReference(Namespace(), Key(), Version());

        reference.ShouldNotBe(new ContextSourceReference(new("other"), Key(), Version()));
        reference.ShouldNotBe(new ContextSourceReference(Namespace(), new("other"), Version()));
        reference.ShouldNotBe(new ContextSourceReference(Namespace(), Key(), new("other")));
    }

    [Fact]
    public void ContextEnums_WhenEnumerated_ContainOnlyNormativeValues()
    {
        Enum.GetValues<ContextTrust>().ShouldBe(
        [
            ContextTrust.Framework,
            ContextTrust.HostPolicy,
            ContextTrust.AgentDefinition,
            ContextTrust.Workspace,
            ContextTrust.User,
            ContextTrust.RetrievedData,
            ContextTrust.ToolData,
            ContextTrust.ModelGenerated,
        ]);
        Enum.GetValues<ContextEvaluationFrequency>().ShouldBe(
        [
            ContextEvaluationFrequency.OncePerRun,
            ContextEvaluationFrequency.OncePerModelRequest,
        ]);
    }

    private static ContextSourceNamespace Namespace() => new("agentkit.context");

    private static ContextSourceKey Key() => new("instructions");

    private static ContextSourceVersion Version() => new("1.0");
}
