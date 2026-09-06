// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Extensibility;

using AgentKit;

public sealed class ExtensionDataTests
{
    [Fact]
    public void Constructor_WhenValuesIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ExtensionData(null!));

        exception.ParamName.ShouldBe("values");
    }

    [Fact]
    public void Empty_WhenAccessed_HasNoValues() => ExtensionData.Empty.Values.ShouldBeEmpty();

    [Fact]
    public void Empty_WhenAccessedTwice_ReturnsSameInstance() =>
        // Documented as a shared instance to avoid needless allocation on
        // the common "no extensions" path.
        ReferenceEquals(ExtensionData.Empty, ExtensionData.Empty).ShouldBeTrue();

    [Fact]
    public void Constructor_WhenValuesArePopulated_RoundTrips()
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("safety", new ExtensionValue([1, 2, 3]));

        var data = new ExtensionData(values);

        data.Values.ShouldContainKey("safety");
    }

    [Fact]
    public void Equality_WhenValuesMatch_InstancesAreEqual()
    {
        var values = ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("k", new ExtensionValue([9]));

        var first = new ExtensionData(values);
        var second = new ExtensionData(values);

        first.ShouldBe(second);
    }
}
