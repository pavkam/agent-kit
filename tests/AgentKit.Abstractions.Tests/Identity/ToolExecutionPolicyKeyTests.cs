// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;
/// <summary>Verifies ToolExecutionPolicyKey behavior and contracts.</summary>
public sealed class ToolExecutionPolicyKeyTests: Conformance.StringIdentityConformanceTests<ToolExecutionPolicyKey>
{
    /// <inheritdoc/>
    protected override ToolExecutionPolicyKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ToolExecutionPolicyKey subject) => subject.Value;
    [Fact]
    public void TextValueConstructors_WhenValueInvalid_ThrowExactExceptions() => AssertInvalidText(static value => new ToolExecutionPolicyKey(value!), "value");

    [Fact]
    public void TextValues_Constructor_WhenValuesValid_PreserveValuesAndEquality()
    {
        var policy = new ToolExecutionPolicyKey("standard");
        policy.ToString().ShouldBe("standard");
    }

    [Fact]
    public void ValueDefaults_WhenComparedAndFormatted_RemainUninitializedValues()
    {
        default(ToolExecutionPolicyKey).ShouldBe(default);
        default(ToolExecutionPolicyKey).ToString().ShouldBeNull();
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
