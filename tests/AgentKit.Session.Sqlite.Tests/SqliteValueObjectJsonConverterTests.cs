// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using System.Text.Json;

/// <summary>Verifies <see cref="SqliteValueObjectJsonConverter{T}"/> deserialization behavior.</summary>
public sealed class SqliteValueObjectJsonConverterTests
{
    private static JsonSerializerOptions Options() => new()
    {
        Converters = { new SqliteValueObjectJsonConverterFactory() },
    };

    [Fact]
    public void Deserialize_WhenThePersistedValueFailsTheStructsValidatingConstructor_ThrowsJsonExceptionInsteadOfTargetInvocationException()
    {
        // OperationStateRevision's constructor throws ArgumentOutOfRangeException for a non-positive value. The
        // converter rebuilds the struct through ConstructorInfo.Invoke, which previously wrapped that exception
        // in a TargetInvocationException instead of the JsonException every other malformed-payload failure
        // documented by ReadEntriesAsync/Deserialize<T> uses, escaping the store as an unrelated reflection
        // failure rather than the corrupt-payload failure it actually is.
        var json = """{"Value":0}""";

        var exception = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<OperationStateRevision>(json, Options()));

        _ = exception.InnerException.ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Deserialize_WhenThePersistedValueIsValid_RoundTripsThroughTheValidatingConstructor()
    {
        var json = """{"Value":7}""";

        var value = JsonSerializer.Deserialize<OperationStateRevision>(json, Options());

        value.Value.ShouldBe(7);
    }
}
