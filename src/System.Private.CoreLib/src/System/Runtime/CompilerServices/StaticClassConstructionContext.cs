// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Runtime;
using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    // This structure is used to pass context about a type's static class construction state from the runtime
    // to the classlibrary via the CheckStaticClassConstruction callback. The runtime knows about the first
    // two fields (cctorMethodAddress and initialized) and thus these must remain the first two fields in the
    // same order and at the same offset (hence the sequential layout attribute). It is permissable for the
    // classlibrary to add its own fields after these for its own use however. These must not contain GC
    // references and will be zero initialized.
    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct StaticClassConstructionContext
    {
        // Pointer to the code for the static class constructor method. This is initialized by the
        // binder/runtime.
        public IntPtr cctorMethodAddress;

        // Initialization state of the class. This is initialized to 0. Every time managed code checks the
        // cctor state the runtime will call the classlibrary's CheckStaticClassConstruction with this context
        // structure unless initialized == 1. This check is specific to allow the classlibrary to store more
        // than a binary state for each cctor if it so desires.
        public volatile int initialized;
    }

#if CORERT 
    // CORERT-TODO: Use full cctor helper instead
    [McgIntrinsics]
    internal sealed class CctorHelper
    {
        // Called by the runtime when it finds a class whose static class constructor has probably not run
        // (probably because it checks in the initialized flag without thread synchronization).
        //
        // This method should synchronize with other threads, recheck the initialized flag and execute the
        // cctor method (whose address is given in the context structure) before setting the initialized flag
        // to 1. Once in this state the runtime will not call this method again for the same type (barring
        // race conditions).
        //
        // The context structure passed by reference lives in the image of one of the application's modules.
        // The contents are thus fixed (do not require pinning) and the address can be used as a unique
        // identifier for the context.
        [RuntimeExport("CheckStaticClassConstruction")]
        private static unsafe void CheckStaticClassConstruction(ref StaticClassConstructionContext context)
        {
            // This is a simplistic placeholder implementation. For instance it uses a busy wait spinlock and
            // does not handle recursion.
        
            while (true)
            {
                // Read the current state of the cctor.
                int oldInitializationState = context.initialized;
        
                // Once it transitions to 1 then the cctor has been run (another thread got there first) and
                // we can simply return.
                if (oldInitializationState == 1)
                    return;
        
                // If the state is anything other than 0 (the initial state) then another thread is currently
                // running this cctor. We must wait for it to complete doing so before continuing, so loop
                // again.
                if (oldInitializationState != 0)
                    continue;
        
                // C# warns that passing a volatile field to a method via a reference loses the volatility of the field.
                // However the method in question is Interlocked.CompareExchange so the volatility in this case is
                // unimportant.
                #pragma warning disable 420
        
                // We read a state of 0 (the initial state: not initialized and not being initialized). Try to
                // transition this to 2 which will let other threads know we're going to run the cctor here.
                if (Interlocked.CompareExchange(ref context.initialized, 2, 0) == 0)
                {
                    // We won the race to transition the state from 0 -> 2. So we can now run the cctor. Other
                    // threads trying to do the same thing will spin waiting for us to transition the state to
                    // 1.
        
                    // Call the cctor code directly from the address in the context. The <int> here says the
                    // cctor returns an int because the calli transform used doesn't handle the void case yet.
                    Call<int>(context.cctorMethodAddress);
        
                    // Insert a memory barrier here to order any writes executed as part of static class
                    // construction above with respect to the initialized flag update we're about to make
                    // below. This is important since the fast path for checking the cctor uses a normal read
                    // and doesn't come here so without the barrier it could observe initialized == 1 but
                    // still see uninitialized static fields on the class.
                    Interlocked.MemoryBarrier();
        
                    // Set the state to 1 to indicate to the runtime and other threads that this cctor has now
                    // been run.
                    context.initialized = 1;
                }
        
                // If we get here some other thread changed the initialization state to a non-zero value
                // before we could. Loop at try again.
            }
        }

        private static object CheckStaticClassConstructionReturnGCStaticBase(ref StaticClassConstructionContext context, object gcStaticBase)
        {
            // CORERT-TODO: Early out if initializer was set to anything. The runner doesn't handle recursion and assumes it's access from
            //              a different thread and deadlocks itself. CoreRT will cause recursion for things like trying to get a static
            //              base from the CCtor (which means that pretty much all cctors will deadlock).
            if (context.initialized == 0)
                CheckStaticClassConstruction(ref context);
            return gcStaticBase;
        }

        private static IntPtr CheckStaticClassConstructionReturnNonGCStaticBase(ref StaticClassConstructionContext context, IntPtr nonGcStaticBase)
        {
            // CORERT-TODO: Early out if initializer was set to anything. The runner doesn't handle recursion and assumes it's access from
            //              a different thread and deadlocks itself. CoreRT will cause recursion for things like trying to get a static
            //              base from the CCtor (which means that pretty much all cctors will deadlock).
            if (context.initialized == 0)
                CheckStaticClassConstruction(ref context);
            return nonGcStaticBase;
        }

        // Intrinsic to call the cctor given a pointer to the code (this method's body is ignored and replaced
        // with a calli during compilation). The transform doesn't handle non-generic versions yet (i.e.
        // functions that are void).
        private static T Call<T>(System.IntPtr pfn)
        {
            return default(T);
        }
     }
#endif
}
