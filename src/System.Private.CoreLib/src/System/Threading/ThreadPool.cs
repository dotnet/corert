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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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

                    WorkStealingQueue[] newQueues = new WorkStealingQueue[oldQueues.Length - 1];
                    if (pos == 0)
                    {
                        Array.Copy(oldQueues, 1, newQueues, 0, newQueues.Length);
                    }
                    else if (pos == oldQueues.Length - 1)
                    {
                        Array.Copy(oldQueues, 0, newQueues, 0, pos);
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

        internal class WorkStealingQueue
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

            private SpinLock m_foreignLock = new SpinLock(false);

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
                            m_foreignLock.Exit(true);
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
                            IThreadPoolWorkItem[] newArray = new IThreadPoolWorkItem[m_array.Length << 1];
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
                            m_foreignLock.Exit(false);
                    }
                }
            }

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
                        // @TODO: optimize away the lock?
                        bool lockTaken = false;
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);

                            // If we lost the race, bail.
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
                                m_foreignLock.Exit(false);
                        }
                    }
                }

                return false;
            }

            public IThreadPoolWorkItem LocalPop() => m_headIndex < m_tailIndex ? LocalPopCore() : null;

            public IThreadPoolWorkItem LocalPopCore()
            {
                while (true)
                {
                    // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                    int tail = m_tailIndex;
                    if (m_headIndex >= tail)
                    {
                        return null;
                    }

                    // Decrement the tail using a fence to ensure a subsequent read doesn't come before.
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
                                // We lost the race, element was stolen, restore the tail.
                                m_tailIndex = tail + 1;
                                return null;
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                                m_foreignLock.Exit(false);
                        }
                    }
                }
            }

            public bool CanSteal => m_headIndex < m_tailIndex;

            public IThreadPoolWorkItem TrySteal(ref bool missedSteal)
            {
                return TrySteal(ref missedSteal, 0); // no blocking by default.
            }

            private IThreadPoolWorkItem TrySteal(ref bool missedSteal, int millisecondsTimeout)
            {
                while (true)
                {
                    if (CanSteal)
                    {
                        bool taken = false;
                        try
                        {
                            m_foreignLock.TryEnter(millisecondsTimeout, ref taken);
                            if (taken)
                            {
                                int head = m_headIndex;
                                Interlocked.Exchange(ref m_headIndex, head + 1);
                                if (head < m_tailIndex)
                                {
                                    int idx = head & m_mask;
                                    IThreadPoolWorkItem obj = Volatile.Read(ref m_array[idx]);

                                    if (obj == null)
                                    {
                                        continue;
                                    }

                                    m_array[idx] = null;
                                    return obj;
                                }
                                else
                                {
                                    m_headIndex = head;
                                }
                            }
                        }
                        finally
                        {
                            if (taken)
                            {
                                m_foreignLock.Exit(useMemoryBarrier: false);
                            }
                        }

                        missedSteal = false;
                    }
                    return null;
                }
            }
        }

        internal class QueueSegment
        {
            // Holds a segment of the queue.  Enqueues/Dequeues start at element 0, and work their way up.
            internal readonly IThreadPoolWorkItem[] nodes;
            private const int QueueSegmentLength = 256;

            // Holds the indexes of the lowest and highest valid elements of the nodes array.
            // The low index is in the lower 16 bits, high index is in the upper 16 bits.
            // Use GetIndexes and CompareExchangeIndexes to manipulate this.
            private volatile int indexes;

            // The next segment in the queue.
            public volatile QueueSegment Next;


            private const int SixteenBits = 0xffff;

            private void GetIndexes(out int upper, out int lower)
            {
                int i = indexes;
                upper = (i >> 16) & SixteenBits;
                lower = i & SixteenBits;

                Debug.Assert(upper >= lower);
                Debug.Assert(upper <= nodes.Length);
                Debug.Assert(lower <= nodes.Length);
                Debug.Assert(upper >= 0);
                Debug.Assert(lower >= 0);
            }

            private bool CompareExchangeIndexes(ref int prevUpper, int newUpper, ref int prevLower, int newLower)
            {
                Debug.Assert(newUpper >= newLower);
                Debug.Assert(newUpper <= nodes.Length);
                Debug.Assert(newLower <= nodes.Length);
                Debug.Assert(newUpper >= 0);
                Debug.Assert(newLower >= 0);
                Debug.Assert(newUpper >= prevUpper);
                Debug.Assert(newLower >= prevLower);
                Debug.Assert(newUpper == prevUpper ^ newLower == prevLower);

                int oldIndexes = (prevUpper << 16) | (prevLower & SixteenBits);
                int newIndexes = (newUpper << 16) | (newLower & SixteenBits);
                int prevIndexes = Interlocked.CompareExchange(ref indexes, newIndexes, oldIndexes);
                prevUpper = (prevIndexes >> 16) & SixteenBits;
                prevLower = prevIndexes & SixteenBits;
                return prevIndexes == oldIndexes;
            }

            public QueueSegment()
            {
                Debug.Assert(QueueSegmentLength <= SixteenBits);
                nodes = new IThreadPoolWorkItem[QueueSegmentLength];
            }


            public bool IsUsedUp()
            {
                int upper, lower;
                GetIndexes(out upper, out lower);
                return (upper == nodes.Length) &&
                       (lower == nodes.Length);
            }

            public bool TryEnqueue(IThreadPoolWorkItem node)
            {
                //
                // If there's room in this segment, atomically increment the upper count (to reserve
                // space for this node), then store the node.
                // Note that this leaves a window where it will look like there is data in that
                // array slot, but it hasn't been written yet.  This is taken care of in TryDequeue
                // with a busy-wait loop, waiting for the element to become non-null.  This implies
                // that we can never store null nodes in this data structure.
                //
                Debug.Assert(null != node);

                int upper, lower;
                GetIndexes(out upper, out lower);

                while (true)
                {
                    if (upper == nodes.Length)
                        return false;

                    if (CompareExchangeIndexes(ref upper, upper + 1, ref lower, lower))
                    {
                        Debug.Assert(Volatile.Read(ref nodes[upper]) == null);
                        Volatile.Write(ref nodes[upper], node);
                        return true;
                    }
                }
            }

            public bool TryDequeue(out IThreadPoolWorkItem node)
            {
                //
                // If there are nodes in this segment, increment the lower count, then take the
                // element we find there.
                //
                int upper, lower;
                GetIndexes(out upper, out lower);

                while (true)
                {
                    if (lower == upper)
                    {
                        node = null;
                        return false;
                    }

                    if (CompareExchangeIndexes(ref upper, upper, ref lower, lower + 1))
                    {
                        // It's possible that a concurrent call to Enqueue hasn't yet
                        // written the node reference to the array.  We need to spin until
                        // it shows up.
                        SpinWait spinner = new SpinWait();
                        while ((node = Volatile.Read(ref nodes[lower])) == null)
                            spinner.SpinOnce();

                        // Null-out the reference so the object can be GC'd earlier.
                        nodes[lower] = null;

                        return true;
                    }
                }
            }
        }

        // The head and tail of the queue.  We enqueue to the head, and dequeue from the tail.
        internal volatile QueueSegment queueHead;
        internal volatile QueueSegment queueTail;
        
        private volatile int numOutstandingThreadRequests = 0;

        // The number of threads executing work items in the Dispatch method
        internal volatile int numWorkingThreads;

        public ThreadPoolWorkQueue()
        {
            queueTail = queueHead = new QueueSegment();
        }

        public ThreadPoolWorkQueueThreadLocals EnsureCurrentThreadHasQueue()
        {
            if (null == ThreadPoolWorkQueueThreadLocals.Current)
                ThreadPoolWorkQueueThreadLocals.Current = new ThreadPoolWorkQueueThreadLocals(this);
            return ThreadPoolWorkQueueThreadLocals.Current;
        }

        internal void EnsureThreadRequested()
        {
            //
            // If we have not yet requested #procs threads from the VM, then request a new thread.
            //
            int count = numOutstandingThreadRequests;
            while (count < ThreadPoolGlobals.processorCount)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count + 1, count);
                if (prev == count)
                {
                    ThreadPool.QueueDispatch();
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
                tl = ThreadPoolWorkQueueThreadLocals.Current;

            if (null != tl)
            {
                tl.workStealingQueue.LocalPush(callback);
            }
            else
            {
                QueueSegment head = queueHead;

                while (!head.TryEnqueue(callback))
                {
                    Interlocked.CompareExchange(ref head.Next, new QueueSegment(), null);

                    while (head.Next != null)
                    {
                        Interlocked.CompareExchange(ref queueHead, head.Next, head);
                        head = queueHead;
                    }
                }
            }

            EnsureThreadRequested();
        }

        internal bool LocalFindAndPop(IThreadPoolWorkItem callback)
        {
            ThreadPoolWorkQueueThreadLocals tl = ThreadPoolWorkQueueThreadLocals.Current;
            if (null == tl)
                return false;

            return tl.workStealingQueue.LocalFindAndPop(callback);
        }

        public void Dequeue(ThreadPoolWorkQueueThreadLocals tl, out IThreadPoolWorkItem callback, out bool missedSteal)
        {
            missedSteal = false;
            WorkStealingQueue localWsq = tl.workStealingQueue;
            callback = localWsq.LocalPop();

            if (null == callback)
            {
                QueueSegment tail = queueTail;
                while (true)
                {
                    if (tail.TryDequeue(out callback))
                    {
                        Debug.Assert(null != callback);
                        break;
                    }

                    if (null == tail.Next || !tail.IsUsedUp())
                    {
                        break;
                    }
                    else
                    {
                        Interlocked.CompareExchange(ref queueTail, tail.Next, tail);
                        tail = queueTail;
                    }
                }
            }

            if (null == callback)
            {
                WorkStealingQueue[] otherQueues = WorkStealingQueueList.Queues;
                int i = tl.random.Next(otherQueues.Length);
                int c = otherQueues.Length;
                Debug.Assert(c > 0, "There must be at leats one queue for this thread.");
                while (c > 0)
                {
                    WorkStealingQueue otherQueue = Volatile.Read(ref otherQueues[i % otherQueues.Length]);
                    if (otherQueue != null &&
                        otherQueue != localWsq &&
                        otherQueue.CanSteal)
                    {
                        callback = otherQueue.TrySteal(ref missedSteal);
                        if (callback != null)
                            break;
                    }
                    i++;
                    c--;
                }
            }
        }
        
        /// <summary>
        /// Dipatches work items to this thread.
        /// </summary>
        /// <returns><c>true</c> if this thread did as much work as was available. <c>false</c> if this thread stopped working early.</returns>
        internal static bool Dispatch()
        {
            var workQueue = ThreadPoolGlobals.workQueue;

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
            try
            {
                //
                // Set up our thread-local data
                //
                ThreadPoolWorkQueueThreadLocals tl = workQueue.EnsureCurrentThreadHasQueue();

                //
                // Loop until there is no work.
                //
                while (true)
                {
                    workQueue.Dequeue(tl, out IThreadPoolWorkItem workItem, out bool missedSteal);

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
                    {
                        return false;
                    }
                }
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

    // Holds a WorkStealingQueue, and remmoves it from the list when this object is no longer referened.
    internal sealed class ThreadPoolWorkQueueThreadLocals
    {
        [ThreadStatic]
        public static ThreadPoolWorkQueueThreadLocals Current;


        public readonly ThreadPoolWorkQueue workQueue;
        public readonly ThreadPoolWorkQueue.WorkStealingQueue workStealingQueue;
        public readonly Random random = new Random(Environment.CurrentManagedThreadId);

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
                    bool done = false;
                    while (!done)
                    {
                        IThreadPoolWorkItem cb = null;
                        if ((cb = workStealingQueue.LocalPop()) != null)
                        {
                            workQueue.Enqueue(cb, true);
                        }
                        else
                        {
                            done = true;
                        }
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

    public delegate void WaitCallback(Object state);

    public delegate void WaitOrTimerCallback(Object state, bool timedOut);  // signalled or timed out

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

    internal sealed class QueueUserWorkItemCallback : IThreadPoolWorkItem
    {
        private WaitCallback callback;
        private ExecutionContext context;
        private Object state;

#if DEBUG
        private volatile int executed;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1821:RemoveEmptyFinalizers")]
        ~QueueUserWorkItemCallback()
        {
            Debug.Assert(
                executed != 0 || Environment.HasShutdownStarted /*|| AppDomain.CurrentDomain.IsFinalizingForUnload()*/,
                "A QueueUserWorkItemCallback was never called!");
        }

        private void MarkExecuted()
        {
            GC.SuppressFinalize(this);
            Debug.Assert(
                0 == Interlocked.Exchange(ref executed, 1),
                "A QueueUserWorkItemCallback was called twice!");
        }
#endif

        internal QueueUserWorkItemCallback(WaitCallback waitCallback, Object stateObj, ExecutionContext ec)
        {
            callback = waitCallback;
            state = stateObj;
            context = ec;
        }

        void IThreadPoolWorkItem.ExecuteWorkItem()
        {
#if DEBUG
            MarkExecuted();
#endif
            try
            {
                if (context == null)
                    callback(state);
                else
                    ExecutionContext.Run(context, ccb, this);
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
                throw; //unreachable
            }
        }

        internal static ContextCallback ccb = new ContextCallback(WaitCallback_Context);

        private static void WaitCallback_Context(Object state)
        {
            QueueUserWorkItemCallback obj = (QueueUserWorkItemCallback)state;
            WaitCallback wc = obj.callback as WaitCallback;
            Debug.Assert(null != wc);
            wc(obj.state);
        }
    }


    internal sealed class QueueUserWorkItemCallbackDefaultContext : IThreadPoolWorkItem
    {
        private WaitCallback callback;
        private Object state;

#if DEBUG
        private volatile int executed;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1821:RemoveEmptyFinalizers")]
        ~QueueUserWorkItemCallbackDefaultContext()
        {
            Debug.Assert(
                executed != 0 || Environment.HasShutdownStarted /*|| AppDomain.CurrentDomain.IsFinalizingForUnload()*/,
                "A QueueUserWorkItemCallbackDefaultContext was never called!");
        }

        private void MarkExecuted()
        {
            GC.SuppressFinalize(this);
            Debug.Assert(
                0 == Interlocked.Exchange(ref executed, 1),
                "A QueueUserWorkItemCallbackDefaultContext was called twice!");
        }
#endif

        internal QueueUserWorkItemCallbackDefaultContext(WaitCallback waitCallback, Object stateObj)
        {
            callback = waitCallback;
            state = stateObj;
        }

        void IThreadPoolWorkItem.ExecuteWorkItem()
        {
#if DEBUG
            MarkExecuted();
#endif
            try
            {
                ExecutionContext.Run(ExecutionContext.Default, ccb, this);
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
                throw; //unreachable
            }
        }

        internal static ContextCallback ccb = new ContextCallback(WaitCallback_Context);

        private static void WaitCallback_Context(Object state)
        {
            QueueUserWorkItemCallbackDefaultContext obj = (QueueUserWorkItemCallbackDefaultContext)state;
            WaitCallback wc = obj.callback as WaitCallback;
            Debug.Assert(null != wc);
            wc(obj.state);
        }
    }

    internal class _ThreadPoolWaitOrTimerCallback
    {
        private static readonly ContextCallback _ccbt = new ContextCallback(WaitOrTimerCallback_Context_t);
        private static readonly ContextCallback _ccbf = new ContextCallback(WaitOrTimerCallback_Context_f);

        private readonly WaitOrTimerCallback _waitOrTimerCallback;
        private readonly ExecutionContext _executionContext;
        private readonly Object _state;

        internal _ThreadPoolWaitOrTimerCallback(WaitOrTimerCallback waitOrTimerCallback, Object state, bool flowExecutionContext)
        {
            _waitOrTimerCallback = waitOrTimerCallback;
            _state = state;

            if (flowExecutionContext)
            {
                _executionContext = ExecutionContext.Capture();
            }
        }

        private static void WaitOrTimerCallback_Context_t(Object state) =>
            WaitOrTimerCallback_Context(state, timedOut: true);

        private static void WaitOrTimerCallback_Context_f(Object state) =>
            WaitOrTimerCallback_Context(state, timedOut: false);

        private static void WaitOrTimerCallback_Context(Object state, bool timedOut)
        {
            _ThreadPoolWaitOrTimerCallback helper = (_ThreadPoolWaitOrTimerCallback)state;
            helper._waitOrTimerCallback(helper._state, timedOut);
        }

        // call back helper
        internal static void PerformWaitOrTimerCallback(Object state, bool timedOut)
        {
            _ThreadPoolWaitOrTimerCallback helper = (_ThreadPoolWaitOrTimerCallback)state;
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
             Object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            return RegisterWaitForSingleObject(waitObject, callBack, state, millisecondsTimeOutInterval, executeOnlyOnce, true);
        }

        [CLSCompliant(false)]
        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             Object state,
             uint millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            return RegisterWaitForSingleObject(waitObject, callBack, state, millisecondsTimeOutInterval, executeOnlyOnce, false);
        }

        public static RegisteredWaitHandle RegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             Object state,
             int millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            Contract.EndContractBlock();
            return RegisterWaitForSingleObject(waitObject, callBack, state, (UInt32)millisecondsTimeOutInterval, executeOnlyOnce, true);
        }

        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
             WaitHandle waitObject,
             WaitOrTimerCallback callBack,
             Object state,
             int millisecondsTimeOutInterval,
             bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            Contract.EndContractBlock();
            return RegisterWaitForSingleObject(waitObject, callBack, state, (UInt32)millisecondsTimeOutInterval, executeOnlyOnce, false);
        }

        public static RegisteredWaitHandle RegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            Object state,
            long millisecondsTimeOutInterval,
            bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            Contract.EndContractBlock();
            return RegisterWaitForSingleObject(waitObject, callBack, state, (UInt32)millisecondsTimeOutInterval, executeOnlyOnce, true);
        }

        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            Object state,
            long millisecondsTimeOutInterval,
            bool executeOnlyOnce)
        {
            if (millisecondsTimeOutInterval < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeOutInterval), SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            Contract.EndContractBlock();
            return RegisterWaitForSingleObject(waitObject, callBack, state, (UInt32)millisecondsTimeOutInterval, executeOnlyOnce, false);
        }

        public static RegisteredWaitHandle RegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            Object state,
            TimeSpan timeout,
            bool executeOnlyOnce)
        {
            int tm = WaitHandle.ToTimeoutMilliseconds(timeout);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (UInt32)tm, executeOnlyOnce, true);
        }

        public static RegisteredWaitHandle UnsafeRegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            Object state,
            TimeSpan timeout,
            bool executeOnlyOnce)
        {
            int tm = WaitHandle.ToTimeoutMilliseconds(timeout);
            return RegisterWaitForSingleObject(waitObject, callBack, state, (UInt32)tm, executeOnlyOnce, false);
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

        public static bool UnsafeQueueUserWorkItem(WaitCallback callBack, Object state)
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
            return ThreadPoolGlobals.workQueue.LocalFindAndPop(workItem);
        }

        // Get all workitems.  Called by TaskScheduler in its debugger hooks.
        internal static IEnumerable<IThreadPoolWorkItem> GetQueuedWorkItems()
        {
            return EnumerateQueuedWorkItems(ThreadPoolWorkQueue.WorkStealingQueueList.Queues, ThreadPoolGlobals.workQueue.queueTail);
        }

        internal static IEnumerable<IThreadPoolWorkItem> EnumerateQueuedWorkItems(ThreadPoolWorkQueue.WorkStealingQueue[] wsQueues, ThreadPoolWorkQueue.QueueSegment globalQueueTail)
        {
            if (wsQueues != null)
            {
                // First, enumerate all workitems in thread-local queues.
                foreach (ThreadPoolWorkQueue.WorkStealingQueue wsq in wsQueues)
                {
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
            }

            if (globalQueueTail != null)
            {
                // Now the global queue
                for (ThreadPoolWorkQueue.QueueSegment segment = globalQueueTail;
                    segment != null;
                    segment = segment.Next)
                {
                    IThreadPoolWorkItem[] items = segment.nodes;
                    for (int i = 0; i < items.Length; i++)
                    {
                        IThreadPoolWorkItem item = items[i];
                        if (item != null)
                            yield return item;
                    }
                }
            }
        }

        internal static IEnumerable<IThreadPoolWorkItem> GetLocallyQueuedWorkItems()
        {
            return EnumerateQueuedWorkItems(new ThreadPoolWorkQueue.WorkStealingQueue[] { ThreadPoolWorkQueueThreadLocals.Current.workStealingQueue }, null);
        }

        internal static IEnumerable<IThreadPoolWorkItem> GetGloballyQueuedWorkItems()
        {
            return EnumerateQueuedWorkItems(null, ThreadPoolGlobals.workQueue.queueTail);
        }

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

        internal static bool IsThreadPoolThread { get { return ThreadPoolWorkQueueThreadLocals.Current != null; } }
    }
}
