// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;
using System.Runtime.CompilerServices;

/// <summary>
/// Reusable <see cref="ArgumentOutOfRangeException"/> guard clauses for
/// constraints the base class library does not already expose as a
/// <c>ThrowIf*</c> member.
/// </summary>
public static class ArgumentOutOfRangeExceptionExtensions
{
    extension(ArgumentOutOfRangeException)
    {
        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> when
        /// <paramref name="value"/> is not a named value of
        /// <typeparamref name="TEnum"/>.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type to validate.</typeparam>
        /// <param name="value">The enumeration value to validate.</param>
        /// <param name="paramName">
        /// The caller-supplied parameter name, inferred from
        /// <paramref name="value"/> when omitted.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is not a named enumeration value.
        /// </exception>
        public static void ThrowIfUndefined<TEnum>(
            TEnum value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where TEnum : struct, Enum
        {
            var enumType = typeof(TEnum);
            if (Attribute.IsDefined(enumType, typeof(FlagsAttribute)))
            {
                var mask = Enum.GetValues<TEnum>().Aggregate(
                    0L,
                    static (accumulator, item) => accumulator | Convert.ToInt64(item, CultureInfo.InvariantCulture));
                var bits = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if ((bits & ~mask) != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        paramName,
                        value,
                        $"Value must be a defined {enumType.Name} flags combination.");
                }

                return;
            }

            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    $"Value must be a defined {enumType.Name} value.");
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> when
        /// <paramref name="value"/> cannot represent a durable operation
        /// result that recovery may commit without reinvoking its effect.
        /// </summary>
        /// <param name="value">The lifecycle state to validate.</param>
        /// <param name="paramName">
        /// The caller-supplied parameter name, inferred from
        /// <paramref name="value"/> when omitted.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is undefined or is an in-progress state
        /// without one complete staged or published result.
        /// </exception>
        public static void ThrowIfNotTerminalDurableOperationState(
            DurableOperationState value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, paramName);
            if (value is not (DurableOperationState.OutcomeReady or DurableOperationState.Completed or DurableOperationState.Faulted))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Value must be OutcomeReady, Completed, or Faulted when recording a durable operation result.");
            }
        }
    }
}
