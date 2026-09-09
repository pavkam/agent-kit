// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolCatalogValueTests
{
    [Fact]
    public void TextValueConstructors_WhenValueInvalid_ThrowExactExceptions()
    {
        AssertInvalidText(static value => new ToolAlias(value!), "value");
        AssertInvalidText(static value => new ToolCatalogVersion(value!), "value");
        AssertInvalidText(static value => new ToolSourceId(value!), "value");
        AssertInvalidText(static value => new ToolSourceVersion(value!), "value");
        AssertInvalidText(static value => new ToolExecutionPolicyKey(value!), "value");
    }

    [Fact]
    public void ToolAlias_Constructor_WhenValueValid_PreservesOrdinalText()
    {
        var value = new ToolAlias("Tool.Alias");

        value.Value.ShouldBe("Tool.Alias");
        value.ToString().ShouldBe("Tool.Alias");
        value.ShouldNotBe(new ToolAlias("tool.alias"));
    }

    [Fact]
    public void TextValues_Constructor_WhenValuesValid_PreserveValuesAndEquality()
    {
        var catalog = new ToolCatalogVersion("catalog-1");
        var sourceId = new ToolSourceId("application");
        var sourceVersion = new ToolSourceVersion("release-7");
        var policy = new ToolExecutionPolicyKey("standard");

        catalog.ToString().ShouldBe("catalog-1");
        sourceId.ToString().ShouldBe("application");
        sourceVersion.ToString().ShouldBe("release-7");
        policy.ToString().ShouldBe("standard");
        catalog.ShouldBe(new ToolCatalogVersion("catalog-1"));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    public void ToolExecutionPolicyVersion_Constructor_WhenValueNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyVersion(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToolExecutionPolicyVersion_Constructor_WhenValuePositive_PreservesValue()
    {
        var version = new ToolExecutionPolicyVersion(1);

        version.Value.ShouldBe(1);
        version.ToString().ShouldBe("1");
    }

    [Fact]
    public void ToolExecutionPolicyVersion_Constructor_WhenValueMaximum_PreservesValue()
    {
        var version = new ToolExecutionPolicyVersion(long.MaxValue);

        version.Value.ShouldBe(long.MaxValue);
        version.ToString().ShouldBe("9223372036854775807");
    }

    [Fact]
    public void ValueDefaults_WhenComparedAndFormatted_RemainUninitializedValues()
    {
        default(ToolAlias).ShouldBe(default);
        default(ToolCatalogVersion).ShouldBe(default);
        default(ToolSourceId).ShouldBe(default);
        default(ToolSourceVersion).ShouldBe(default);
        default(ToolExecutionPolicyKey).ShouldBe(default);
        default(ToolExecutionPolicyVersion).ShouldBe(default);
        default(ToolIdentity).ShouldBe(default);
        default(ToolAlias).ToString().ShouldBeNull();
        default(ToolCatalogVersion).ToString().ShouldBeNull();
        default(ToolSourceId).ToString().ShouldBeNull();
        default(ToolSourceVersion).ToString().ShouldBeNull();
        default(ToolExecutionPolicyKey).ToString().ShouldBeNull();
        default(ToolExecutionPolicyVersion).ToString().ShouldBe("0");
    }

    [Fact]
    public void ToolIdentity_Constructor_WhenIdDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolIdentity(default, new ToolVersion("1")));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void ToolIdentity_Constructor_WhenVersionDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolIdentity(new ToolId("read"), default));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ToolIdentity_Constructor_WhenValuesValid_PreservesExactPair()
    {
        var identity = new ToolIdentity(new ToolId("read"), new ToolVersion("2"));

        identity.Id.ShouldBe(new ToolId("read"));
        identity.Version.ShouldBe(new ToolVersion("2"));
    }

    [Fact]
    public void ToolExecutionPolicyReference_Constructor_WhenKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolExecutionPolicyReference(default, new ToolExecutionPolicyVersion(1)));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ToolExecutionPolicyReference_Constructor_WhenVersionDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), default));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ToolExecutionPolicyReference_Constructor_WhenValuesValid_PreservesEvidence()
    {
        var reference = new ToolExecutionPolicyReference(
            new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(3));

        reference.Key.ShouldBe(new ToolExecutionPolicyKey("standard"));
        reference.Version.ShouldBe(new ToolExecutionPolicyVersion(3));
        reference.ShouldBe(new ToolExecutionPolicyReference(
            new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(3)));
    }

    private static void AssertInvalidText(Func<string?, object> construct, string paramName)
    {
        var nullException = Should.Throw<ArgumentNullException>(() => construct(null));
        var emptyException = Should.Throw<ArgumentException>(() => construct(string.Empty));
        var whitespaceException = Should.Throw<ArgumentException>(() => construct(" \t\r\n"));

        nullException.GetType().ShouldBe(typeof(ArgumentNullException));
        emptyException.GetType().ShouldBe(typeof(ArgumentException));
        whitespaceException.GetType().ShouldBe(typeof(ArgumentException));
        nullException.ParamName.ShouldBe(paramName);
        emptyException.ParamName.ShouldBe(paramName);
        whitespaceException.ParamName.ShouldBe(paramName);
    }
}
