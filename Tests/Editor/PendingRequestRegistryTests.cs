using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Appstack.Tests
{
    public sealed class PendingRequestRegistryTests
    {
        [Test]
        public void SuccessfulCompletionInvokesOnceAndRemovesRequest()
        {
            var registry = new PendingRequestRegistry<string>();
            string result = null;
            var requestId = registry.Register(value => result = value, null, null);

            var first = registry.TryComplete(requestId, () => "done", null);
            var duplicate = registry.TryComplete(requestId, () => "duplicate", null);

            Assert.That(first, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(result, Is.EqualTo("done"));
            Assert.That(registry.Count, Is.Zero);
        }

        [Test]
        public void ErrorCompletionSkipsResultAndInvokesError()
        {
            var registry = new PendingRequestRegistry<string>();
            var resultFactoryCalled = false;
            string error = null;
            var requestId = registry.Register(null, value => error = value, null);

            registry.TryComplete(
                requestId,
                () =>
                {
                    resultFactoryCalled = true;
                    return "unexpected";
                },
                "native failure");

            Assert.That(resultFactoryCalled, Is.False);
            Assert.That(error, Is.EqualTo("native failure"));
            Assert.That(registry.Count, Is.Zero);
        }

        [Test]
        public void CompletionPostsExactlyOnceToCapturedSynchronizationContext()
        {
            var context = new RecordingSynchronizationContext();
            var registry = new PendingRequestRegistry<string>();
            string result = null;
            var requestId = registry.Register(value => result = value, null, context);

            registry.TryComplete(requestId, () => "posted", null);
            registry.TryComplete(requestId, () => "duplicate", null);

            Assert.That(result, Is.Null);
            Assert.That(context.Posted.Count, Is.EqualTo(1));
            Assert.That(registry.Count, Is.Zero);

            context.RunAll();
            Assert.That(result, Is.EqualTo("posted"));
        }

        [Test]
        public void NullSynchronizationContextCompletesSynchronously()
        {
            var registry = new PendingRequestRegistry<string>();
            var methodReturned = false;
            var callbackObservedReturn = true;
            var requestId = registry.Register(
                _ => callbackObservedReturn = methodReturned,
                null,
                null);

            registry.TryComplete(requestId, () => "immediate", null);
            methodReturned = true;

            Assert.That(callbackObservedReturn, Is.False);
        }

        [Test]
        public void UnknownRequestDoesNotCreateResult()
        {
            var registry = new PendingRequestRegistry<string>();
            var resultFactoryCalled = false;

            var completed = registry.TryComplete(
                404,
                () =>
                {
                    resultFactoryCalled = true;
                    return "unexpected";
                },
                null);

            Assert.That(completed, Is.False);
            Assert.That(resultFactoryCalled, Is.False);
        }

        [Test]
        public void ResultFactoryFailureStillRemovesRequest()
        {
            var registry = new PendingRequestRegistry<string>();
            var requestId = registry.Register(_ => Assert.Fail(), null, null);

            Assert.Throws<InvalidOperationException>(
                () => registry.TryComplete(
                    requestId,
                    () => throw new InvalidOperationException("parse failed"),
                    null));

            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.TryComplete(requestId, () => "late", null), Is.False);
        }

        [Test]
        public void OutOfOrderResponsesReachTheirMatchingCallbacks()
        {
            const int count = 100;
            var registry = new PendingRequestRegistry<int>();
            var requestIds = new int[count];
            var results = new int[count];

            for (var index = 0; index < count; index++)
            {
                var capturedIndex = index;
                requestIds[index] = registry.Register(
                    value => results[capturedIndex] = value,
                    null,
                    null);
            }

            for (var index = count - 1; index >= 0; index--)
            {
                var value = index + 1;
                registry.TryComplete(requestIds[index], () => value, null);
            }

            Assert.That(results, Is.EqualTo(Enumerable.Range(1, count).ToArray()));
            Assert.That(registry.Count, Is.Zero);
        }

        [Test]
        [Timeout(5000)]
        public void ParallelRegistrationsProduceUniquePositiveIds()
        {
            const int count = 2000;
            var registry = new PendingRequestRegistry<int>();
            var requestIds = new int[count];

            Parallel.For(
                0,
                count,
                index => requestIds[index] = registry.Register(null, null, null));

            Assert.That(requestIds, Has.All.GreaterThan(0));
            Assert.That(requestIds.Distinct().Count(), Is.EqualTo(count));
            Assert.That(registry.Count, Is.EqualTo(count));
        }

        [Test]
        [Timeout(5000)]
        public void ConcurrentRequestsCompleteExactlyOnceWithoutCrossTalk()
        {
            const int count = 1000;
            var registry = new PendingRequestRegistry<int>();
            var requestIds = new int[count];
            var results = new int[count];
            var callbackCalls = 0;

            Parallel.For(
                0,
                count,
                index =>
                {
                    var capturedIndex = index;
                    requestIds[index] = registry.Register(
                        value =>
                        {
                            results[capturedIndex] = value;
                            Interlocked.Increment(ref callbackCalls);
                        },
                        null,
                        null);
                });

            Parallel.For(
                0,
                count,
                index =>
                {
                    var value = index + 1;
                    registry.TryComplete(requestIds[index], () => value, null);
                    registry.TryComplete(requestIds[index], () => -1, null);
                });

            Assert.That(callbackCalls, Is.EqualTo(count));
            Assert.That(results, Is.EqualTo(Enumerable.Range(1, count).ToArray()));
            Assert.That(registry.Count, Is.Zero);
        }

        [Test]
        public void RequestIdsRollFromMaximumBackToOneWithoutCollision()
        {
            var registry = new PendingRequestRegistry<string>(int.MaxValue - 1);

            var maximum = registry.Register(null, null, null);
            var wrapped = registry.Register(null, null, null);

            Assert.That(maximum, Is.EqualTo(int.MaxValue));
            Assert.That(wrapped, Is.EqualTo(1));
            Assert.That(registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void WrappedRequestIdSkipsAnExistingPendingRequest()
        {
            var registry = new PendingRequestRegistry<string>();
            var first = registry.Register(null, null, null);
            var nextRequestId = typeof(PendingRequestRegistry<string>).GetField(
                "nextRequestId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(nextRequestId, Is.Not.Null);
            nextRequestId.SetValue(registry, int.MaxValue);

            var wrapped = registry.Register(null, null, null);

            Assert.That(first, Is.EqualTo(1));
            Assert.That(wrapped, Is.EqualTo(2));
        }

        [Test]
        public void NewRegistryStartsWithCleanState()
        {
            var previous = new PendingRequestRegistry<string>();
            previous.Register(null, null, null);

            var reloaded = new PendingRequestRegistry<string>();
            var requestId = reloaded.Register(null, null, null);

            Assert.That(reloaded.Count, Is.EqualTo(1));
            Assert.That(requestId, Is.EqualTo(1));
        }

        private sealed class RecordingSynchronizationContext : SynchronizationContext
        {
            public List<Action> Posted { get; } = new List<Action>();

            public override void Post(SendOrPostCallback callback, object state)
            {
                Posted.Add(() => callback(state));
            }

            public void RunAll()
            {
                foreach (var callback in Posted)
                {
                    callback();
                }

                Posted.Clear();
            }
        }
    }
}
