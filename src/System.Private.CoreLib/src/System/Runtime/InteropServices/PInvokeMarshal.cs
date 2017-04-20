// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Contracts;
using System.Security;
using Internal.Runtime.CompilerHelpers;
using Internal.Runtime.Augments;
using Debug = System.Diagnostics.Debug;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This PInvokeMarshal class should provide full public Marshal 
    /// implementation for all things related to P/Invoke marshalling
    /// </summary>
    public partial class PInvokeMarshal
    {
        [ThreadStatic]
        internal static int s_lastWin32Error;

        public static int GetLastWin32Error()
        {
            return s_lastWin32Error;
        }

        public static void SetLastWin32Error(int errorCode)
        {
            s_lastWin32Error = errorCode;
        }

        public static unsafe IntPtr AllocHGlobal(IntPtr cb)
        {
            return MemAlloc(cb);
        }

        public static unsafe IntPtr AllocHGlobal(int cb)
        {
            return AllocHGlobal((IntPtr)cb);
        }

        public static void FreeHGlobal(IntPtr hglobal)
        {
            MemFree(hglobal);
        }

        public static unsafe IntPtr AllocCoTaskMem(int cb)
        {
            IntPtr allocatedMemory = CoTaskMemAlloc(new UIntPtr(unchecked((uint)cb)));
            if (allocatedMemory == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }
            return allocatedMemory;
        }

        public static void FreeCoTaskMem(IntPtr ptr)
        {
            CoTaskMemFree(ptr);
        }

        public static IntPtr SecureStringToGlobalAllocAnsi(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: true, unicode: false);
        }

        public static IntPtr SecureStringToGlobalAllocUnicode(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: true, unicode: true); ;
        }

        public static IntPtr SecureStringToCoTaskMemAnsi(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: false, unicode: false);
        }

        public static IntPtr SecureStringToCoTaskMemUnicode(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            Contract.EndContractBlock();

            return s.MarshalToString(globalAlloc: false, unicode: true);
        }

        #region Delegate marshalling

        private static object s_thunkPoolHeap;

        /// <summary>
        /// Return the stub to the pinvoke marshalling stub
        /// </summary>
        /// <param name="del">The delegate</param>
        public static IntPtr GetStubForPInvokeDelegate(Delegate del)
        {
            if (del == null)
                return IntPtr.Zero;

            NativeFunctionPointerWrapper fpWrapper = del.Target as NativeFunctionPointerWrapper;
            if (fpWrapper != null)
            {
                //
                // Marshalling a delegate created from native function pointer back into function pointer
                // This is easy - just return the 'wrapped' native function pointer
                //
                return fpWrapper.NativeFunctionPointer;
            }
            else
            {
                //
                // Marshalling a managed delegate created from managed code into a native function pointer
                //
                return GetOrAllocateThunk(del);
            }
        }
        /// <summary>
        /// Used to lookup whether a delegate already has thunk allocated for it
        /// </summary>
        private static ConditionalWeakTable<Delegate, PInvokeDelegateThunk> s_pInvokeDelegates;

        private static ConditionalWeakTable<Delegate, PInvokeDelegateThunk> GetPInvokeDelegates()
        {
            //
            // Create the dictionary on-demand to avoid the dependency in the McgModule.ctor
            // Otherwise NUTC will complain that McgModule being eager ctor depends on a deferred
            // ctor type
            //
            if (s_pInvokeDelegates == null)
            {

                Interlocked.CompareExchange(
                    ref s_pInvokeDelegates,
                    new ConditionalWeakTable<Delegate, PInvokeDelegateThunk>(),
                    null
                );
            }

            return s_pInvokeDelegates;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal unsafe struct ThunkContextData
        {
            public GCHandle Handle;        //  A weak GCHandle to the delegate
            public IntPtr FunctionPtr;     // Function pointer for open static delegates
        }

        internal sealed class PInvokeDelegateThunk
        {
            public IntPtr Thunk;        //  Thunk pointer
            public IntPtr ContextData;  //  ThunkContextData pointer which will be stored in the context slot of the thunk

            public PInvokeDelegateThunk(Delegate del)
            {

                Thunk = RuntimeAugments.AllocateThunk(s_thunkPoolHeap);
                Debug.Assert(Thunk != IntPtr.Zero);

                if (Thunk == IntPtr.Zero)
                {
                    // We've either run out of memory, or failed to allocate a new thunk due to some other bug. Now we should fail fast
                    Environment.FailFast("Insufficient number of thunks.");
                }
                else
                {
                    //
                    // Allocate unmanaged memory for GCHandle of delegate and function pointer of open static delegate
                    // We will store this pointer on the context slot of thunk data
                    //
                    ContextData = AllocHGlobal(2 * IntPtr.Size);
                    unsafe
                    {
                        ThunkContextData* thunkData = (ThunkContextData*)ContextData;

                        // allocate a weak GChandle for the delegate
                        thunkData->Handle = GCHandle.Alloc(del, GCHandleType.Weak);

                        // if it is an open static delegate get the function pointer
                        thunkData->FunctionPtr = del.GetRawFunctionPointerForOpenStaticDelegate();
                    }
                }
            }

            ~PInvokeDelegateThunk()
            {
                // Free the thunk
                RuntimeAugments.FreeThunk(s_thunkPoolHeap, Thunk);
                unsafe
                {
                    if (ContextData != IntPtr.Zero)
                    {
                        // free the GCHandle
                        GCHandle handle = ((ThunkContextData*)ContextData)->Handle;
                        if (handle != null)
                        {
                            handle.Free();
                        }
                
                        // Free the allocated context data memory
                        FreeHGlobal(ContextData);
                    }
                }
            }
        }

        private static IntPtr GetOrAllocateThunk(Delegate del)
        {
            ConditionalWeakTable<Delegate, PInvokeDelegateThunk> pinvokeDelegates = GetPInvokeDelegates();

            PInvokeDelegateThunk delegateThunk;

            // if the delegate already exists in the table return the allocated thunk for it
            if (pinvokeDelegates.TryGetValue(del, out delegateThunk))
            {
                return delegateThunk.Thunk;
            }

            if (s_thunkPoolHeap == null)
            {
                s_thunkPoolHeap = RuntimeAugments.CreateThunksHeap(RuntimeImports.GetInteropCommonStubAddress());
                Debug.Assert(s_thunkPoolHeap != null);
            }


            delegateThunk = new PInvokeDelegateThunk(del);


            McgPInvokeDelegateData pinvokeDelegateData;
            if (!RuntimeAugments.InteropCallbacks.TryGetMarshallerDataForDelegate(del.GetTypeHandle(), out pinvokeDelegateData))
            {
                Environment.FailFast("Couldn't find marshalling stubs for delegate.");
            }

            //
            //  For open static delegates set target to ReverseOpenStaticDelegateStub which calls the static function pointer directly
            //
            IntPtr pTarget = del.GetRawFunctionPointerForOpenStaticDelegate() == IntPtr.Zero ? pinvokeDelegateData.ReverseStub : pinvokeDelegateData.ReverseOpenStaticDelegateStub;


            RuntimeAugments.SetThunkData(s_thunkPoolHeap, delegateThunk.Thunk, delegateThunk.ContextData, pTarget);

            // Add the delegate to the dictionary if it doesn't already exists
            delegateThunk = pinvokeDelegates.GetOrAdd(del, delegateThunk);

            return delegateThunk.Thunk;
        }

        /// <summary>
        /// Retrieve the corresponding P/invoke instance from the stub
        /// </summary>
        public static Delegate GetPInvokeDelegateForStub(IntPtr pStub, RuntimeTypeHandle delegateType)
        {
            if (pStub == IntPtr.Zero)
                return null;
            //
            // First try to see if this is one of the thunks we've allocated when we marshal a managed
            // delegate to native code
            // s_thunkPoolHeap will be null if there isn't any managed delegate to native
            //
            IntPtr pContext;
            IntPtr pTarget;
            if (s_thunkPoolHeap != null && RuntimeAugments.TryGetThunkData(s_thunkPoolHeap, pStub, out pContext, out pTarget))
            {
                GCHandle handle;
                unsafe
                {
                    // Pull out Handle from context
                    handle = ((ThunkContextData*)pContext)->Handle;
                }
                Delegate target = InteropExtensions.UncheckedCast<Delegate>(handle.Target);

                //
                // The delegate might already been garbage collected
                // User should use GC.KeepAlive or whatever ways necessary to keep the delegate alive
                // until they are done with the native function pointer
                //
                if (target == null)
                {
                    Environment.FailFast(SR.Delegate_GarbageCollected);
                }

                return target;
            }

            //
            // Otherwise, the stub must be a pure native function pointer
            // We need to create the delegate that points to the invoke method of a
            // NativeFunctionPointerWrapper derived class
            //
            McgPInvokeDelegateData pInvokeDelegateData;
            if (!RuntimeAugments.InteropCallbacks.TryGetMarshallerDataForDelegate(delegateType, out pInvokeDelegateData))
            {
                return null;
            }
            return CalliIntrinsics.Call<Delegate>(
                pInvokeDelegateData.ForwardDelegateCreationStub,
                pStub
            );
        }

        /// <summary>
        /// Retrieves the function pointer for the current open static delegate that is being called
        /// </summary>
        public static IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
            //
            // RH keeps track of the current thunk that is being called through a secret argument / thread
            // statics. No matter how that's implemented, we get the current thunk which we can use for
            // look up later
            //
            IntPtr pContext = RuntimeImports.GetCurrentInteropThunkContext();
            Debug.Assert(pContext != null);

            IntPtr fnPtr;
            unsafe
            {
                // Pull out function pointer for open static delegate
                fnPtr = ((ThunkContextData*)pContext)->FunctionPtr;
            }
            Debug.Assert(fnPtr != null);

            return fnPtr;
        }

        /// <summary>
        /// Retrieves the current delegate that is being called
        /// </summary>
        public static T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
            //
            // RH keeps track of the current thunk that is being called through a secret argument / thread
            // statics. No matter how that's implemented, we get the current thunk which we can use for
            // look up later
            //
            IntPtr pContext = RuntimeImports.GetCurrentInteropThunkContext();

            Debug.Assert(pContext != null);

            GCHandle handle;
            unsafe
            {
                // Pull out Handle from context
                handle = ((ThunkContextData*)pContext)->Handle;

            }

            T target = InteropExtensions.UncheckedCast<T>(handle.Target);

            //
            // The delegate might already been garbage collected
            // User should use GC.KeepAlive or whatever ways necessary to keep the delegate alive
            // until they are done with the native function pointer
            //
            if (target == null)
            {
                Environment.FailFast(SR.Delegate_GarbageCollected);
            }
            return target;
        }

        [McgIntrinsics]
        private static unsafe class CalliIntrinsics
        {
            internal static T Call<T>(IntPtr pfn, IntPtr arg0) { throw new NotImplementedException(); }
        }
        #endregion
    }
}
