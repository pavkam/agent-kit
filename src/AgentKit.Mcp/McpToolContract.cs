// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

using System.ComponentModel;
using System.Reflection;

/// <summary>Provides the immutable reflected MCP tool surface declared by <typeparamref name="TTools"/>.</summary>
/// <typeparam name="TTools">
/// A class whose public instance tool methods carry <see cref="McpToolAttribute"/>, accept one request
/// object and an optional trailing <see cref="CancellationToken"/>, and return
/// <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/> containing a response object.
/// </typeparam>
public sealed class McpToolContract<TTools>
    where TTools : class
{
    private readonly ImmutableDictionary<MethodInfo, McpToolMethodDescriptor> _methodsByReflectionIdentity;

    /// <summary>Creates and validates the complete reflected contract.</summary>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TTools"/> is not a class, has no attributed tools, contains duplicate
    /// names, or declares a tool with an unsupported request or return shape.
    /// </exception>
    public McpToolContract()
    {
        var toolClass = typeof(TTools).IsClass
            ? typeof(TTools)
            : throw new InvalidOperationException($"MCP tool surface '{typeof(TTools)}' must be a class.");

        var methods = toolClass
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(static method => (Method: method, Attribute: method.GetCustomAttribute<McpToolAttribute>(inherit: true)))
            .Where(static candidate => candidate.Attribute is not null)
            .OrderBy(static candidate => candidate.Method.MetadataToken)
            .Select(static candidate => Describe(candidate.Method, candidate.Attribute!))
            .ToImmutableArray();

        Methods = !methods.IsEmpty
            ? methods
            : throw new InvalidOperationException($"MCP tool surface '{toolClass}' does not declare any attributed tool methods.");

        var duplicate = methods.GroupBy(static method => method.Name).FirstOrDefault(static group => group.Count() > 1);
        _methodsByReflectionIdentity = duplicate is null
            ? methods.ToImmutableDictionary(static descriptor => descriptor.Method)
            : throw new InvalidOperationException(
                $"MCP tool surface '{toolClass}' declares duplicate tool name '{duplicate.Key}'. MCP names do not select tool versions.");
    }

    /// <summary>Gets the reflected methods in deterministic metadata order.</summary>
    public ImmutableArray<McpToolMethodDescriptor> Methods { get; }

    /// <summary>Resolves an expression-reflected method to its validated descriptor.</summary>
    /// <param name="method">The method selected from <typeparamref name="TTools"/>.</param>
    /// <returns>The matching descriptor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is not part of this contract.</exception>
    public McpToolMethodDescriptor Resolve(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return _methodsByReflectionIdentity.TryGetValue(method, out var descriptor)
            ? descriptor
            : throw new ArgumentException(
                $"Method '{method.Name}' is not an MCP tool in '{typeof(TTools)}'.",
                nameof(method));
    }

    private static McpToolMethodDescriptor Describe(MethodInfo method, McpToolAttribute attribute)
    {
        if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
        {
            throw Invalid(method, "must not be generic");
        }

        var parameters = method.GetParameters();
        var cancellationParameters = parameters.Where(static parameter => parameter.ParameterType == typeof(CancellationToken)).ToArray();
        if (cancellationParameters.Length > 1 ||
            (cancellationParameters is [{ Position: var position }] && position != parameters.Length - 1))
        {
            throw Invalid(method, "may declare at most one trailing CancellationToken");
        }

        var requestParameters = parameters.Where(static parameter => parameter.ParameterType != typeof(CancellationToken)).ToArray();
        if (requestParameters is not [var request] || request.IsOut || request.ParameterType.IsByRef || !IsObjectShape(request.ParameterType))
        {
            throw Invalid(method, "must accept exactly one request object plus an optional trailing CancellationToken");
        }

        var responseType = UnwrapResponse(method.ReturnType) ??
            throw Invalid(method, "must return Task<TResponse> or ValueTask<TResponse>");

        if (!IsObjectShape(responseType))
        {
            throw Invalid(method, "must return an asynchronous response object rather than a primitive value");
        }

        var description = method.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description ?? method.Name;
        return new McpToolMethodDescriptor(
            attribute.Name,
            attribute.Version,
            description,
            method,
            request.Name!,
            request.ParameterType,
            responseType,
            attribute.ReadOnly,
            attribute.Idempotent,
            attribute.OpenWorld,
            attribute.Destructive);
    }

    private static bool IsObjectShape(Type type) =>
        type.IsClass && !type.IsArray && type != typeof(string) && !typeof(Delegate).IsAssignableFrom(type);

    private static Type? UnwrapResponse(Type returnType) =>
        returnType.IsGenericType &&
        (returnType.GetGenericTypeDefinition() == typeof(Task<>) ||
         returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            ? returnType.GetGenericArguments()[0]
            : null;

    private static InvalidOperationException Invalid(MethodInfo method, string requirement) =>
        new($"MCP tool method '{method.DeclaringType}.{method.Name}' {requirement}.");
}
