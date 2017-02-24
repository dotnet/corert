// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Exposes features of the Garbage Collector to managed code.
//

using System;
using System.Threading;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Security;
using Internal.Runtime.Augments;

namespace System
{
    // !!!!!!!!!!!!!!!!!!!!!!!
    // Make sure you change the def in rtu\gc.h if you change this!
    public enum GCCollectionMode
    {
        Default = 0,
        Forced = 1,
        Optimized = 2
    }

    public enum GCNotificationStatus
    {
        Succeeded     = 0,
        Failed        = 1,
        Canceled      = 2,
        Timeout       = 3,
        NotApplicable = 4
    }

    internal enum InternalGCCollectionMode
    {
        NonBlocking = 0x00000001,
        Blocking = 0x00000002,
        Optimized = 0x00000004,
        Compacting = 0x00000008,
    }

    internal enum StartNoGCRegionStatus
    {
        Succeeded = 0,
        NotEnoughMemory = 1,
        AmountTooLarge = 2,
        AlreadyInProgress = 3
    }

    internal enum EndNoGCRegionStatus
    {
        Succeeded = 0,
        NotInProgress = 1,
        GCInduced = 2,
        AllocationExceeded = 3
    }

    public static class GC
    {
        public static int GetGeneration(Object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return RuntimeImports.RhGetGeneration(obj);
        }

        /// <summary>
        /// Returns the current generation number of the target 
        /// of a specified <see cref="System.WeakReference"/>.  
        /// </summary>
        /// <param name="wr">The WeakReference whose target is the object
        /// whose generation will be returned</param>
        /// <returns>The generation of the target of the WeakReference</returns>
        /// <exception cref="ArgumentNullException">The target of the weak reference
        /// has already been garbage collected.</exception>
        public static int GetGeneration(WeakReference wr)
        {
            // note - this throws an NRE if given a null weak reference. This isn't
            // documented, but it's the behavior of Desktop and CoreCLR.
            Object handleRef = RuntimeImports.RhHandleGet(wr.m_handle);
            if (handleRef == null)
            {
                throw new ArgumentNullException(nameof(wr));
            }

            int result = RuntimeImports.RhGetGeneration(handleRef);
            KeepAlive(wr);
            return result;
        }

        // Forces a collection of all generations from 0 through Generation.
        public static void Collect(int generation)
        {
            Collect(generation, GCCollectionMode.Default);
        }

        // Garbage collect all generations.
        public static void Collect()
        {
            //-1 says to GC all generations.
            RuntimeImports.RhCollect(-1, InternalGCCollectionMode.Blocking);
        }

        public static void Collect(int generation, GCCollectionMode mode)
        {
            Collect(generation, mode, true);
        }

        public static void Collect(int generation, GCCollectionMode mode, bool blocking)
        {
            Collect(generation, mode, blocking, false);
        }

        public static void Collect(int generation, GCCollectionMode mode, bool blocking, bool compacting)
        {
            if (generation < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation), SR.ArgumentOutOfRange_GenericPositive);
            }

            if ((mode < GCCollectionMode.Default) || (mode > GCCollectionMode.Optimized))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), SR.ArgumentOutOfRange_Enum);
            }

            int iInternalModes = 0;

            if (mode == GCCollectionMode.Optimized)
            {
                iInternalModes |= (int)InternalGCCollectionMode.Optimized;
            }

            if (compacting)
            {
                iInternalModes |= (int)InternalGCCollectionMode.Compacting;
            }

            if (blocking)
            {
                iInternalModes |= (int)InternalGCCollectionMode.Blocking;
            }
            else if (!compacting)
            {
                iInternalModes |= (int)InternalGCCollectionMode.NonBlocking;
            }

            RuntimeImports.RhCollect(generation, (InternalGCCollectionMode)iInternalModes);
        }

        /// <summary>
        /// Specifies that a garbage collection notification should be raised when conditions are favorable
        /// for a full garbage collection and when the collection has been completed.
        /// </summary>
        /// <param name="maxGenerationThreshold">A number between 1 and 99 that specifies when the notification
        /// should be raised based on the objects allocated in Gen 2.</param>
        /// <param name="largeObjectHeapThreshold">A number between 1 and 99 that specifies when the notification
        /// should be raised based on the objects allocated in the large object heap.</param>
        /// <exception cref="ArgumentOutOfRangeException">If either of the two arguments are not between 1 and 99</exception>
        /// <exception cref="InvalidOperationException">If Concurrent GC is enabled</exception>"
        public static void RegisterForFullGCNotification(int maxGenerationThreshold, int largeObjectHeapThreshold)
        {
            if (maxGenerationThreshold < 1 || maxGenerationThreshold > 99)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxGenerationThreshold),
                    String.Format(SR.ArgumentOutOfRange_Bounds_Lower_Upper, 1, 99));
            }

            if (largeObjectHeapThreshold < 1 || largeObjectHeapThreshold > 99)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(largeObjectHeapThreshold),
                    String.Format(SR.ArgumentOutOfRange_Bounds_Lower_Upper, 1, 99));
            }

            // This is not documented on MSDN, but CoreCLR throws when the GC's
            // RegisterForFullGCNotification returns false
            if (!RuntimeImports.RhRegisterForFullGCNotification(maxGenerationThreshold, largeObjectHeapThreshold))
            {
                throw new InvalidOperationException(SR.InvalidOperation_NotWithConcurrentGC);
            }
        }

        /// <summary>
        /// Returns the status of a registered notification about whether a blocking garbage collection
        /// is imminent. May wait indefinitely for a full collection.
        /// </summary>
        /// <returns>The status of a registered full GC notification</returns>
        public static GCNotificationStatus WaitForFullGCApproach()
        {
            return (GCNotificationStatus)RuntimeImports.RhWaitForFullGCApproach(-1);
        }

        /// <summary>
        /// Returns the status of a registered notification about whether a blocking garbage collection
        /// is imminent. May wait up to a given timeout for a full collection.
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout on waiting for a full collection</param>
        /// <returns>The status of a registered full GC notification</returns>
        public static GCNotificationStatus WaitForFullGCApproach(int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millisecondsTimeout),
                    SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }

            return (GCNotificationStatus)RuntimeImports.RhWaitForFullGCApproach(millisecondsTimeout);
        }

        /// <summary>
        /// Returns the status of a registered notification about whether a blocking garbage collection
        /// has completed. May wait indefinitely for a full collection.
        /// </summary>
        /// <returns>The status of a registered full GC notification</returns>
        public static GCNotificationStatus WaitForFullGCComplete()
        {
            return (GCNotificationStatus)RuntimeImports.RhWaitForFullGCComplete(-1);
        }

        /// <summary>
        /// Returns the status of a registered notification about whether a blocking garbage collection
        /// has completed. May wait up to a specified timeout for a full collection.
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout on waiting for a full collection</param>
        /// <returns></returns>
        public static GCNotificationStatus WaitForFullGCComplete(int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millisecondsTimeout),
                    SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
            }

            return (GCNotificationStatus)RuntimeImports.RhWaitForFullGCComplete(millisecondsTimeout);
        }

        /// <summary>
        /// Cancels an outstanding full GC notification.
        /// </summary>
        /// <exception cref="InvalidOperationException">Raised if called
        /// with concurrent GC enabled</exception>
        public static void CancelFullGCNotification()
        {
            if (!RuntimeImports.RhCancelFullGCNotification())
            {
                throw new InvalidOperationException(SR.InvalidOperation_NotWithConcurrentGC);
            }
        }

        /// <summary>
        /// Attempts to disallow garbage collection during execution of a critical path.
        /// </summary>
        /// <param name="totalSize">Disallows garbage collection if a specified amount of
        /// of memory is available.</param>
        /// <returns>True if the disallowing of garbage collection was successful, False otherwise</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the amount of memory requested
        /// is too large for the GC to accomodate</exception>
        /// <exception cref="InvalidOperationException">If the GC is already in a NoGCRegion</exception>
        public static bool TryStartNoGCRegion(long totalSize)
        {
            return StartNoGCRegionWorker(totalSize, false, 0, false);
        }

        /// <summary>
        /// Attempts to disallow garbage collection during execution of a critical path.
        /// </summary>
        /// <param name="totalSize">Disallows garbage collection if a specified amount of
        /// of memory is available.</param>
        /// <param name="lohSize">Disallows garbagte collection if a specified amount of
        /// large object heap space is available.</param>
        /// <returns>True if the disallowing of garbage collection was successful, False otherwise</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the amount of memory requested
        /// is too large for the GC to accomodate</exception>
        /// <exception cref="InvalidOperationException">If the GC is already in a NoGCRegion</exception>
        public static bool TryStartNoGCRegion(long totalSize, long lohSize)
        {
            return StartNoGCRegionWorker(totalSize, true, lohSize, false);
        }

        /// <summary>
        /// Attempts to disallow garbage collection during execution of a critical path.
        /// </summary>
        /// <param name="totalSize">Disallows garbage collection if a specified amount of
        /// of memory is available.</param>
        /// <param name="disallowFullBlockingGC">Controls whether or not a full blocking GC
        /// is performed if the requested amount of memory is not available</param>
        /// <returns>True if the disallowing of garbage collection was successful, False otherwise</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the amount of memory requested
        /// is too large for the GC to accomodate</exception>
        /// <exception cref="InvalidOperationException">If the GC is already in a NoGCRegion</exception>
        public static bool TryStartNoGCRegion(long totalSize, bool disallowFullBlockingGC)
        {
            return StartNoGCRegionWorker(totalSize, false, 0, disallowFullBlockingGC);
        }

        /// <summary>
        /// Attempts to disallow garbage collection during execution of a critical path.
        /// </summary>
        /// <param name="totalSize">Disallows garbage collection if a specified amount of
        /// of memory is available.</param>
        /// <param name="lohSize">Disallows garbagte collection if a specified amount of
        /// large object heap space is available.</param>
        /// <param name="disallowFullBlockingGC">Controls whether or not a full blocking GC
        /// is performed if the requested amount of memory is not available</param>
        /// <returns>True if the disallowing of garbage collection was successful, False otherwise</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the amount of memory requested
        /// is too large for the GC to accomodate</exception>
        /// <exception cref="InvalidOperationException">If the GC is already in a NoGCRegion</exception>
        public static bool TryStartNoGCRegion(long totalSize, long lohSize, bool disallowFullBlockingGC)
        {
            return StartNoGCRegionWorker(totalSize, true, lohSize, disallowFullBlockingGC);
        }

        private static bool StartNoGCRegionWorker(long totalSize, bool hasLohSize, long lohSize, bool disallowFullBlockingGC)
        {
            StartNoGCRegionStatus status =
                (StartNoGCRegionStatus)RuntimeImports.RhStartNoGCRegion(totalSize, hasLohSize, lohSize, disallowFullBlockingGC);
            if (status == StartNoGCRegionStatus.AmountTooLarge)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalSize),
                    SR.ArgumentOutOfRangeException_NoGCRegionSizeTooLarge);
            }
            else if (status == StartNoGCRegionStatus.AlreadyInProgress)
            {
                throw new InvalidOperationException(
                    SR.InvalidOperationException_AlreadyInNoGCRegion);
            }
            else if (status == StartNoGCRegionStatus.NotEnoughMemory)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Exits the current no GC region.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the GC is not in a no GC region</exception>
        /// <exception cref="InvalidOperationException">If the no GC region was exited due to an induced GC</exception>
        /// <exception cref="InvalidOperationException">If the no GC region was exited due to memory allocations
        /// exceeding the amount given to <see cref="TryStartNoGCRegion(long)"/></exception>
        public static void EndNoGCRegion()
        {
            EndNoGCRegionStatus status = (EndNoGCRegionStatus)RuntimeImports.RhEndNoGCRegion();
            if (status == EndNoGCRegionStatus.NotInProgress)
            {
                throw new InvalidOperationException(
                    SR.InvalidOperationException_NoGCRegionNotInProgress);
            }
            else if (status == EndNoGCRegionStatus.GCInduced)
            {
                throw new InvalidOperationException(
                    SR.InvalidOperationException_NoGCRegionInduced);
            }
            else if (status == EndNoGCRegionStatus.AllocationExceeded)
            {
                throw new InvalidOperationException(
                    SR.InvalidOperationException_NoGCRegionAllocationExceeded);
            }
        }

        // Block until the next finalization pass is complete.
        public static void WaitForPendingFinalizers()
        {
            RuntimeImports.RhWaitForPendingFinalizers(RuntimeThread.ReentrantWaitsEnabled);
        }

        public static void SuppressFinalize(Object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            RuntimeImports.RhSuppressFinalize(obj);
        }

        public static void ReRegisterForFinalize(Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            RuntimeImports.RhReRegisterForFinalize(obj);
        }

        [Intrinsic]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // disable optimizations
        public static void KeepAlive(Object obj)
        {
        }

        // Returns the maximum GC generation.  Currently assumes only 1 heap.
        //
        public static int MaxGeneration
        {
            get { return RuntimeImports.RhGetMaxGcGeneration(); }
        }

        public static int CollectionCount(int generation)
        {
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation), SR.ArgumentOutOfRange_GenericPositive);

            return RuntimeImports.RhGetGcCollectionCount(generation, false);
        }

        // Support for AddMemoryPressure and RemoveMemoryPressure below.
        private const uint PressureCount = 4;
#if BIT64
        private const uint MinGCMemoryPressureBudget = 4 * 1024 * 1024;
#else
        private const uint MinGCMemoryPressureBudget = 3 * 1024 * 1024;
#endif

        private const uint MaxGCMemoryPressureRatio = 10;

        private static int[] s_gcCounts = new int[] { 0, 0, 0 };

        private static long[] s_addPressure = new long[] { 0, 0, 0, 0 };
        private static long[] s_removePressure = new long[] { 0, 0, 0, 0 };
        private static uint s_iteration = 0;

        /// <summary>
        /// Resets the pressure accounting after a gen2 GC has occured.
        /// </summary>
        private static void CheckCollectionCount()
        {
            if (s_gcCounts[2] != CollectionCount(2))
            {
                for (int i = 0; i < 3; i++)
                {
                    s_gcCounts[i] = CollectionCount(i);
                }

                s_iteration++;

                uint p = s_iteration % PressureCount;

                s_addPressure[p] = 0;
                s_removePressure[p] = 0;
            }
        }

        private static long InterlockedAddMemoryPressure(ref long pAugend, long addend)
        {
            long oldMemValue;
            long newMemValue;

            do
            {
                oldMemValue = pAugend;
                newMemValue = oldMemValue + addend;

                // check for overflow
                if (newMemValue < oldMemValue)
                {
                    newMemValue = long.MaxValue;
                }
            } while (Interlocked.CompareExchange(ref pAugend, newMemValue, oldMemValue) != oldMemValue);

            return newMemValue;
        }

        /// <summary>
        /// New AddMemoryPressure implementation (used by RCW and the CLRServicesImpl class)
        /// 1. Less sensitive than the original implementation (start budget 3 MB)
        /// 2. Focuses more on newly added memory pressure
        /// 3. Budget adjusted by effectiveness of last 3 triggered GC (add / remove ratio, max 10x)
        /// 4. Budget maxed with 30% of current managed GC size
        /// 5. If Gen2 GC is happening naturally, ignore past pressure
        /// 
        /// Here's a brief description of the ideal algorithm for Add/Remove memory pressure:
        /// Do a GC when (HeapStart is less than X * MemPressureGrowth) where
        /// - HeapStart is GC Heap size after doing the last GC
        /// - MemPressureGrowth is the net of Add and Remove since the last GC
        /// - X is proportional to our guess of the ummanaged memory death rate per GC interval,
        /// and would be calculated based on historic data using standard exponential approximation:
        /// Xnew = UMDeath/UMTotal * 0.5 + Xprev
        /// </summary>
        /// <param name="bytesAllocated"></param>
        public static void AddMemoryPressure(long bytesAllocated)
        {
            if (bytesAllocated <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesAllocated),
                        SR.ArgumentOutOfRange_NeedPosNum);
            }

#if !BIT64
            if (bytesAllocated > Int32.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesAllocated),
                        SR.ArgumentOutOfRange_MustBeNonNegInt32);
            }
#endif

            CheckCollectionCount();
            uint p = s_iteration % PressureCount;
            long newMemValue = InterlockedAddMemoryPressure(ref s_addPressure[p], bytesAllocated);

            Debug.Assert(PressureCount == 4, "GC.AddMemoryPressure contains unrolled loops which depend on the PressureCount");

            if (newMemValue >= MinGCMemoryPressureBudget)
            {
                long add = s_addPressure[0] + s_addPressure[1] + s_addPressure[2] + s_addPressure[3] - s_addPressure[p];
                long rem = s_removePressure[0] + s_removePressure[1] + s_removePressure[2] + s_removePressure[3] - s_removePressure[p];

                long budget = MinGCMemoryPressureBudget;

                if (s_iteration >= PressureCount)  // wait until we have enough data points
                {
                    // Adjust according to effectiveness of GC
                    // Scale budget according to past m_addPressure / m_remPressure ratio
                    if (add >= rem * MaxGCMemoryPressureRatio)
                    {
                        budget = MinGCMemoryPressureBudget * MaxGCMemoryPressureRatio;
                    }
                    else if (add > rem)
                    {
                        Debug.Assert(rem != 0);

                        // Avoid overflow by calculating addPressure / remPressure as fixed point (1 = 1024)
                        budget = (add * 1024 / rem) * budget / 1024;
                    }
                }

                // If still over budget, check current managed heap size
                if (newMemValue >= budget)
                {
                    long heapOver3 = RuntimeImports.RhGetCurrentObjSize() / 3;

                    if (budget < heapOver3)  //Max
                    {
                        budget = heapOver3;
                    }

                    if (newMemValue >= budget)
                    {
                        // last check - if we would exceed 20% of GC "duty cycle", do not trigger GC at this time
                        if ((RuntimeImports.RhGetGCNow() - RuntimeImports.RhGetLastGCStartTime(2)) > (RuntimeImports.RhGetLastGCDuration(2) * 5))
                        {
                            RuntimeImports.RhCollect(2, InternalGCCollectionMode.NonBlocking);
                            CheckCollectionCount();
                        }
                    }
                }
            }
        }

        public static void RemoveMemoryPressure(long bytesAllocated)
        {
            if (bytesAllocated <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesAllocated),
                        SR.ArgumentOutOfRange_NeedPosNum);
            }

#if !BIT64
            if (bytesAllocated > Int32.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesAllocated),
                        SR.ArgumentOutOfRange_MustBeNonNegInt32);
            }
#endif

            CheckCollectionCount();
            uint p = s_iteration % PressureCount;
            InterlockedAddMemoryPressure(ref s_removePressure[p], bytesAllocated);
        }

        public static long GetTotalMemory(bool forceFullCollection)
        {
            long size = RuntimeImports.RhGetGcTotalMemory();

            if (forceFullCollection)
            {
                // If we force a full collection, we will run the finalizers on all 
                // existing objects and do a collection until the value stabilizes.
                // The value is "stable" when either the value is within 5% of the 
                // previous call to GetTotalMemory, or if we have been sitting
                // here for more than x times (we don't want to loop forever here).
                int reps = 20;  // Number of iterations

                long diff;

                do
                {
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    long newSize = RuntimeImports.RhGetGcTotalMemory();
                    diff = (newSize - size) * 100 / size;
                    size = newSize;
                }
                while (reps-- > 0 && !(-5 < diff && diff < 5));
            }

            return size;
        }
    }
}
