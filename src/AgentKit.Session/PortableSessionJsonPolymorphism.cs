// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Declares the single stable <c>$kind</c> discriminator map for every closed provider-neutral
/// hierarchy that enters durable session JSON: <see cref="AgentMessage"/>, <see cref="ContentPart"/>,
/// and <see cref="OperationCorrelation"/>.
/// </summary>
/// <remarks>
/// <para>
/// The portable entry codecs and every first-party durable store adapter must serialize these
/// hierarchies with byte-identical discriminators, otherwise a value written by one component cannot
/// be read by another. This type is the only place those discriminators are spelled; adapters call
/// <see cref="Apply"/> from a <see cref="DefaultJsonTypeInfoResolver"/> modifier or read the
/// individual maps instead of copying the tuples.
/// </para>
/// <para>
/// Every map is strict: an unrecognized discriminator fails deserialization and an unlisted derived
/// type fails serialization, so durable data never silently degrades to a base-type payload. Each
/// call returns a fresh mutable <see cref="JsonPolymorphismOptions"/> instance because the
/// serializer takes ownership of the options it receives; callers must not share one instance across
/// resolvers.
/// </para>
/// </remarks>
public static class PortableSessionJsonPolymorphism
{
    /// <summary>The JSON property that carries the discriminator on every polymorphic value.</summary>
    public const string DiscriminatorPropertyName = "$kind";

    /// <summary>Creates the strict discriminator map for the closed <see cref="OperationCorrelation"/> hierarchy.</summary>
    /// <returns>A new options instance listing every correlation kind in declaration order.</returns>
    public static JsonPolymorphismOptions Correlations() => Strict(
        (typeof(BeforeRunOperationCorrelation), "before-run"),
        (typeof(InRunOperationCorrelation), "in-run"),
        (typeof(AfterRunOperationCorrelation), "after-run"));

    /// <summary>Creates the strict discriminator map for the closed <see cref="AgentMessage"/> hierarchy.</summary>
    /// <returns>A new options instance listing every message kind in declaration order.</returns>
    public static JsonPolymorphismOptions Messages() => Strict(
        (typeof(SystemMessage), "system"),
        (typeof(DeveloperMessage), "developer"),
        (typeof(UserMessage), "user"),
        (typeof(AssistantMessage), "assistant"),
        (typeof(ToolMessage), "tool"),
        (typeof(RuntimeMessage), "runtime"));

    /// <summary>Creates the strict discriminator map for the closed <see cref="ContentPart"/> hierarchy.</summary>
    /// <returns>A new options instance listing every content-part kind in declaration order.</returns>
    public static JsonPolymorphismOptions Parts() => Strict(
        (typeof(TextPart), "text"),
        (typeof(StructuredDataPart), "structured"),
        (typeof(ToolCallPart), "tool-call"),
        (typeof(ToolResultPart), "tool-result"),
        (typeof(ReasoningPart), "reasoning"),
        (typeof(MediaReferencePart), "media"),
        (typeof(UnknownContentPart), "unknown"));

    /// <summary>
    /// Assigns the matching discriminator map when <paramref name="typeInfo"/> describes one of the
    /// polymorphic base types, and leaves every other contract untouched.
    /// </summary>
    /// <param name="typeInfo">The contract being built by a <see cref="DefaultJsonTypeInfoResolver"/> modifier.</param>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfo"/> is null.</exception>
    public static void Apply(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (typeInfo.Type == typeof(OperationCorrelation))
        {
            typeInfo.PolymorphismOptions = Correlations();
        }
        else if (typeInfo.Type == typeof(AgentMessage))
        {
            typeInfo.PolymorphismOptions = Messages();
        }
        else if (typeInfo.Type == typeof(ContentPart))
        {
            typeInfo.PolymorphismOptions = Parts();
        }
    }

    /// <summary>Creates a reflection-based resolver that already applies every portable discriminator map.</summary>
    /// <returns>A new resolver whose modifier list contains <see cref="Apply"/>.</returns>
    public static DefaultJsonTypeInfoResolver CreateResolver()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(Apply);
        return resolver;
    }

    /// <summary>Builds one strict, ordered discriminator map.</summary>
    /// <param name="derivedTypes">The concrete type and stable discriminator pairs in declaration order.</param>
    /// <returns>Options that fail on unknown discriminators and unlisted derived types.</returns>
    private static JsonPolymorphismOptions Strict(params ReadOnlySpan<(Type Type, string Discriminator)> derivedTypes)
    {
        Debug.Assert(derivedTypes.Length > 0, "A discriminator map must list at least one derived type.");
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = DiscriminatorPropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var (type, discriminator) in derivedTypes)
        {
            options.DerivedTypes.Add(new JsonDerivedType(type, discriminator));
        }

        return options;
    }
}
