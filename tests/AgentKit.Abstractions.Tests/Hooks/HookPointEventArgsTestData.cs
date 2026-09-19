// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using System.Text.Json;

/// <summary>Shared builders for the first-party hook point event arguments.</summary>
internal static class HookPointEventArgsTestData
{
    public static AgentId AgentId { get; } = new(Guid.NewGuid());

    public static SessionId SessionId { get; } = new(Guid.NewGuid());

    public static InRunOperationCorrelation TurnCorrelation { get; } =
        new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));

    public static InRunOperationCorrelation RunCorrelation { get; } =
        new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    public static HookDispatchMetadata RunDispatch(HookPointId point, DateTimeOffset? timestamp = null) =>
        new(point, new HookDispatchId(Guid.NewGuid()), RunCorrelation, timestamp ?? DateTimeOffset.UnixEpoch, (timestamp ?? DateTimeOffset.UnixEpoch) + TimeSpan.FromMinutes(1));

    public static HookDispatchMetadata TurnDispatch(HookPointId point, DateTimeOffset? timestamp = null) =>
        new(point, new HookDispatchId(Guid.NewGuid()), TurnCorrelation, timestamp ?? DateTimeOffset.UnixEpoch, (timestamp ?? DateTimeOffset.UnixEpoch) + TimeSpan.FromMinutes(1));

    public static HookDispatchMetadata OutOfRunDispatch(HookPointId point, DateTimeOffset? timestamp = null) =>
        new(point, new HookDispatchId(Guid.NewGuid()), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null),
            timestamp ?? DateTimeOffset.UnixEpoch, (timestamp ?? DateTimeOffset.UnixEpoch) + TimeSpan.FromMinutes(1));

    public static ModelDescriptor Model { get; } = new(
        new ModelAlias("chat"), new ProviderId("test"), new ApiFamilyId("test"), new ModelId("m"), null,
        new ModelCapabilities(true, true, true, true, false, false, false, ExtensionData.Empty),
        new ModelLimits(null, null), null, ExtensionData.Empty);

    public static LlmRequestContext Request(LlmRequestSettings? settings = null) => new(
        new ModelRequestId(Guid.NewGuid()), Model, [], [], LlmToolChoice.Auto, settings ?? LlmRequestSettings.Default, ExtensionData.Empty);

    public static ToolCallPart Call(string argumentsJson = /*lang=json,strict*/ """{"path":"a.txt"}""") => new(
        new ToolCallId(Guid.NewGuid()),
        new ToolReference(new ToolAlias("read_file"), null, null),
        JsonDocument.Parse(argumentsJson).RootElement.Clone(),
        null,
        ExtensionData.Empty);
}
