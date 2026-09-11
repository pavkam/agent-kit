// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;
/// <summary>Verifies ToolSourceId behavior and contracts.</summary>
public sealed class ToolSourceIdTests: Conformance.StringIdentityConformanceTests<ToolSourceId>
{
    /// <inheritdoc/>
    protected override ToolSourceId Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ToolSourceId subject) => subject.Value;
    [Fact]
    public void TextValueConstructors_WhenValueInvalid_ThrowExactExceptions() => AssertInvalidText(static value => new ToolSourceId(value!), "value");

    [Fact]
    public void TextValues_Constructor_WhenValuesValid_PreserveValuesAndEquality()
    {
        var sourceId = new ToolSourceId("application");
        sourceId.ToString().ShouldBe("application");
    }

    [Fact]
    public void ValueDefaults_WhenComparedAndFormatted_RemainUninitializedValues()
    {
        default(ToolSourceId).ShouldBe(default);
        default(ToolSourceId).ToString().ShouldBeNull();
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
