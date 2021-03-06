// -----------------------------------------------------------------------
//  <copyright file="AsyncHelpers.cs" company="Hibernating Rhinos LTD">
//      Copyright (coffee) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Extensions;

namespace Raven.Client.Util
{
    public static class AsyncHelpers
    {
        public static void RunSync(Func<Task> task)
        {
            var oldContext = SynchronizationContext.Current;
            try
            {
                var synch = new ExclusiveSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(synch);
                synch.Post(async _ =>
                {
                    try
                    {
                        await task().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        synch.InnerException = e;
                        throw;
                    }
                    finally
                    {
                        synch.EndMessageLoop();
                    }
                }, null);
                synch.BeginMessageLoop();
            }
            catch (AggregateException ex)
            {
                var exception = ex.ExtractSingleInnerException();
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }
        }

        public static T RunSync<T>(Func<Task<T>> task)
        {
            var result = default(T);
            Stopwatch sp = Stopwatch.StartNew();
            var oldContext = SynchronizationContext.Current;
            try
            {
                var synch = new ExclusiveSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(synch);

                synch.Post(async _ =>
                {
                    try
                    {
                        result = await task().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        synch.InnerException = e;
                        throw;
                    }
                    finally
                    {
                        sp.Stop();
                        synch.EndMessageLoop();
                    }
                }, null);
                synch.BeginMessageLoop();
            }
            catch (AggregateException ex)
            {
                var exception = ex.ExtractSingleInnerException();
                if (exception is OperationCanceledException)
                    throw new TimeoutException("Operation timed out after: " + sp.Elapsed, ex);
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }

            return result;
        }

        private class ExclusiveSynchronizationContext : SynchronizationContext
        {
            private readonly AutoResetEvent _workItemsWaiting = new AutoResetEvent(false);
            private readonly Queue<Tuple<SendOrPostCallback, object>> _items = new Queue<Tuple<SendOrPostCallback, object>>();

            private bool _done;
            public Exception InnerException { private get; set; }

            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException("We cannot send to our same thread");
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (_items)
                {
                    _items.Enqueue(Tuple.Create(d, state));
                }
                _workItemsWaiting.Set();
            }

            public void EndMessageLoop()
            {
                Post(_ => _done = true, null);
            }

            public void BeginMessageLoop()
            {
                while (!_done)
                {
                    Tuple<SendOrPostCallback, object> task = null;
                    lock (_items)
                    {
                        if (_items.Count > 0)
                        {
                            task = _items.Dequeue();
                        }
                    }
                    if (task != null)
                    {
                        task.Item1(task.Item2);
                        if (InnerException != null) // the method threw an exception
                        {
                            throw new AggregateException("AsyncHelpers.Run method threw an exception.", InnerException);
                        }
                    }
                    else
                    {
                        _workItemsWaiting.WaitOne();
                    }
                }
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }
        }
    }
}