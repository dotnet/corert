// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// NOTE:
//   These source code are being published to InternalAPIs and consumed by RH builds
//   Use PublishInteropAPI.bat to keep the InternalAPI copies in sync
// ----------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text;
using System.Runtime;
using Internal.NativeFormat;

namespace System.Runtime.InteropServices
{
    //
    // This section contains the code that would have been generated by MCG in order to use the AddrOf and
    // StdCall intrinsics, had we used it to generate all of our interop code.  In general, the transformation
    // done by the IL2IL step is simple.  We search the assembly being transformed for a definition of the
    // McgIntrinsics attribute and, then, any class marked with [McgIntrinsics] will be searched for methods
    // named StdCall and AddrOf.  For methods named StdCall, an implementation is provided that loads up the
    // arguments and does an IL 'calli' instruction to the first argument to StdCall using the unmanaged
    // stdcall calling convention.  For methods named AddrOf, it is the callsites that are transformed.  For
    // this, the callsites are implicitly constructing a Func<T> delegate around a static method.  The IL2IL
    // transform will look at every callsite to an AddrOf and ensure that it matches the expected pattern.  If
    // it does, it will eliminate the delegate construction and the call to the methods defined below and
    // simply leave the 'ldftn' IL instruction that was used as part of that delegate construction sequence.
    //
    // Note:In .NET Native NutC and binder on seeing an LDFTN with NativeCallable target method generate method
    // callable from native , on CoreCLR we wrap NativeCallable in an UMThunk , which essentialy
    // is the same code path as reverse PInvoke.

    // Note: Because AddrOf depends on Func<T>, the arguments may not be pointer types (because generics can
    // not be instantiated over pointer types).
    //
    // Note: Because StdCall is most commonly used with COM calls, void-returning StdCalls weren't implemented
    // in MCG and we've also avoided them here (even though the IL2IL transform should support them).
    //
    [System.AttributeUsageAttribute(System.AttributeTargets.Class)]
    internal class McgIntrinsicsAttribute : System.Attribute
    {
    }

    [McgIntrinsics]
    internal static unsafe partial class CalliIntrinsics
    {
        internal static int StdCall__QueryInterface(
                   IntPtr pfn,
                   IntPtr pComThis,
                   IntPtr arg0,
                   IntPtr arg1)
        {
            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__AddRef(System.IntPtr pfn, IntPtr pComThis)
        {
            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__Release(System.IntPtr pfn, IntPtr pComThis)
        {
            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__int(
            System.IntPtr pfn,
            IntPtr pComThis)
        {
            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__int(
                    IntPtr pfn,
                    IntPtr pComThis,
                    uint arg0)
        {
            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__int(
                    IntPtr pfn,
                    IntPtr pComThis,
                    void* arg0)
        {
            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__int(
                   IntPtr pfn,
                   IntPtr pComThis,
                   void* arg0,
                   void* arg1)
        {

            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__int(
           IntPtr pfn,
           IntPtr pComThis,
           void* arg0,
           int arg1,
           IntPtr arg2)
        {

            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        internal static int StdCall__int(
          IntPtr pfn,
          IntPtr pComThis,
          void* arg0,
          IntPtr arg1,
          uint arg2,
          IntPtr arg3,
          uint arg4,
          void* arg5)
        {

            // This method is implemented elsewhere in the toolchain
            return default(int);
        }

        public static int StdCall__int(
            IntPtr pfn,
            void* pthis,
            System.Runtime.InteropServices.HSTRING arg0,
            void* arg1)
        {
            // This method is implemented elsewhere in the toolchain
            return 0;
        }

        public static int StdCall__int(
            global::System.IntPtr pfn,
            void* pthis,
            void* arg0)
        {
            // This method is implemented elsewhere in the toolchain
            return 0;
        }

        public static int StdCall__int(
            global::System.IntPtr pfn,
            IntPtr pthis,
            HSTRING arg0,
            IntPtr arg1,
            void * arg2,
            void * arg3)
        {
            // This method is implemented elsewhere in the toolchain
            return 0;
        }

        public static int StdCall__int(
            global::System.IntPtr pfn,
            IntPtr pthis,
            int arg0,
            IntPtr arg1,
            IntPtr arg2,
            int arg3,
            int arg4,
            IntPtr arg5,
            void * arg6,
            void * arg7)
        {
            // This method is implemented elsewhere in the toolchain
            return 0;
        }

        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    void* pComThis,
                    ulong arg0,
                    uint arg1,
                    void* arg2)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    void* pComThis,
                    IntPtr arg0,
                    void* arg1,
                    void* arg2,
                    int arg3,
                    IntPtr arg4)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }

        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    uint arg0)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    void* arg0)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    void* arg0,
                    void* arg1)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    void* arg0,
                    uint arg1,
                    void* arg2)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    void* arg0,
                    void* arg1,
                    void* arg2)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    void* arg0,
                    uint arg1,
                    void* arg2,
                    void* arg3)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    int hr,
                    void* errorMsg,
                    System.IntPtr pUnk)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }
        internal static T StdCall<T>(
                    System.IntPtr pfn,
                    System.IntPtr pComThis,
                    out System.IntPtr arg1,
                    out int arg2,
                    out System.IntPtr arg3,
                    out System.IntPtr arg4)
        {
            // This method is implemented elsewhere in the toolchain
            arg1 = default(IntPtr);
            arg3 = default(IntPtr);
            arg4 = default(IntPtr);
            arg2 = 0;
            return default(T);
        }
        internal static T StdCall<T>(
            System.IntPtr pfn,
            System.IntPtr pComThis,
            out System.IntPtr arg)
        {
            // This method is implemented elsewhere in the toolchain
            arg = default(IntPtr);
            return default(T);
        }
        internal static T StdCall<T>(
            System.IntPtr pfn,
            System.IntPtr pComThis,
            System.Guid arg1,
            out System.IntPtr arg2)
        {
            // This method is implemented elsewhere in the toolchain
            arg2 = default(IntPtr);
            return default(T);
        }

        internal static T StdCall<T>(
            IntPtr pfn,
            void* pComThis,
            IntPtr piid,
            IntPtr pv,
            int dwDestContext,
            IntPtr pvDestContext,
            int mshlflags,
            IntPtr pclsid)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }

        internal static T StdCall<T>(
              IntPtr pfn,
              void* pComThis,
              IntPtr pStm,
              IntPtr piid,
              IntPtr pv,
              int dwDestContext,
              IntPtr pvDestContext,
              int mshlflags)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }

        internal static T StdCall<T>(
              IntPtr pfn,
              void* pComThis,
              IntPtr pStm,
              IntPtr piid,
              IntPtr ppvObj)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }

        internal static T StdCall<T>(
              IntPtr pfn,
              void* pComThis,
              IntPtr pStm)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }

        internal static T StdCall<T>(
              IntPtr pfn,
              void* pComThis,
              int dwReserved)
        {
            // This method is implemented elsewhere in the toolchain
            return default(T);
        }

        private const MethodImplOptions InternalCall = (MethodImplOptions)0x1000;

        // Cooperative GC versions of StdCall.
        // We need to call via an imported stub to do a stdcall without triggering GC
        // We can't use managed calli because the native target address could potentially satisfy a magic
        // bit check and causing the stub to believe it is a managed method which leads to crash.
#if !RHTESTCL && !CORECLR && !CORERT
        [MethodImplAttribute(InternalCall)]
#if X86
        [RuntimeImport("*", "@StdCallCOOP0@8")]
#else
        [RuntimeImport("*", "StdCallCOOP0")]
#endif // X86
        internal static extern int StdCallCOOP(System.IntPtr pfn, void* pComThis);

        [MethodImplAttribute(InternalCall)]
#if X86
        [RuntimeImport("*", "@StdCallCOOPV@12")]
#else
        [RuntimeImport("*", "StdCallCOOPV")]
#endif // X86

        internal static extern int StdCallCOOP(System.IntPtr pfn, void* pComThis, void* arg0);

        [MethodImplAttribute(InternalCall)]
#if X86
        [RuntimeImport("*", "@StdCallCOOPI@12")]
#else
        [RuntimeImport("*", "StdCallCOOPI")]
#endif // X86
        internal static extern int StdCallCOOP(System.IntPtr pfn, void* pComThis, int arg0);
#else
        internal static int StdCallCOOP(System.IntPtr pfn, void* pComThis)
        {
            return Call<int>(pfn, pComThis);
        }

        internal static int StdCallCOOP(System.IntPtr pfn, void* pComThis, void* arg0)
        {
            return Call<int>(pfn, pComThis, arg0);
        }

        internal static int StdCallCOOP(System.IntPtr pfn, void* pComThis, int arg0)
        {
            return Call<int>(pfn, pComThis, arg0);
        }
#endif

        // Used to invoke GetCcwVtable* methods which return pVtbl
        internal static IntPtr Call__GetCcwVtable(
                    System.IntPtr pfn)
        {
            return default(IntPtr);
        }
        internal static T Call<T>(
                   System.IntPtr pfn,
                   void* arg0)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    Object arg0)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    System.Object arg0,
                    System.Object arg1,
                    int arg2)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    System.Object arg0,
                    int arg1)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    void* arg0,
                    void* arg1)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    void* arg0,
                    int arg1)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    System.IntPtr arg0,
                    uint arg1,
                    System.IntPtr arg2)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    System.IntPtr arg0,
                    uint arg1,
                    void* arg2,
                    System.IntPtr arg3)
        {
            return default(T);
        }
        internal static T Call<T>(
                    System.IntPtr pfn,
                    __ComObject arg0,
                    System.IntPtr arg1)
        {
            return default(T);
        }
        internal static T Call<T>(
            System.IntPtr pfn,
            __ComObject arg0,
            System.IntPtr arg1,
            RuntimeTypeHandle arg2)
        {
            return default(T);
        }

#if ENABLE_WINRT
        // For SharedCCW_IVector/SharedCCW_IVectorView
        internal static T Call<T>(IntPtr pfn, object list, Toolbox.IList_Oper oper, int index, ref object item)
        {
            return default(T);
        }

        // For SharedCCW_ITerator
        internal static T Call<T>(IntPtr pfn, object iterator, Toolbox.IIterator_Oper oper, ref object data, int len)
        {
            return default(T);
        }

        internal static T Call<T>(IntPtr pfn, object iterator, Toolbox.IIterator_Oper oper, IntPtr pData, ref int len)
        {
            return default(T);
        }

#if !RHTESTCL && !CORECLR

        // For SharedCcw_AsyncOperationCompletedHandler
        internal static T Call<T>(IntPtr pfn, object handler, object asyncInfo, global::Windows.Foundation.AsyncStatus status)
        {
            return default(T);
        }
#endif
        // For SharedCCW_IVector_Blittable/SharedCCW_IVectorView_Blittable
        internal static T Call<T>(IntPtr pfn, object list, Toolbox.IList_Oper oper, ref int index, System.IntPtr pData)
        {
            return default(T);
        }
#endif // ENABLE_WINRT
        // For ForwardDelegateCreationStub
        internal static Delegate Call__Delegate(System.IntPtr pfn, System.IntPtr pStub)
        {
            return default(Delegate);
        }
    }

    [McgIntrinsics]
    internal static partial class AddrOfIntrinsics
    {
        internal static IntPtr AddrOf<T>(T ftn)
        {
            // This method is implemented elsewhere in the toolchain
            return default(IntPtr);
        }

        internal static IntPtr StaticFieldAddr<T>(ref T field)
        {
            // This method is implemented elsewhere in the toolchain
            return default(IntPtr);
        }

        internal static IntPtr VirtualAddrOf<T>(object o, int methodIndex)
        {
            // This method is implemented elsewhere in the toolchain
            return System.IntPtr.Zero;
        }

        // Apart from AddRef , Release and QI there are lot other functions with same
        // signature but different semantics.So keeping AddrOfTarget1 , AddrOfTarget4 around even though
        // they have the same signature as AddrOfAddRef and AddrOfQueryInterface

        // Common delegate signature for many functions
        internal delegate int AddrOfTarget1(IntPtr p0);
        internal delegate int AddrOfTarget2(IntPtr p0, int p1);
        internal delegate int AddrOfTarget3(IntPtr p0, IntPtr p1);
        internal delegate int AddrOfTarget4(IntPtr p0, IntPtr p1, IntPtr p2);
        internal delegate int AddrOfTarget5(IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3);

        // specialized delegates
        internal delegate int AddrOfGetIID(IntPtr __IntPtr__pComThis, IntPtr __IntPtr__iidCount, IntPtr __IntPtr__iids);
        internal delegate int AddrOfCreateManagedReference(System.IntPtr pComThis, IntPtr __IntPtr__pJupiterObject, IntPtr __IntPtr__ppNewReference);
        internal delegate int AddrOfResolve(IntPtr __IntPtr__pComThis, IntPtr __IntPtr__piid, IntPtr __IntPtr__ppWeakReference);
        internal delegate int AddrOfIndexOf(IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3);
        internal delegate int AddrOfMarshalInterface(IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, IntPtr p5, int p6);

        internal delegate int AddrOfGetMarshalUnMarshal(IntPtr pComThis,
                                                        IntPtr piid,
                                                        IntPtr pv,
                                                        int dwDestContext,
                                                        IntPtr pvDestContext,
                                                        int mshlflags,
                                                        IntPtr pclsid);

        internal delegate int AddrOfAttachingCtor(__ComObject comObject, IntPtr pBaseIUnknown, RuntimeTypeHandle classType);
        internal delegate int AddrOfGetSetInsertReplaceAll(IntPtr pComThis, uint index, IntPtr pItem);
        internal delegate int AddrOfRemoveAt(System.IntPtr pComThis, uint index);
        internal delegate int AddrOfGetMany1(IntPtr pComThis, uint startIndex, uint len, IntPtr pDest, IntPtr pCount);
        internal delegate int AddrOfGetMany2(IntPtr pComThis, uint len, IntPtr pDest, IntPtr pCount);
        internal delegate int AddrOfHookGCCallbacks(int nCondemnedGeneration);
        internal delegate int AddrOfAddRemoveMemoryPressure(System.IntPtr pComThis, ulong bytesAllocated);

        internal delegate bool AddrOfIsAlive(ComCallableObject comCallableObject);

        internal delegate int AddrOfGetTypeInfo(
            IntPtr pComThis,
            uint iTInfo,
            uint lcid,
            IntPtr ppTInfo);
        internal delegate int AddrOfGetIDsOfNames(
            IntPtr pComThis,
            IntPtr riid,
            IntPtr rgszNames,
            uint cNames,
            uint lcid,
            IntPtr rgDispId);
        internal delegate int AddrOfInvoke(
            IntPtr pComThis,
            int dispIdMember,
            IntPtr riid,
            uint lcid,
            ushort wFlags,
            IntPtr pDispParams,
            IntPtr pVarResult,
            IntPtr pExcepInfo,
            IntPtr puArgErr);

        // IStream
        internal delegate int AddrOfIStreamClone(IntPtr pComThis, out IntPtr ppstm);
        internal delegate int AddrOfIStreamCopyTo(IntPtr pComThis, IntPtr pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten);
        internal delegate int AddrOfIStreamLockRegion(IntPtr pComThis, long libOffset, long cb, int dwLockType);
        internal delegate int AddrOfIStreamRead(IntPtr pComThis, IntPtr pv, int cb, IntPtr pcbRead);
        internal delegate int AddrOfIStreamSeek(IntPtr pComThis, long dlibMove, int dwOrigin, IntPtr plibNewPosition);
        internal delegate int AddrOfIStreamSetSize(IntPtr pComThis, long libNewSize);
        internal delegate int AddrOfIStreamStat(IntPtr pComThis, IntPtr pstatstg, int grfStatFlag);
        internal delegate int AddrOfIStreamUnlockRegion(IntPtr pComThis, long libOffset, long cb, int dwLockType);
        internal delegate int AddrOfIStreamWrite(IntPtr pComThis, IntPtr pv, int cb, IntPtr pcbWritten);

#if !RHTESTCL && !CORECLR
        // ICommand
        internal delegate int AddrOfICommandremove_CanExecuteChanged(IntPtr pComThis, System.Runtime.InteropServices.WindowsRuntime.EventRegistrationToken unsafe_token);
#endif        
    }

#if !CORECLR && ENABLE_WINRT
    [McgIntrinsics]
    internal class WinRTAddrOfIntrinsics
    {
        internal delegate int AddrOfGetCustomProperty(System.IntPtr pComThis, HSTRING unsafe_name, IntPtr __IntPtr__unsafe_customProperty);
        internal delegate int AddrOfGetIndexedProperty(System.IntPtr pComThis, HSTRING unsafe_name, TypeName unsafe_type, IntPtr __IntPtr__unsafe_customProperty);
        internal delegate int AddrOfTarget19(IntPtr p0, IntPtr p1, int p2);
    }
#endif // !CORECLR && ENABLE_WINRT

    public delegate IntPtr AddrOfGetCCWVtable();
    public delegate int AddrOfRelease(IntPtr pComThis);
    public delegate int AddrOfAddRef(IntPtr pComThis);
    public delegate int AddrOfQueryInterface(IntPtr __IntPtr__pComThis, IntPtr __IntPtr__pIID, IntPtr __IntPtr__ppvObject);

    // Helper delegate to invoke mcg generated ForwardDelegateCreationStub(IntPtr functionPointer)
    public delegate Delegate ForwardDelegateCreationStub(IntPtr pFunc);
}
