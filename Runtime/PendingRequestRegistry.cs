using System;
using System.Collections.Generic;
using System.Threading;

namespace Appstack
{
    /// <summary>
    /// Correlates native request IDs with managed callbacks and their captured contexts.
    /// </summary>
    internal sealed class PendingRequestRegistry<TResult>
    {
        private readonly object gate = new object();
        private readonly Dictionary<int, PendingRequest> requests =
            new Dictionary<int, PendingRequest>();
        private int nextRequestId;

        private sealed class PendingRequest
        {
            public PendingRequest(
                Action<TResult> onSuccess,
                Action<string> onError,
                SynchronizationContext synchronizationContext)
            {
                OnSuccess = onSuccess;
                OnError = onError;
                SynchronizationContext = synchronizationContext;
            }

            public Action<TResult> OnSuccess { get; }
            public Action<string> OnError { get; }
            public SynchronizationContext SynchronizationContext { get; }
        }

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return requests.Count;
                }
            }
        }

        public int Register(
            Action<TResult> onSuccess,
            Action<string> onError,
            SynchronizationContext synchronizationContext)
        {
            var requestId = Interlocked.Increment(ref nextRequestId);
            lock (gate)
            {
                requests[requestId] =
                    new PendingRequest(onSuccess, onError, synchronizationContext);
            }

            return requestId;
        }

        public bool TryComplete(
            int requestId,
            Func<TResult> createResult,
            string error)
        {
            PendingRequest pending;
            lock (gate)
            {
                if (!requests.TryGetValue(requestId, out pending))
                {
                    return false;
                }

                requests.Remove(requestId);
            }

            void Complete()
            {
                if (!string.IsNullOrEmpty(error))
                {
                    pending.OnError?.Invoke(error);
                    return;
                }

                pending.OnSuccess?.Invoke(
                    createResult == null ? default : createResult());
            }

            if (pending.SynchronizationContext != null)
            {
                pending.SynchronizationContext.Post(_ => Complete(), null);
            }
            else
            {
                Complete();
            }

            return true;
        }
    }
}
