// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime
{
    internal class GCStress
    {
        // ProjectX TODO:  delete the RuntimeExport attribute. The RuntimeExport version is
        // only used in MDIL mode where it's called in the managed bootstrapping code. 
        [RuntimeExport("RhGcStress_Initialize")]
        public static void Initialize()
        {
#if FEATURE_GC_STRESS
            // This method is called via binder-injected code in a module's DllMain.  The OS guarantees that 
            // only one thread at a time is in any DllMain, so we should be thread-safe as a result.
            if (Initialized)
                return;

            Initialized = true;

            Head = new GCStress();
            Tail = Head;

            int size = 10;
            for (int i = 0; i < size; i++)
            {
                Tail.Next = new GCStress();
                Tail = Tail.Next;
            }

            // drop the first element 
            Head = Head.Next;

            // notify redhawku.dll
            InternalCalls.RhpInitializeGcStress();
#endif // FEATURE_GC_STRESS
        }

        [UnmanagedCallersOnly(EntryPoint = "RhGcStress_Initialize2", CallingConvention = CallingConvention.Cdecl)]
        internal static void Initialize2()
        {
            Initialize();
        }

        [System.Diagnostics.Conditional("FEATURE_GC_STRESS")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void TriggerGC()
        {
#if FEATURE_GC_STRESS
            if(GCStress.Initialized)
                InternalCalls.RhCollect(-1, InternalGCCollectionMode.Blocking);
#endif
        }

#if FEATURE_GC_STRESS
        ~GCStress()
        {
            // drop the first element
            Head = Head.Next;

            // create and link a new element at the end of the list 
            Tail.Next = new GCStress();
            Tail = Tail.Next;
        }
#endif // FEATURE_GC_STRESS

#if FEATURE_GC_STRESS
        internal static bool Initialized { get; private set; }
        private static GCStress Head;
        private static GCStress Tail;

        private GCStress Next;
#endif // FEATURE_GC_STRESS

        private GCStress()
        {
        }
    }
}
