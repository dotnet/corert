// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime
{
    public /*internal*/ class GCStress
    {
        [RuntimeExport("RhGcStress_Initialize")]
        public /*internal*/ static void Initialize()
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

        ~GCStress()
        {
#if FEATURE_GC_STRESS
            // drop the first element
            Head = Head.Next;

            // create and link a new element at the end of the list 
            Tail.Next = new GCStress();
            Tail = Tail.Next;
#endif // FEATURE_GC_STRESS
        }

#if FEATURE_GC_STRESS
        static internal bool Initialized { get; private set; }
        static private GCStress Head;
        static private GCStress Tail;

        private GCStress Next;
#endif // FEATURE_GC_STRESS

        private GCStress()
        {
        }
    }
}
