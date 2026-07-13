using System;
using System.Collections.Generic;
using System.Threading;
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
        public void CompletionPostsToCapturedSynchronizationContext()
        {
            var context = new RecordingSynchronizationContext();
            var registry = new PendingRequestRegistry<string>();
            string result = null;
            var requestId = registry.Register(value => result = value, null, context);

            registry.TryComplete(requestId, () => "posted", null);

            Assert.That(result, Is.Null);
            Assert.That(context.Posted.Count, Is.EqualTo(1));
            Assert.That(registry.Count, Is.Zero);

            context.RunAll();
            Assert.That(result, Is.EqualTo("posted"));
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
