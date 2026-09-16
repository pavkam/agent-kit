// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Verifies the strict bounded evidence reader fails closed on corrupted or tampered persisted state.</summary>
public sealed class SqliteSecurityGrantCodecReaderTests
{
    private static readonly SqliteSecurityGrantStoreSettings _settings = SqliteSecurityGrantStoreSettings.CreateDefault();

    [Fact]
    public void Constructor_WhenSettingsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(CreateWithNullSettings);

        exception.ParamName.ShouldBe("settings");
    }

    [Fact]
    public void ReadScope_WhenCorrelationKindByteIsUnsupported_ThrowsInvalidDataException()
    {
        // A valid non-empty agent identity followed by "no session" (0) and an unsupported correlation kind (4).
        var agentIdBytes = SqliteSecurityGrantCodec.EncodeGuid(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var payload = new byte[agentIdBytes.Length + 2];
        agentIdBytes.CopyTo(payload, 0);
        payload[16] = 0;
        payload[17] = 4;

        var exception = Should.Throw<InvalidDataException>(() => ReadScope(payload));

        exception.Message.ShouldContain("correlation");
    }

    [Fact]
    public void ReadResources_WhenPersistedCountIsZero_ThrowsInvalidDataException()
    {
        // A zero-valued big-endian int32 count; WriteResources never persists this, so only tampered evidence reaches it.
        var exception = Should.Throw<InvalidDataException>(() => ReadResources(new byte[4]));

        exception.Message.ShouldContain("empty");
    }

    [Fact]
    public void ReadBoolean_WhenPersistedByteIsNeitherZeroNorOne_ThrowsInvalidDataException()
    {
        // ReadAuthorization's leading discriminator byte is read through the same private ReadBoolean helper.
        var exception = Should.Throw<InvalidDataException>(() => ReadAuthorization([2]));

        exception.Message.ShouldContain("boolean");
    }

    private static void CreateWithNullSettings() => _ = new SqliteSecurityGrantCodecReader([], null!);

    private static void ReadScope(byte[] payload)
    {
        var reader = new SqliteSecurityGrantCodecReader(payload, _settings);
        _ = reader.ReadScope();
    }

    private static void ReadResources(byte[] payload)
    {
        var reader = new SqliteSecurityGrantCodecReader(payload, _settings);
        _ = reader.ReadResources();
    }

    private static void ReadAuthorization(byte[] payload)
    {
        var reader = new SqliteSecurityGrantCodecReader(payload, _settings);
        _ = reader.ReadAuthorization();
    }
}
