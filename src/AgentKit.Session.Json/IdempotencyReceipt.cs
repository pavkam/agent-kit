// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>
/// Retains the stable request evidence and terminal result for one accepted session operation.
/// </summary>
/// <typeparam name="TRequest">The immutable request type whose structural equality defines replay equivalence.</typeparam>
/// <typeparam name="TResult">The immutable terminal result returned by an equivalent replay.</typeparam>
/// <remarks>
/// The containing store projects receipts into memory, and the durable record log lets replay rebuild every one of them
/// after process loss, so a retry issued across a restart still reconciles against the original evidence. The request
/// captures operation target, tenant-scoped identity, concurrency version, payload, and extensions, while caller
/// cancellation is intentionally not part of replay evidence.
/// </remarks>
internal sealed class IdempotencyReceipt<TRequest, TResult>
    where TRequest : class
    where TResult : class
{
    /// <summary>Initializes accepted replay evidence and its immutable terminal receipt.</summary>
    /// <param name="request">The non-null immutable request used as canonical replay evidence.</param>
    /// <param name="result">The non-null immutable terminal result returned to equivalent retries.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    internal IdempotencyReceipt(TRequest request, TResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        Request = request;
        Result = result;
    }

    /// <summary>Gets the immutable canonical evidence accepted for this idempotency key.</summary>
    public TRequest Request { get; }

    /// <summary>Gets the immutable terminal receipt returned by an equivalent replay.</summary>
    public TResult Result { get; }
}
