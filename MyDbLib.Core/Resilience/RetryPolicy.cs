using MyDbLib.Api.Exceptions;
using MyDbLib.Api.Interfaces;
using MyDbLib.Api.Models;
using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyDbLib.Core.Resilience
{
    /// <summary>
    /// Default retry policy implementation.
    /// Handles transient failures in a DB-agnostic manner.
    /// </summary>
    public sealed class RetryPolicy : IRetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _delay;

        public RetryPolicy(int maxRetries, TimeSpan delay)
        {
            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries));

            _maxRetries = maxRetries;
            _delay = delay;
        }

        // ASYNC (no return)
        public async Task ExecuteAsync(Func<Task> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await ExecuteAsync<object>(async () =>
            {
                await action();
                return null!;
            });
        }

        // ASYNC (with return)
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            int attempt = 0;

            while (true)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < _maxRetries)
                {
                    attempt++;
                    await Task.Delay(_delay);
                }
            }
        }

        // SYNC
        public T Execute<T>(Func<T> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            int attempt = 0;

            while (true)
            {
                try
                {
                    return action();
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < _maxRetries)
                {
                    attempt++;
                    Thread.Sleep(_delay);
                }
            }
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex == null)
                return false;

            if (ex is TimeoutException)
                return true;

            var name = ex.GetType().Name;

            return name.IndexOf("Deadlock", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Transient", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

}
