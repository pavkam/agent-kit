// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Declares stable discriminators for every abstract value persisted in SQLite session state.</summary>
/// <remarks>
/// The SQLite adapter does not spell its own discriminators. It reuses
/// <see cref="PortableSessionJsonPolymorphism"/>, the single source of truth shared with the portable
/// entry codecs, so a message, content part, or correlation written through either path reads back
/// through the other.
/// </remarks>
internal static class SqliteSessionJsonTypeResolver
{
    /// <summary>Creates the explicit version-one polymorphic resolver.</summary>
    /// <returns>A strict resolver containing only known provider-neutral session values.</returns>
    internal static IJsonTypeInfoResolver Create() => PortableSessionJsonPolymorphism.CreateResolver();
}
