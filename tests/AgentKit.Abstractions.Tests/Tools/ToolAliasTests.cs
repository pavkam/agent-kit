// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;



/// <summary>Verifies ToolAlias behavior and contracts.</summary>
public sealed class ToolAliasTests: Conformance.StringIdentityConformanceTests<ToolAlias>
{
    [Fact]
    public void ToolAlias_Constructor_WhenValueValid_PreservesOrdinalText()
    {
        var value = new ToolAlias("Tool.Alias");
        value.Value.ShouldBe("Tool.Alias");
        value.ToString().ShouldBe("Tool.Alias");
        value.ShouldNotBe(new ToolAlias("tool.alias"));
    }

    /// <inheritdoc/>
    protected override ToolAlias Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ToolAlias subject) => subject.Value;
    [Fact]
    public void TextValueConstructors_WhenValueInvalid_ThrowExactExceptions() => AssertInvalidText(static value => new ToolAlias(value!), "value");

    [Fact]
    public void ValueDefaults_WhenComparedAndFormatted_RemainUninitializedValues()
    {
        default(ToolAlias).ShouldBe(default);
        default(ToolAlias).ToString().ShouldBeNull();
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
