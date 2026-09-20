// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using System.Security.Cryptography;
using System.Text;

/// <summary>Deterministic <see cref="HookRegistrationId"/> derivation for first-party registration helpers.</summary>
/// <remarks>
/// Legacy <see cref="IHook"/> implementations identify themselves with <see cref="HookId"/> text. Until every
/// registration supplies an explicit <see cref="HookRegistrationDescriptor"/>, the first-party
/// <c>Add*Hook&lt;T&gt;</c> helpers derive registration identity from that author id so soft and hard ordering
/// edges continue to reference the same stable value.
/// </remarks>
internal static class HookRegistrationIds
{
    private static readonly Guid _namespace = new("f4b9d2c6-3a71-4f5e-9c88-2e6b0d1a7f3e");

    /// <summary>Derives one registration identity from one author-supplied <see cref="HookId"/>.</summary>
    /// <param name="authorId">The hook's declared identity.</param>
    /// <returns>A stable registration identity for catalog capture and ordering.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="authorId"/> is default.</exception>
    public static HookRegistrationId FromAuthorHookId(HookId authorId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(authorId, default);
        var name = $"{_namespace:D}:{authorId.Value}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return new HookRegistrationId(new Guid(hash.AsSpan(0, 16)));
    }

    /// <summary>Maps author ordering edges to registration identities.</summary>
    /// <param name="authorIds">The author-supplied hook identities.</param>
    /// <returns>The corresponding registration identities, preserving order.</returns>
    public static ImmutableArray<HookRegistrationId> FromAuthorHookIds(ImmutableArray<HookId> authorIds)
    {
        if (authorIds.IsDefaultOrEmpty)
        {
            return [];
        }

        var mapped = ImmutableArray.CreateBuilder<HookRegistrationId>(authorIds.Length);
        foreach (var authorId in authorIds)
        {
            mapped.Add(FromAuthorHookId(authorId));
        }

        return mapped.ToImmutable();
    }
}
