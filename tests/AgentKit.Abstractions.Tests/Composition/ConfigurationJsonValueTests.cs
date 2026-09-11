// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies ConfigurationJsonValue behavior and contracts.</summary>
public sealed class ConfigurationJsonValueTests
{
    [Fact]
    public void ConfigurationJsonValue_Constructor_WhenDocumentDisposed_OwnsStructuralValue()
    {
        ConfigurationJsonValue value;
        using (var document = JsonDocument.Parse("{\"enabled\":true}"))
        {
            value = new ConfigurationJsonValue(document.RootElement);
        }

        using var equivalent = JsonDocument.Parse("{\"enabled\":true}");
        var same = new ConfigurationJsonValue(equivalent.RootElement);
        value.Value.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        value.ShouldBe(same);
        value.GetHashCode().ShouldBe(same.GetHashCode());
    }

    [Fact]
    public void ConfigurationJsonValue_Constructor_WhenUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ConfigurationJsonValue(default));
        exception.ParamName.ShouldBe("value");
    }
}
