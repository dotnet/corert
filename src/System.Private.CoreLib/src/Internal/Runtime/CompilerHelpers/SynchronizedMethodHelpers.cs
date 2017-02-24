// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// Set of helpers used to implement synchronized methods.
    /// </summary>
    internal static class SynchronizedMethodHelpers
    {
        private static void MonitorEnter(object obj, ref bool lockTaken)
        {
            // Inlined Monitor.Enter with a few tweaks
            Lock lck = Monitor.GetLock(obj);
            if (lck.TryAcquire(0))
            {
                lockTaken = true;
                return;
            }
            Monitor.TryAcquireContended(lck, obj, Timeout.Infinite);
            lockTaken = true;
        }
        private static void MonitorExit(object obj, ref bool lockTaken)
        {
            // Inlined Monitor.Exit with a few tweaks
            if (!lockTaken) return;
            Monitor.GetLock(obj).Release();
            lockTaken = false;
        }

        private static void MonitorEnterStatic(IntPtr pEEType, ref bool lockTaken)
        {
            // Inlined Monitor.Enter with a few tweaks
            object obj = GetStaticLockObject(pEEType);
            Lock lck = Monitor.GetLock(obj);
            if (lck.TryAcquire(0))
            {
                lockTaken = true;
                return;
            }
            Monitor.TryAcquireContended(lck, obj, Timeout.Infinite);
            lockTaken = true;
        }
        private static void MonitorExitStatic(IntPtr pEEType, ref bool lockTaken)
        {
            // Inlined Monitor.Exit with a few tweaks
            if (!lockTaken) return;
            object obj = GetStaticLockObject(pEEType);
            Monitor.GetLock(obj).Release();
            lockTaken = false;
        }

        private static Object GetStaticLockObject(IntPtr pEEType)
        {
            return Internal.Reflection.Core.NonPortable.ReflectionCoreNonPortable.GetRuntimeTypeForEEType(new System.EETypePtr(pEEType));
        }
    }
}
