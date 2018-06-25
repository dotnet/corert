// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: Class for creating and managing a threadpool
**
**
=============================================================================*/

#pragma warning disable 0420

/*
 * Below you'll notice two sets of APIs that are separated by the
 * use of 'Unsafe' in their names.  The unsafe versions are called
 * that because they do not propagate the calling stack onto the
 * worker thread.  This allows code to lose the calling stack and 
 * thereby elevate its security privileges.  Note that this operation
 * is much akin to the combined ability to control security policy
 * and control security evidence.  With these privileges, a person 
 * can gain the right to load assemblies that are fully trusted which
 * then assert full trust and can call any code they want regardless
 * of the previous stack information.
 */

using Internal.Runtime.Augments;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace System.Threading
{
    internal static class ThreadPoolGlobals
    {
        public static readonly int processorCount = Environment.ProcessorCount;

        private static ThreadPoolWorkQueue _workQueue;
        public static ThreadPoolWorkQueue workQueue
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _workQueue, () => new ThreadPoolWorkQueue());
            }
        }
    }

    internal sealed class ThreadPoolWorkQueue
    {
        internal static class WorkStealingQueueList
        {
            private static volatile WorkStealingQueue[] _queues = new WorkStealingQueue[0];

            public static WorkStealingQueue[] Queues => _queues;

            public static void Add(WorkStealingQueue queue)
            {
                Debug.Assert(queue != null);
                while (true)
                {
                    WorkStealingQueue[] oldQueues = _queues;
                    Debug.Assert(Array.IndexOf(oldQueues, queue) == -1);

                    var newQueues = new WorkStealingQueue[oldQueues.Length + 1];
                    Array.Copy(oldQueues, 0, newQueues, 0, oldQueues.Length);
                    newQueues[newQueues.Length - 1] = queue;
                    if (Interlocked.CompareExchange(ref _queues, newQueues, oldQueues) == oldQueues)
                    {
                        break;
                    }
                }
            }

            public static void Remove(WorkStealingQueue queue)
            {
                Debug.Assert(queue != null);
                while (true)
                {
                    WorkStealingQueue[] oldQueues = _queues;
                    if (oldQueues.Length == 0)
                    {
                        return;
                    }

                    int pos = Array.IndexOf(oldQueues, queue);
                    if (pos == -1)
                    {
                        Debug.Fail("Should have found the queue");
                        return;
                    }

                    var newQueues = new WorkStealingQueue[oldQueues.Length - 1];
                    if (pos == 0)
                    {
                        Array.Copy(oldQueues, 1, newQueues, 0, newQueues.Length);
                    }
                    else if (pos == oldQueues.Length - 1)
                    {
                        Array.Copy(oldQueues, 0, newQueues, 0, newQueues.Length);
                    }
                    else
                    {
                        Array.Copy(oldQueues, 0, newQueues, 0, pos);
                        Array.Copy(oldQueues, pos + 1, newQueues, pos, newQueues.Length - pos);
                    }

                    if (Interlocked.CompareExchange(ref _queues, newQueues, oldQueues) == oldQueues)
                    {
                        break;
                    }
                }
            }
        }

        internal sealed class WorkStealingQueue
        {
            private const int INITIAL_SIZE = 32;
            internal volatile IThreadPoolWorkItem[] m_array = new IThreadPoolWorkItem[INITIAL_SIZE];
            private volatile int m_mask = INITIAL_SIZE - 1;

#if DEBUG
            // in debug builds, start at the end so we exercise the index reset logic.
            private const int START_INDEX = int.MaxValue;
#else
            private const int START_INDEX = 0;
#endif

            private volatile int m_headIndex = START_INDEX;
            private volatile int m_tailIndex = START_INDEX;

            private SpinLock m_foreignLock = new SpinLock(enableThreadOwnerTracking: false);

            public void LocalPush(IThreadPoolWorkItem obj)
            {
                int tail = m_tailIndex;

                // We're going to increment the tail; if we'll overflow, then we need to reset our counts
                if (tail == int.MaxValue)
                {
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);

                        if (m_tailIndex == int.MaxValue)
                        {
                            //
                            // Rather than resetting to zero, we'll just mask off the bits we don't care about.
                            // This way we don't need to rearrange the items already in the queue; they'll be found
                            // correctly exactly where they are.  One subtlety here is that we need to make sure that
                            // if head is currently < tail, it remains that way.  This happens to just fall out from
                            // the bit-masking, because we only do this if tail == int.MaxValue, meaning that all
                            // bits are set, so all of the bits we're keeping will also be set.  Thus it's impossible
                            // for the head to end up > than the tail, since you can't set any more bits than all of 
                            // them.
                            //
                            m_headIndex = m_headIndex & m_mask;
                            m_tailIndex = tail = m_tailIndex & m_mask;
                            Debug.Assert(m_headIndex <= m_tailIndex);
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(useMemoryBarrier: true);
                    }
                }

                // When there are at least 2 elements' worth of space, we can take the fast path.
                if (tail < m_headIndex + m_mask)
                {
                    Volatile.Write(ref m_array[tail & m_mask], obj);
                    m_tailIndex = tail + 1;
                }
                else
                {
                    // We need to contend with foreign pops, so we lock.
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);

                        int head = m_headIndex;
                        int count = m_tailIndex - m_headIndex;

                        // If there is still space (one left), just add the element.
                        if (count >= m_mask)
                        {
                            // We're full; expand the queue by doubling its size.
                            var newArray = new IThreadPoolWorkItem[m_array.Length << 1];
                            for (int i = 0; i < m_array.Length; i++)
                                newArray[i] = m_array[(i + head) & m_mask];

                            // Reset the field values, incl. the mask.
                            m_array = newArray;
                            m_headIndex = 0;
                            m_tailIndex = tail = count;
                            m_mask = (m_mask << 1) | 1;
                        }

                        Volatile.Write(ref m_array[tail & m_mask], obj);
                        m_tailIndex = tail + 1;
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(useMemoryBarrier: false);
                    }
                }
            }

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            public bool LocalFindAndPop(IThreadPoolWorkItem obj)
            {
                // Fast path: check the tail. If equal, we can skip the lock.
                if (m_array[(m_tailIndex - 1) & m_mask] == obj)
                {
                    IThreadPoolWorkItem unused = LocalPop();
                    Debug.Assert(unused == null || unused == obj);
                    return unused != null;
                }

                // Else, do an O(N) search for the work item. The theory of work stealing and our
                // inlining logic is that most waits will happen on recently queued work.  And
                // since recently queued work will be close to the tail end (which is where we
                // begin our search), we will likely find it quickly.  In the worst case, we
                // will traverse the whole local queue; this is typically not going to be a
                // problem (although degenerate cases are clearly an issue) because local work
                // queues tend to be somewhat shallow in length, and because if we fail to find
                // the work item, we are about to block anyway (which is very expensive).
                for (int i = m_tailIndex - 2; i >= m_headIndex; i--)
                {
                    if (m_array[i & m_mask] == obj)
                    {
                        // If we found the element, block out steals to avoid interference.
                        bool lockTaken = false;
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);

                            // If we encountered a race condition, bail.
                            if (m_array[i & m_mask] == null)
                                return false;

                            // Otherwise, null out the element.
                            Volatile.Write(ref m_array[i & m_mask], null);

                            // And then check to see if we can fix up the indexes (if we're at
                            // the edge).  If we can't, we just leave nulls in the array and they'll
                            // get filtered out eventually (but may lead to superflous resizing).
                            if (i == m_tailIndex)
                                m_tailIndex -= 1;
                            else if (i == m_headIndex)
                                m_headIndex += 1;

                            return true;
                        }
                        finally
                        {
                            if (lockTaken)
                                m_foreignLock.Exit(useMemoryBarrier: false);
                        }
                    }
                }

                return false;
            }

            public IThreadPoolWorkItem LocalPop() => m_headIndex < m_tailIndex ? LocalPopCore() : null;

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            private IThreadPoolWorkItem LocalPopCore()
            {
                while (true)
                {
                    int tail = m_tailIndex;
                    if (m_headIndex >= tail)
                    {
                        return null;
                    }

                    // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                    tail -= 1;
                    Interlocked.Exchange(ref m_tailIndex, tail);

                    // If there is no interaction with a take, we can head down the fast path.
                    if (m_headIndex <= tail)
                    {
                        int idx = tail & m_mask;
                        IThreadPoolWorkItem obj = Volatile.Read(ref m_array[idx]);

                        // Check for nulls in the array.
                        if (obj == null) continue;

                        m_array[idx] = null;
                        return obj;
                    }
                    else
                    {
                        // Interaction with takes: 0 or 1 elements left.
                        bool lockTaken = false;
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);

                            if (m_headIndex <= tail)
                            {
                                // Element still available. Take it.
                                int idx = tail & m_mask;
                                IThreadPoolWorkItem obj = Volatile.Read(ref m_array[idx]);

                                // Check for nulls in the array.
                                if (obj == null) continue;

                                m_array[idx] = null;
                                return obj;
                            }
                            else
                            {
                                // If we encountered a race condition and element was stolen, restore the tail.
                                m_tailIndex = tail + 1;
                                return null;
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                                m_foreignLock.Exit(useMemoryBarrier: false);
                        }
                    }
                }
            }

            public bool CanSteal => m_headIndex < m_tailIndex;

            public IThreadPoolWorkItem TrySteal(ref bool missedSteal)
            {
                while (true)
                {
                    if (CanSteal)
                    {
                        bool taken = false;
                        try
                        {
                            m_foreignLock.TryEnter(ref taken);
                            if (taken)
                            {
                                // Increment head, and ensure read of tail doesn't move before it (fence).
                                int head = m_headIndex;
                                Interlocked.Exchange(ref m_headIndex, head + 1);

                                if (head < m_tailIndex)
                                {
                                    int idx = head & m_mask;
                                    IThreadPoolWorkItem obj = Volatile.Read(ref m_array[idx]);

                                    // Check for nulls in the array.
                                    if (obj == null) continue;

                                    m_array[idx] = null;
                                    return obj;
                                }
                                else
                                {
                                    // Failed, restore head.
                                    m_headIndex = head;
                                }
                            }
                        }
                        finally
                        {
                            if (taken)
                                m_foreignLock.Exit(useMemoryBarrier: false);
                        }

                        missedSteal = true;
                    }

                    return null;
                }
            }
        }

        internal readonly ConcurrentQueue<IThreadPoolWorkItem> workItems = new ConcurrentQueue<IThreadPoolWorkItem>();
        
        private volatile int numOutstandingThreadRequests = 0;

        // The number of threads executing work items in the Dispatch method
        internal volatile int numWorkingThreads;

        public ThreadPoolWorkQueue()
        {
        }

        public ThreadPoolWorkQueueThreadLocals EnsureCurrentThreadHasQueue() =>
            ThreadPoolWorkQueueThreadLocals.threadLocals ??
            (ThreadPoolWorkQueueThreadLocals.threadLocals = new ThreadPoolWorkQueueThreadLocals(this));

        internal void EnsureThreadRequested()
        {
            //
            // If we have not yet requested #procs threads, then request a new thread.
            //
            int count = numOutstandingThreadRequests;
            while (count < ThreadPoolGlobals.processorCount)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count + 1, count);
                if (prev == count)
                {
                    ThreadPool.RequestWorkerThread();
                    break;
                }
                count = prev;
            }
        }

        internal void MarkThreadRequestSatisfied()
        {
            //
            // One of our outstanding thread requests has been satisfied.
            // Decrement the count so that future calls to EnsureThreadRequested will succeed.
            //
            int count = numOutstandingThreadRequests;
            while (count > 0)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count - 1, count);
                if (prev == count)
                {
                    break;
                }
                count = prev;
            }
        }

        public void Enqueue(IThreadPoolWorkItem callback, bool forceGlobal)
        {
            ThreadPoolWorkQueueThreadLocals tl = null;
            if (!forceGlobal)
                tl = ThreadPoolWorkQueueThreadLocals.threadLocals;

            if (null != tl)
            {
                tl.workStealingQueue.LocalPush(callback);
            }
            else
            {
                workItems.Enqueue(callback);
            }

            EnsureThreadRequested();
        }

        internal bool LocalFindAndPop(IThreadPoolWorkItem callback)
        {
            ThreadPoolWorkQueueThreadLocals tl = ThreadPoolWorkQueueThreadLocals.threadLocals;
            return tl != null && tl.workStealingQueue.LocalFindAndPop(callback);
        }

        public IThreadPoolWorkItem Dequeue(ThreadPoolWorkQueueThreadLocals tl, ref bool missedSteal)
        {
            WorkStealingQueue localWsq = tl.workStealingQueue;
            IThreadPoolWorkItem callback;

            if ((callback = localWsq.LocalPop()) == null && // first try the local queue
                !workItems.TryDequeue(out callback)) // then try the global queue
            {
                // finally try to steal from another thread's local queue
                WorkStealingQueue[] queues = WorkStealingQueueList.Queues;
                int c = queues.Length;
                Debug.Assert(c > 0, "There must at least be a queue for this thread.");
                int maxIndex = c - 1;
                int i = tl.random.Next(c);
                while (c > 0)
                {
                    i = (i < maxIndex) ? i + 1 : 0;
                    WorkStealingQueue otherQueue = queues[i];
                    if (otherQueue != localWsq && otherQueue.CanSteal)
                    {
                        callback = otherQueue.TrySteal(ref missedSteal);
                        if (callback != null)
                        {
                            break;
                        }
                    }
                    c--;
                }
            }

            return callback;
        }

        /// <summary>
        /// Dispatches work items to this thread.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this thread did as much work as was available or its quantum expired.
        /// <c>false</c> if this thread stopped working early.
        /// </returns>
        internal static bool Dispatch()
        {
            var workQueue = ThreadPoolGlobals.workQueue;

            //
            // Save the start time
            //
            int startTickCount = Environment.TickCount;

            //
            // Update our records to indicate that an outstanding request for a thread has now been fulfilled.
            // From this point on, we are responsible for requesting another thread if we stop working for any
            // reason, and we believe there might still be work in the queue.
            //
            workQueue.MarkThreadRequestSatisfied();

            Interlocked.Increment(ref workQueue.numWorkingThreads);

            //
            // Assume that we're going to need another thread if this one returns to the VM.  We'll set this to 
            // false later, but only if we're absolutely certain that the queue is empty.
            //
            bool needAnotherThread = true;
            IThreadPoolWorkItem workItem = null;
            try
            {
                //
                // Set up our thread-local data
                //
                ThreadPoolWorkQueueThreadLocals tl = workQueue.EnsureCurrentThreadHasQueue();

                //
                // Loop until our quantum expires or there is no work.
                //
                while (ThreadPool.KeepDispatching(startTickCount))
                {
                    bool missedSteal = false;
                    workItem = workQueue.Dequeue(tl, ref missedSteal);

                    if (workItem == null)
                    {
                        //
                        // No work.
                        // If we missed a steal, though, there may be more work in the queue.
                        // Instead of looping around and trying again, we'll just request another thread.  Hopefully the thread
                        // that owns the contended work-stealing queue will pick up its own workitems in the meantime, 
                        // which will be more efficient than this thread doing it anyway.
                        //
                        needAnotherThread = missedSteal;

                        // Tell the VM we're returning normally, not because Hill Climbing asked us to return.
                        return true;
                    }

                    //
                    // If we found work, there may be more work.  Ask for another thread so that the other work can be processed
                    // in parallel.  Note that this will only ask for a max of #procs threads, so it's safe to call it for every dequeue.
                    //
                    workQueue.EnsureThreadRequested();

                    try
                    {
                        SynchronizationContext.SetSynchronizationContext(null);
                        workItem.ExecuteWorkItem();
                    }
                    finally
                    {
                        workItem = null;
                        SynchronizationContext.SetSynchronizationContext(null);
                    }

                    RuntimeThread.CurrentThread.ResetThreadPoolThread();

                    if (!ThreadPool.NotifyWorkItemComplete())
                        return false;
                }

                // If we get here, it's because our quantum expired.
                return true;
            }
            catch (Exception e)
            {
                // Work items should not allow exceptions to escape.  For example, Task catches and stores any exceptions.
                Environment.FailFast("Unhandled exception in ThreadPool dispatch loop", e);
                return true; // Will never actually be executed because Environment.FailFast doesn't return
            }
            finally
            {
                int numWorkers = Interlocked.Decrement(ref workQueue.numWorkingThreads);
                Debug.Assert(numWorkers >= 0);

                //
                // If we are exiting for any reason other than that the queue is definitely empty, ask for another
                // thread to pick up where we left off.
                //
                if (needAnotherThread)
                    workQueue.EnsureThreadRequested();
            }
        }
    }

    // Simple random number generator. We don't need great randomness, we just need a little and for it to be fast.
    internal struct FastRandom // xorshift prng
    {
        private uint _w, _x, _y, _z;

        public FastRandom(int seed)
        {
            _x = (uint)seed;
            _w = 88675123;
            _y = 362436069;
            _z = 521288629;
        }

        public int Next(int maxValue)
        {
            Debug.Assert(maxValue > 0);

            uint t = _x ^ (_x << 11);
            _x = _y; _y = _z; _z = _w;
            _w = _w ^ (_w >> 19) ^ (t ^ (t >> 8));

            return (int)(_w % (uint)maxValue);
        }
    }

    // Holds a WorkStealingQueue, and remmoves it from the list when this object is no longer referened.
    internal sealed class ThreadPoolWorkQueueThreadLocals
    {
        [ThreadStatic]
        public static ThreadPoolWorkQueueThreadLocals threadLocals;

        public readonly ThreadPoolWorkQueue workQueue;
        public readonly ThreadPoolWorkQueue.WorkStealingQueue workStealingQueue;
        public FastRandom random = new FastRandom(Environment.CurrentManagedThreadId);  // mutable struct, do not copy or make readonly

        public ThreadPoolWorkQueueThreadLocals(ThreadPoolWorkQueue tpq)
        {
            workQueue = tpq;
            workStealingQueue = new ThreadPoolWorkQueue.WorkStealingQueue();
            ThreadPoolWorkQueue.WorkStealingQueueList.Add(workStealingQueue);
        }

        private void CleanUp()
        {
            if (null != workStealingQueue)
            {
                if (null != workQueue)
                {
                    IThreadPoolWorkItem cb;
                    while ((cb = workStealingQueue.LocalPop()) != null)
                    {
                        Debug.Assert(null != cb);
                        workQueue.Enqueue(cb, forceGlobal: true);
                    }
                }

                ThreadPoolWorkQueue.WorkStealingQueueList.Remove(workStealingQueue);
            }
        }

        ~ThreadPoolWorkQueueThreadLocals()
        {
            // Since the purpose of calling CleanUp is to transfer any pending workitems into the global
            // queue so that they will be executed by another thread, there's no point in doing this cleanup
            // if we're in the process of shutting down or unloading the AD.  In those cases, the work won't
            // execute anyway.  And there are subtle races involved there that would lead us to do the wrong
            // thing anyway.  So we'll only clean up if this is a "normal" finalization.
            if (!(Environment.HasShutdownStarted /*|| AppDomain.CurrentDomain.IsFinalizingForUnload()*/))
                CleanUp();
        }
    }

    public delegate void WaitCallback(object state);

    public delegate void WaitOrTimerCallback(object state, bool timedOut);  // signalled or timed out

    //
    // Interface to something that can be queued to the TP.  This is implemented by 
    // QueueUserWorkItemCallback, Task, and potentially other internal types.
    // For example, SemaphoreSlim represents callbacks using its own type that
    // implements IThreadPoolWorkItem.
    //
    // If we decide to expose some of the workstealing
    // stuff, this is NOT the thing we want to expose to the public.
    //
    internal interface IThreadPoolWorkItem
    {
        void ExecuteWorkItem();
    }

    internal abstract class QueueUserWorkItemCallbackBase : IThreadPoolWorkItem
    {
#if DEBUG
        private volatile int executed;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1821:RemoveEmptyFinalizers")]
        ~QueueUserWorkItemCallbackBase()
        {
            Debug.Assert(
                executed != 0 || Environment.HasShutdownStarted /*|| AppDomain.CurrentDomain.IsFinalizingForUnload()*/,
                "A QueueUserWorkItemCallback was never called!");
        }

        protected void MarkExecuted()
        {
            GC.SuppressFinalize(this);
            Debug.Assert(
                0 == Interlocked.Exchange(ref executed, 1),
                "A QueueUserWorkItemCallback was called twice!");
        }
#endif

        public virtual void ExecuteWorkItem()
        {
#if DEBUG
            MarkExecuted();
#endif
        }
    }

    internal sealed class QueueUserWorkItemCallback : QueueUserWorkItemCallbackBase
    {
        private WaitCallback _callback;
        private readonly object _state;
        private readonly ExecutionContext _context;

        internal static readonly ContextCallback s_executionContextShim = state =>
        {
            var obj = (QueueUserWorkItemCallback)state;
            WaitCallback c = obj._callback;
            Debug.Assert(c != null);
            obj._callback = null;
            c(obj._state);
        };

        internal QueueUserWorkItemCallback(WaitCallback callback, object state, ExecutionContext context)
        {
            _callback = callback;
            _state = state;
            _context = context;
        }

        public override void ExecuteWorkItem()
        {
            base.ExecuteWorkItem();
            try
            {
                if (_context == null)
                {
                    WaitCallback c = _callback;
                    _callback = null;
                    c(_state);
                }
                else
                {
                    ExecutionContext.Run(_context, s_executionContextShim, this);
                }
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
                throw; //unreachable
            }
        }
    }

    internal sealed class QueueUserWorkItemCallback<TState> : QueueUserWorkItemCallbackBase
    {
        private Action<TState> _callback;
        private readonly TState _state;
        private readonly ExecutionContext _context;

        internal static readonly ContextCallback s_executionContextShim = state =>
        {
            var obj = (QueueUserWorkItemCallback<TState>)state;
            Action<TState> c = obj._callback;
            Debug.Assert(c != null);
            obj._callback = null;
            c(obj._state);
        };

        internal QueueUserWorkItemCallback(Action<TState> callback, TState state, ExecutionContext context)
        {
            _callback = callback;
            _state = state;
            _context = context;
        }

        public override void ExecuteWorkItem()
        {
            base.ExecuteWorkItem();
            try
            {
                if (_context == null)
                {
                    Action<TState> c = _callback;
                    _callback = null;
                    c(_state);
                }
                else
                {
                    ExecutionContext.RunInternal(_context, s_executionContextShim, this);
                }
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
                throw; //unreachable
            }
        }
    }

    internal sealed class QueueUserWorkItemCallbackDefaultContext : QueueUserWorkItemCallbackBase
    {
        private WaitCallback _callback;
        private readonly object _state;

        internal static readonly ContextCallback s_executionContextShim = state =>
        {
            var obj = (QueueUserWorkItemCallbackDefaultContext)state;
            WaitCallback c = obj._callback;
            Debug.Assert(c != null);
            obj._callback = null;
            c(obj._state);
        };

        internal QueueUserWorkItemCallbackDefaultContext(WaitCallback callback, object state)
        {
            _callback = callback;
            _state = state;
        }

        public override void ExecuteWorkItem()
        {
            base.ExecuteWorkItem();
            try
            {
                ExecutionContext.Run(ExecutionContext.Default, s_executionContextShim, this);
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
                throw; //unreachable
            }
        }
    }

    internal sealed class QueueUserWorkItemCallbackDefaultContext<TState> : QueueUserWorkItemCallbackBase
    {
        private Action<TState> _callback;
        private readonly TState _state;

        internal static readonly ContextCallback s_executionContextShim = state =>
        {
            var obj = (QueueUserWorkItemCallbackDefaultContext<TState>)state;
            Action<TState> c = obj._callback;
            Debug.Assert(c != null);
            obj._callback = null;
            c(obj._state);
        };

        internal QueueUserWorkItemCallbackDefaultContext(Action<TState> callback, TState state)
        {
            _callback = callback;
            _state = state;
        }

        public override void ExecuteWorkItem()
        {
            base.ExecuteWorkItem();
            try
            {
                ExecutionContext.Run(ExecutionContext.Default, s_executionContextShim, this);
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
                throw; //unreachable
            }
        }
    }

    internal class _ThreadPoolWaitOrTimerCallback
    {
        private WaitOrTimerCallback _waitOrTimerCallback;
        private ExecutionContext _executionContext;
        private object _state;
        private static readonly ContextCallback _ccbt = new ContextCallback(WaitOrTimerCallback_Context_t);
        private static readonly ContextCallback _ccbf = new ContextCallback(WaitOrTimerCallback_Context_f);

        internal _ThreadPoolWaitOrTimerCallback(WaitOrTimerCallback waitOrTimerCallback, object state, bool flowExecutionContext)
        {
            _waitOrTimerCallback = waitOrTimerCallback;
            _state = state;

            if (flowExecutionContext)
            {
                // capture the exection context
                _executionContext = ExecutionContext.Capture();
            }
        }

        private static void WaitOrTimerCallback_Context_t(object state) =>
            WaitOrTimerCallback_Context(state, timedOut: true);

        private static void WaitOrTimerCallback_Context_f(object state) =>
            WaitOrTimerCallback_Context(state, timedOut: false);

        private static void WaitOrTimerCallback_Context(object state, bool timedOut)
        {
            _ThreadPoolWaitOrTimerCallback helper = (_ThreadPoolWaitOrTimerCallback)state;
            helper._waitOrTimerCallback(helper._state, timedOut);
        }

        // call back helper
        internal static void PerformWaitOrTimerCallback(_ThreadPoolWaitOrTimerCallback helper, bool timedOut)
        {
            Debug.Assert(helper != null, "Null state passed to PerformWaitOrTimerCallback!");
            // call directly if it is an unsafe call OR EC flow is suppressed
            if (helper._executionContext == null)
            {
                WaitOrTimerCallback callback = helper._waitOrTimerCallback;
                callback(helper._state, timedOut);
            }
            else
            {
                ExecutionContext.Run(helper._executionContext, timedOut ? _ccbt : _ccbf, helper);
            }
        }
    }

    public static partial class ThreadPool
    {
        [CLSCompliant(false)]
        public static RegisteredWaitHandle RegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval > (uint)int.MaxValue && millisecondsTimeOutInterval != uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            return RegisterWaitForSingleObject(waitObject, callBack, state, millisecondsTimeOutInterval, executeOnlyOnce, true);
        }

        [CLSCompliant(false)]
        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval > (uint)int.MaxValue && millisecondsTimeOutInterval != uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            return RegisterWaitForSingleObject(waitObject, callBack, state, millisecondsTimeOutInterval, executeOnlyOnce, false);
        }

        public static RegisteredWaitHandle RegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             object state,
             int millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (uint)millisecondsTimeOutInterval, executeOnlyOnce, true);
        }

        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             object state,
             int millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (uint)millisecondsTimeOutInterval, executeOnlyOnce, false);
        }

        public static RegisteredWaitHandle RegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            object state,
            long millisecondsTimeOutInterval,
            bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1 || millisecondsTimeOutInterval > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (uint)millisecondsTimeOutInterval, executeOnlyOnce, true);
        }

        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            object state,
            long millisecondsTimeOutInterval,
            bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1 || millisecondsTimeOutInterval > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (uint)millisecondsTimeOutInterval, executeOnlyOnce, false);
        }

        public static RegisteredWaitHandle RegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            object state,
            TimeSpan timeout,
            bool executeOnlyOnce)
        {
            int tm = WaitHandle.ToTimeoutMilliseconds(timeout);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (uint)tm, executeOnlyOnce, true);
        }

        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            object state,
            TimeSpan timeout,
            bool executeOnlyOnce)
        {
            int tm = WaitHandle.ToTimeoutMilliseconds(timeout);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (uint)tm, executeOnlyOnce, false);
        }

        public static bool QueueUserWorkItem(WaitCallback callBack) =>
            QueueUserWorkItem(callBack, null);

        public static bool QueueUserWorkItem(WaitCallback callBack, object state)
        {
            if (callBack == null)
            {
                throw new ArgumentNullException(nameof(callBack));
            }

            ExecutionContext context = ExecutionContext.Capture();

            IThreadPoolWorkItem tpcallBack = context == ExecutionContext.Default ?
                new QueueUserWorkItemCallbackDefaultContext(callBack, state) :
                (IThreadPoolWorkItem)new QueueUserWorkItemCallback(callBack, state, context);

            ThreadPoolGlobals.workQueue.Enqueue(tpcallBack, forceGlobal: true);

            return true;
        }

        public static bool QueueUserWorkItem<TState>(Action<TState> callBack, TState state, bool preferLocal)
        {
            if (callBack == null)
            {
                throw new ArgumentNullException(nameof(callBack));
            }

            ExecutionContext context = ExecutionContext.Capture();

            IThreadPoolWorkItem tpcallBack = context == ExecutionContext.Default ?
                new QueueUserWorkItemCallbackDefaultContext<TState>(callBack, state) :
                (IThreadPoolWorkItem)new QueueUserWorkItemCallback<TState>(callBack, state, context);

            ThreadPoolGlobals.workQueue.Enqueue(tpcallBack, forceGlobal: !preferLocal);

            return true;
        }

        public static bool UnsafeQueueUserWorkItem(WaitCallback callBack, object state)
        {
            if (callBack == null)
            {
                throw new ArgumentNullException(nameof(callBack));
            }

            IThreadPoolWorkItem tpcallBack = new QueueUserWorkItemCallback(callBack, state, null);

            ThreadPoolGlobals.workQueue.Enqueue(tpcallBack, forceGlobal: true);

            return true;
        }

        internal static void UnsafeQueueCustomWorkItem(IThreadPoolWorkItem workItem, bool forceGlobal)
        {
            Debug.Assert(null != workItem);
            ThreadPoolGlobals.workQueue.Enqueue(workItem, forceGlobal);
        }

        // This method tries to take the target callback out of the current thread's queue.
        internal static bool TryPopCustomWorkItem(IThreadPoolWorkItem workItem)
        {
            Debug.Assert(null != workItem);
            return ThreadPoolGlobals.workQueue.LocalFindAndPop(workItem);
        }

        // Get all workitems.  Called by TaskScheduler in its debugger hooks.
        internal static IEnumerable<IThreadPoolWorkItem> GetQueuedWorkItems()
        {
            // Enumerate the global queue
            foreach (IThreadPoolWorkItem workItem in ThreadPoolGlobals.workQueue.workItems)
            {
                yield return workItem;
            }

            // Enumerate each local queue
            foreach (ThreadPoolWorkQueue.WorkStealingQueue wsq in ThreadPoolWorkQueue.WorkStealingQueueList.Queues)
            {
                if (wsq != null && wsq.m_array != null)
                {
                    IThreadPoolWorkItem[] items = wsq.m_array;
                    for (int i = 0; i < items.Length; i++)
                    {
                        IThreadPoolWorkItem item = items[i];
                        if (item != null)
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        internal static IEnumerable<IThreadPoolWorkItem> GetLocallyQueuedWorkItems()
        {
            ThreadPoolWorkQueue.WorkStealingQueue wsq = ThreadPoolWorkQueueThreadLocals.threadLocals.workStealingQueue;
            if (wsq != null && wsq.m_array != null)
            {
                IThreadPoolWorkItem[] items = wsq.m_array;
                for (int i = 0; i < items.Length; i++)
                {
                    IThreadPoolWorkItem item = items[i];
                    if (item != null)
                        yield return item;
                }
            }
        }

        internal static IEnumerable<IThreadPoolWorkItem> GetGloballyQueuedWorkItems() => ThreadPoolGlobals.workQueue.workItems;

        private static object[] ToObjectArray(IEnumerable<IThreadPoolWorkItem> workitems)
        {
            int i = 0;
            foreach (IThreadPoolWorkItem item in workitems)
            {
                i++;
            }

            object[] result = new object[i];
            i = 0;
            foreach (IThreadPoolWorkItem item in workitems)
            {
                if (i < result.Length) //just in case someone calls us while the queues are in motion
                    result[i] = item;
                i++;
            }

            return result;
        }

        // This is the method the debugger will actually call, if it ends up calling
        // into ThreadPool directly.  Tests can use this to simulate a debugger, as well.
        internal static object[] GetQueuedWorkItemsForDebugger() =>
            ToObjectArray(GetQueuedWorkItems());

        internal static object[] GetGloballyQueuedWorkItemsForDebugger() =>
            ToObjectArray(GetGloballyQueuedWorkItems());

        internal static object[] GetLocallyQueuedWorkItemsForDebugger() =>
            ToObjectArray(GetLocallyQueuedWorkItems());

        unsafe private static void NativeOverlappedCallback(object obj)
        {
            NativeOverlapped* overlapped = (NativeOverlapped*)(IntPtr)obj;
            _IOCompletionCallback.PerformIOCompletionCallback(0, 0, overlapped);
        }

        [CLSCompliant(false)]
        unsafe public static bool UnsafeQueueNativeOverlapped(NativeOverlapped* overlapped)
        {
            // OS doesn't signal handle, so do it here (CoreCLR does this assignment in ThreadPoolNative::CorPostQueuedCompletionStatus)
            overlapped->InternalLow = (IntPtr)0;
            // Both types of callbacks are executed on the same thread pool
            return UnsafeQueueUserWorkItem(NativeOverlappedCallback, (IntPtr)overlapped);
        }

        [Obsolete("ThreadPool.BindHandle(IntPtr) has been deprecated.  Please use ThreadPool.BindHandle(SafeHandle) instead.", false)]
        public static bool BindHandle(IntPtr osHandle)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported); // Replaced by ThreadPoolBoundHandle.BindHandle
        }

        public static bool BindHandle(SafeHandle osHandle)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported); // Replaced by ThreadPoolBoundHandle.BindHandle
        }

        internal static bool IsThreadPoolThread { get { return ThreadPoolWorkQueueThreadLocals.threadLocals != null; } }
    }
}
