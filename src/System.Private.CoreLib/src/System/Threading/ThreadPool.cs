// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

/*=============================================================================
**
** Class: ThreadPool
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Internal.Runtime.Augments;

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
        // Simple sparsely populated array to allow lock-free reading.
        internal class SparseArray<T> where T : class
        {
            private volatile T[] m_array;
            private readonly Lock m_lock = new Lock();

            internal SparseArray(int initialSize)
            {
                m_array = new T[initialSize];
            }

            internal T[] Current
            {
                get { return m_array; }
            }

            internal int Add(T e)
            {
                while (true)
                {
                    T[] array = m_array;
                    using (LockHolder.Hold(m_lock))
                    {
                        for (int i = 0; i < array.Length; i++)
                        {
                            if (array[i] == null)
                            {
                                Volatile.Write(ref array[i], e);
                                return i;
                            }
                            else if (i == array.Length - 1)
                            {
                                // Must resize. If we raced and lost, we start over again.
                                if (array != m_array)
                                    continue;

                                T[] newArray = new T[array.Length * 2];
                                Array.Copy(array, newArray, i + 1);
                                newArray[i + 1] = e;
                                m_array = newArray;
                                return i + 1;
                            }
                        }
                    }
                }
            }

            internal void Remove(T e)
            {
                T[] array = m_array;
                using (LockHolder.Hold(m_lock))
                {
                    for (int i = 0; i < m_array.Length; i++)
                    {
                        if (m_array[i] == e)
                        {
                            Volatile.Write(ref m_array[i], null);
                            break;
                        }
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
                            Contract.Assert(m_headIndex <= m_tailIndex);
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
                    IThreadPoolWorkItem unused;
                    if (LocalPop(out unused))
                    {
                        Contract.Assert(unused == obj);
                        return true;
                    }
                    return false;
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

            public bool LocalPop(out IThreadPoolWorkItem obj)
            {
                while (true)
                {
                    // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                    int tail = m_tailIndex;
                    if (m_headIndex >= tail)
                    {
                        obj = null;
                        return false;
                    }

                    tail -= 1;
                    Interlocked.Exchange(ref m_tailIndex, tail);

                    // If there is no interaction with a take, we can head down the fast path.
                    if (m_headIndex <= tail)
                    {
                        int idx = tail & m_mask;
                        obj = Volatile.Read(ref m_array[idx]);

                        // Check for nulls in the array.
                        if (obj == null) continue;

                        m_array[idx] = null;
                        return true;
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
                                obj = Volatile.Read(ref m_array[idx]);

                                // Check for nulls in the array.
                                if (obj == null) continue;

                                m_array[idx] = null;
                                return true;
                            }
                            else
                            {
                                // We lost the race, element was stolen, restore the tail.
                                m_tailIndex = tail + 1;
                                obj = null;
                                return false;
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

            public bool TrySteal(out IThreadPoolWorkItem obj, ref bool missedSteal)
            {
                return TrySteal(out obj, ref missedSteal, 0); // no blocking by default.
            }

            private bool TrySteal(out IThreadPoolWorkItem obj, ref bool missedSteal, int millisecondsTimeout)
            {
                obj = null;

                while (true)
                {
                    if (m_headIndex >= m_tailIndex)
                        return false;

                    bool taken = false;
                    try
                    {
                        m_foreignLock.TryEnter(millisecondsTimeout, ref taken);
                        if (taken)
                        {
                            // Increment head, and ensure read of tail doesn't move before it (fence).
                            int head = m_headIndex;
                            Interlocked.Exchange(ref m_headIndex, head + 1);

                            if (head < m_tailIndex)
                            {
                                int idx = head & m_mask;
                                obj = Volatile.Read(ref m_array[idx]);

                                // Check for nulls in the array.
                                if (obj == null) continue;

                                m_array[idx] = null;
                                return true;
                            }
                            else
                            {
                                // Failed, restore head.
                                m_headIndex = head;
                                obj = null;
                                missedSteal = true;
                            }
                        }
                        else
                        {
                            missedSteal = true;
                        }
                    }
                    finally
                    {
                        if (taken)
                            m_foreignLock.Exit(false);
                    }

                    return false;
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


            const int SixteenBits = 0xffff;

            void GetIndexes(out int upper, out int lower)
            {
                int i = indexes;
                upper = (i >> 16) & SixteenBits;
                lower = i & SixteenBits;

                Contract.Assert(upper >= lower);
                Contract.Assert(upper <= nodes.Length);
                Contract.Assert(lower <= nodes.Length);
                Contract.Assert(upper >= 0);
                Contract.Assert(lower >= 0);
            }

            bool CompareExchangeIndexes(ref int prevUpper, int newUpper, ref int prevLower, int newLower)
            {
                Contract.Assert(newUpper >= newLower);
                Contract.Assert(newUpper <= nodes.Length);
                Contract.Assert(newLower <= nodes.Length);
                Contract.Assert(newUpper >= 0);
                Contract.Assert(newLower >= 0);
                Contract.Assert(newUpper >= prevUpper);
                Contract.Assert(newLower >= prevLower);
                Contract.Assert(newUpper == prevUpper ^ newLower == prevLower);

                int oldIndexes = (prevUpper << 16) | (prevLower & SixteenBits);
                int newIndexes = (newUpper << 16) | (newLower & SixteenBits);
                int prevIndexes = Interlocked.CompareExchange(ref indexes, newIndexes, oldIndexes);
                prevUpper = (prevIndexes >> 16) & SixteenBits;
                prevLower = prevIndexes & SixteenBits;
                return prevIndexes == oldIndexes;
            }

            public QueueSegment()
            {
                Contract.Assert(QueueSegmentLength <= SixteenBits);
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
                Contract.Assert(null != node);

                int upper, lower;
                GetIndexes(out upper, out lower);

                while (true)
                {
                    if (upper == nodes.Length)
                        return false;

                    if (CompareExchangeIndexes(ref upper, upper + 1, ref lower, lower))
                    {
                        Contract.Assert(Volatile.Read(ref nodes[upper]) == null);
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

        internal static SparseArray<WorkStealingQueue> allThreadQueues = new SparseArray<WorkStealingQueue>(16); //TODO: base this on processor count, once the security restrictions are removed from Environment.ProcessorCount

        private volatile int numOutstandingThreadRequests = 0;

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
            // Note that there is a separate count in the VM which will also be incremented in this case, 
            // which is handled by RequestWorkerThread.
            //
            int count = numOutstandingThreadRequests;
            while (count < ThreadPoolGlobals.processorCount)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count + 1, count);
                if (prev == count)
                {
                    NativeThreadPool.QueueDispatch();
                    break;
                }
                count = prev;
            }
        }

        internal void MarkThreadRequestSatisfied()
        {
            //
            // The VM has called us, so one of our outstanding thread requests has been satisfied.
            // Decrement the count so that future calls to EnsureThreadRequested will succeed.
            // Note that there is a separate count in the VM which has already been decremented by the VM
            // by the time we reach this point.
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
            callback = null;
            missedSteal = false;
            WorkStealingQueue wsq = tl.workStealingQueue;

            if (wsq.LocalPop(out callback))
                Contract.Assert(null != callback);

            if (null == callback)
            {
                QueueSegment tail = queueTail;
                while (true)
                {
                    if (tail.TryDequeue(out callback))
                    {
                        Contract.Assert(null != callback);
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
                WorkStealingQueue[] otherQueues = allThreadQueues.Current;
                int i = tl.random.Next(otherQueues.Length);
                int c = otherQueues.Length;
                while (c > 0)
                {
                    WorkStealingQueue otherQueue = Volatile.Read(ref otherQueues[i % otherQueues.Length]);
                    if (otherQueue != null &&
                        otherQueue != wsq &&
                        otherQueue.TrySteal(out callback, ref missedSteal))
                    {
                        Contract.Assert(null != callback);
                        break;
                    }
                    i++;
                    c--;
                }
            }
        }


        //Per-appDomain quantum (in ms) for which the thread keeps processing
        //requests in the current domain.
        const uint tpQuantum = 30U;

        static internal void Dispatch()
        {
            var workQueue = ThreadPoolGlobals.workQueue;

            //
            // The clock is ticking!  We have ThreadPoolGlobals.tpQuantum milliseconds to get some work done, and then
            // we need to return to the VM.
            //
            int quantumStartTime = Environment.TickCount;

            //
            // Update our records to indicate that an outstanding request for a thread has now been fulfilled.
            // From this point on, we are responsible for requesting another thread if we stop working for any
            // reason, and we believe there might still be work in the queue.
            //
            // Note that if this thread is aborted before we get a chance to request another one, the VM will
            // record a thread request on our behalf.  So we don't need to worry about getting aborted right here.
            //
            workQueue.MarkThreadRequestSatisfied();

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
                // Loop until our quantum expires.
                //
                while ((Environment.TickCount - quantumStartTime) < tpQuantum)
                {
                    //
                    // Dequeue and EnsureThreadRequested must be protected from ThreadAbortException.  
                    // These are fast, so this will not delay aborts/AD-unloads for very long.
                    //
                    try { }
                    finally
                    {
                        bool missedSteal = false;
                        workQueue.Dequeue(tl, out workItem, out missedSteal);

                        if (workItem == null)
                        {
                            //
                            // No work.  We're going to return to the VM once we leave this protected region.
                            // If we missed a steal, though, there may be more work in the queue.
                            // Instead of looping around and trying again, we'll just request another thread.  This way
                            // we won't starve other AppDomains while we spin trying to get locks, and hopefully the thread
                            // that owns the contended work-stealing queue will pick up its own workitems in the meantime, 
                            // which will be more efficient than this thread doing it anyway.
                            //
                            needAnotherThread = missedSteal;
                        }
                        else
                        {
                            //
                            // If we found work, there may be more work.  Ask for another thread so that the other work can be processed
                            // in parallel.  Note that this will only ask for a max of #procs threads, so it's safe to call it for every dequeue.
                            //
                            workQueue.EnsureThreadRequested();
                        }
                    }

                    if (workItem == null)
                    {
                        // no more work to do
                        return;
                    }
                    else
                    {
                        //
                        // Execute the workitem outside of any finally blocks, so that it can be aborted if needed.
                        //
                        //RuntimeHelpers.PrepareConstrainedRegions();
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
                    }
                }
            }
            catch (Exception e)
            {
                // Work items should not allow exceptions to escape.  For example, Task catches and stores any exceptions.
                Environment.FailFast("Unhandled exception in ThreadPool dispatch loop", e);
            }
            finally
            {
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
            ThreadPoolWorkQueue.allThreadQueues.Add(workStealingQueue);
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
                        // Ensure that we won't be aborted between LocalPop and Enqueue.
                        try { }
                        finally
                        {
                            IThreadPoolWorkItem cb = null;
                            if (workStealingQueue.LocalPop(out cb))
                            {
                                Contract.Assert(null != cb);
                                workQueue.Enqueue(cb, true);
                            }
                            else
                            {
                                done = true;
                            }
                        }
                    }
                }

                ThreadPoolWorkQueue.allThreadQueues.Remove(workStealingQueue);
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

    [System.Runtime.InteropServices.ComVisible(true)]
    internal delegate void WaitCallback(Object state);

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
        volatile int executed;

        ~QueueUserWorkItemCallback()
        {
            Contract.Assert(
                executed != 0 || Environment.HasShutdownStarted /*|| AppDomain.CurrentDomain.IsFinalizingForUnload()*/,
                "A QueueUserWorkItemCallback was never called!");
        }

        void MarkExecuted()
        {
            GC.SuppressFinalize(this);
            Contract.Assert(
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

        static internal ContextCallback ccb = new ContextCallback(WaitCallback_Context);

        static private void WaitCallback_Context(Object state)
        {
            QueueUserWorkItemCallback obj = (QueueUserWorkItemCallback)state;
            WaitCallback wc = obj.callback as WaitCallback;
            Contract.Assert(null != wc);
            wc(obj.state);
        }
    }


    internal sealed class QueueUserWorkItemCallbackDefaultContext : IThreadPoolWorkItem
    {
        private WaitCallback callback;
        private Object state;

#if DEBUG
        volatile int executed;

        ~QueueUserWorkItemCallbackDefaultContext()
        {
            Contract.Assert(
                executed != 0 || Environment.HasShutdownStarted /*|| AppDomain.CurrentDomain.IsFinalizingForUnload()*/,
                "A QueueUserWorkItemCallbackDefaultContext was never called!");
        }

        void MarkExecuted()
        {
            GC.SuppressFinalize(this);
            Contract.Assert(
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

        static internal ContextCallback ccb = new ContextCallback(WaitCallback_Context);

        static private void WaitCallback_Context(Object state)
        {
            QueueUserWorkItemCallbackDefaultContext obj = (QueueUserWorkItemCallbackDefaultContext)state;
            WaitCallback wc = obj.callback as WaitCallback;
            Contract.Assert(null != wc);
            wc(obj.state);
        }
    }

    internal static class ThreadPool
    {
        public static void QueueUserWorkItem(
             WaitCallback callBack,     // NOTE: we do not expose options that allow the callback to be queued as an APC
             Object state
             )
        {
            Contract.Assert(callBack != null);
            ExecutionContext context = ExecutionContext.Capture();
            IThreadPoolWorkItem tpcallBack = context == ExecutionContext.Default ?
                    new QueueUserWorkItemCallbackDefaultContext(callBack, state) :
                    (IThreadPoolWorkItem)new QueueUserWorkItemCallback(callBack, state, context);
            ThreadPoolGlobals.workQueue.Enqueue(tpcallBack, true);
        }

        public static void QueueUserWorkItem(
             WaitCallback callBack     // NOTE: we do not expose options that allow the callback to be queued as an APC
             )
        {
            QueueUserWorkItem(callBack, null);
        }

        public static void UnsafeQueueUserWorkItem(
             WaitCallback callBack,     // NOTE: we do not expose options that allow the callback to be queued as an APC
             Object state
             )
        {
            Contract.Assert(callBack != null);
            QueueUserWorkItemCallback tpcallBack = new QueueUserWorkItemCallback(callBack, state, null);
            ThreadPoolGlobals.workQueue.Enqueue(tpcallBack, true);
        }

        internal static void UnsafeQueueCustomWorkItem(IThreadPoolWorkItem workItem, bool forceGlobal)
        {
            Contract.Assert(null != workItem);
            ThreadPoolGlobals.workQueue.Enqueue(workItem, forceGlobal);
        }

        internal static void NotifyWorkItemProgress()
        {
        }

        // This method tries to take the target callback out of the current thread's queue.
        internal static bool TryPopCustomWorkItem(IThreadPoolWorkItem workItem)
        {
            return ThreadPoolGlobals.workQueue.LocalFindAndPop(workItem);
        }

        // Get all workitems.  Called by TaskScheduler in its debugger hooks.
        internal static IEnumerable<IThreadPoolWorkItem> GetQueuedWorkItems()
        {
            return EnumerateQueuedWorkItems(ThreadPoolWorkQueue.allThreadQueues.Current, ThreadPoolGlobals.workQueue.queueTail);
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
        internal static object[] GetQueuedWorkItemsForDebugger()
        {
            return ToObjectArray(GetQueuedWorkItems());
        }

        internal static object[] GetGloballyQueuedWorkItemsForDebugger()
        {
            return ToObjectArray(GetGloballyQueuedWorkItems());
        }

        internal static object[] GetLocallyQueuedWorkItemsForDebugger()
        {
            return ToObjectArray(GetLocallyQueuedWorkItems());
        }

        internal static bool IsThreadPoolThread { get { return ThreadPoolWorkQueueThreadLocals.Current != null; } }

        internal static void RegisterWaitForSingleObject(
            WaitHandle waitObject,
            Action<object, bool> callBack,
            Object state,
            int millisecondsTimeOutInterval,
            bool executeOnlyOnce)
        {
            //
            // This is just a quick-and-dirty implementation to make TaskFactory.FromAsync
            // work for the few apps that are using it.  A proper implementation would coalesce
            // multiple waits onto a single thread, so that fewer machine resources would be
            // consumed.
            //
            // Also, we're not returning a RegisteredWaitHandleObject, as in the real public
            // version of this API, simply because the single consumer of this API doesn't
            // need it.
            //

            Contract.Assert(executeOnlyOnce);

            QueueUserWorkItem(_ =>
            {
                bool timedOut = waitObject.WaitOne(millisecondsTimeOutInterval);
                callBack(state, timedOut);
            });
        }
    }

    internal static class NativeThreadPool
    {
        private static volatile bool s_dispatchCallbackSet;

        private static void Dispatch()
        {
            ThreadPoolWorkQueue.Dispatch();
        }

        internal static void QueueDispatch()
        {
            if (!s_dispatchCallbackSet)
            {
                WinRTInterop.Callbacks.SetThreadpoolDispatchCallback(Dispatch);
                s_dispatchCallbackSet = true;
            }
            WinRTInterop.Callbacks.SubmitThreadpoolDispatchCallback();
        }

        internal static void QueueLongRunningWork(Action callback)
        {
            WinRTInterop.Callbacks.SubmitLongRunningThreadpoolWork(callback);
        }
    }
}

