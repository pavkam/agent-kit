// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Declares stable discriminators for every abstract value persisted in SQLite session state.</summary>
internal static class SqliteSessionJsonTypeResolver
{
    /// <summary>Creates the explicit version-one polymorphic resolver.</summary>
    /// <returns>A resolver containing only known provider-neutral session values.</returns>
    internal static IJsonTypeInfoResolver Create()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static info =>
        {
            if (info.Type == typeof(OperationCorrelation))
            {
                info.PolymorphismOptions = Options(
                    (typeof(BeforeRunOperationCorrelation), "before-run"),
                    (typeof(InRunOperationCorrelation), "in-run"),
                    (typeof(AfterRunOperationCorrelation), "after-run"));
            }
            else if (info.Type == typeof(ContentPart))
            {
                info.PolymorphismOptions = Options(
                    (typeof(TextPart), "text"), (typeof(StructuredDataPart), "json"),
                    (typeof(ToolCallPart), "tool-call"), (typeof(ToolResultPart), "tool-result"),
                    (typeof(ReasoningPart), "reasoning"), (typeof(MediaReferencePart), "media"),
                    (typeof(UnknownContentPart), "unknown"));
            }
            else if (info.Type == typeof(AgentMessage))
            {
                info.PolymorphismOptions = Options(
                    (typeof(SystemMessage), "system"), (typeof(DeveloperMessage), "developer"),
                    (typeof(UserMessage), "user"), (typeof(AssistantMessage), "assistant"),
                    (typeof(ToolMessage), "tool"), (typeof(RuntimeMessage), "runtime"));
            }
        });
        return resolver;
    }

    /// <summary>Builds one strict discriminator map.</summary>
    /// <param name="types">Known concrete type and stable discriminator pairs.</param>
    /// <returns>A strict polymorphism configuration.</returns>
    private static JsonPolymorphismOptions Options(params (Type Type, string Discriminator)[] types)
    {
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$kind",
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var (type, discriminator) in types)
        {
            options.DerivedTypes.Add(new JsonDerivedType(type, discriminator));
        }

        return options;
    }
}
