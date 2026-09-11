// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;
/// <summary>Verifies ToolSourceVersion behavior and contracts.</summary>
public sealed class ToolSourceVersionTests: Conformance.StringIdentityConformanceTests<ToolSourceVersion>
{
    /// <inheritdoc/>
    protected override ToolSourceVersion Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ToolSourceVersion subject) => subject.Value;
    [Fact]
    public void TextValueConstructors_WhenValueInvalid_ThrowExactExceptions() => AssertInvalidText(static value => new ToolSourceVersion(value!), "value");

    [Fact]
    public void TextValues_Constructor_WhenValuesValid_PreserveValuesAndEquality()
    {
        var sourceVersion = new ToolSourceVersion("release-7");
        sourceVersion.ToString().ShouldBe("release-7");
    }

    [Fact]
    public void ValueDefaults_WhenComparedAndFormatted_RemainUninitializedValues()
    {
        default(ToolSourceVersion).ShouldBe(default);
        default(ToolSourceVersion).ToString().ShouldBeNull();
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
