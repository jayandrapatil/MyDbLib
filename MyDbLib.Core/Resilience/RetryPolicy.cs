using MyDbLib.Api.Interfaces;
using System;
using System.Linq;
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

        // Operations that do NOT return a value like InsertAsync, DeleteAsync, tx.CommitAsync()
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

        // Operations that DO return a value like QueryAsync, InsertAndGetIdAsync, UpdateAsync
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

        /// <summary>
        /// Determines whether an exception is transient.
        /// This logic is intentionally DB-agnostic.
        /// </summary>
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
