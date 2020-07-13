// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;

namespace System.Runtime.InteropServices
{
    public static partial class Marshal
    {
        public static int GetHRForException(Exception? e)
        {
            return PInvokeMarshal.GetHRForException(e);
        }

        public unsafe static int AddRef(IntPtr pUnk)
        {
            if (pUnk == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pUnk));

            return CalliIntrinsics.StdCall__AddRef(((__com_IUnknown*)(void*)pUnk)->pVtable->
                pfnAddRef, pUnk);
        }

        public static bool AreComObjectsAvailableForCleanup() => false;

        public static IntPtr CreateAggregatedObject(IntPtr pOuter, object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object BindToMoniker(string monikerName)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static void CleanupUnusedObjectsInCurrentContext()
        {
        }

        public static IntPtr CreateAggregatedObject<T>(IntPtr pOuter, T o) where T : notnull
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object? CreateWrapperOfType(object? o, Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static TWrapper CreateWrapperOfType<T, TWrapper>([AllowNull] T o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static void ChangeWrapperHandleStrength(object otp, bool fIsWeak)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static int FinalReleaseComObject(object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static IntPtr GetComInterfaceForObject(object o, Type T)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static IntPtr GetComInterfaceForObject(object o, Type T, CustomQueryInterfaceMode mode)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static IntPtr GetComInterfaceForObject<T, TInterface>([DisallowNull] T o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object? GetComObjectData(object obj, object key)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static IntPtr GetHINSTANCE(Module m)
        {
            if (m is null)
            {
                throw new ArgumentNullException(nameof(m));
            }

            return (IntPtr)(-1);
        }           

        public static IntPtr GetIDispatchForObject(object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static IntPtr GetIUnknownForObject(object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static void GetNativeVariantForObject(object? obj, IntPtr pDstNativeVariant)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static void GetNativeVariantForObject<T>([AllowNull] T obj, IntPtr pDstNativeVariant)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object GetTypedObjectForIUnknown(IntPtr pUnk, Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object GetObjectForIUnknown(IntPtr pUnk)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object? GetObjectForNativeVariant(IntPtr pSrcNativeVariant)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [return: MaybeNull]
        public static T GetObjectForNativeVariant<T>(IntPtr pSrcNativeVariant)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object?[] GetObjectsForNativeVariants(IntPtr aSrcNativeVariant, int cVars)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static T[] GetObjectsForNativeVariants<T>(IntPtr aSrcNativeVariant, int cVars)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static int GetStartComSlot(Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static int GetEndComSlot(Type t)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static Type? GetTypeFromCLSID(Guid clsid)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static string GetTypeInfoName(ITypeInfo typeInfo)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static object GetUniqueObjectForIUnknown(IntPtr unknown)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static bool IsComObject(object o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            return false;
        }

        public static bool IsTypeVisibleFromCom(Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }
            return false;
        }

        public unsafe static int QueryInterface(IntPtr pUnk, ref Guid iid, out IntPtr ppv)
        {
            if (pUnk == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pUnk));

            IntPtr pComIUnk;
            int hr;

            fixed (Guid* unsafe_iid = &iid)
            {
                hr = CalliIntrinsics.StdCall__QueryInterface(((__com_IUnknown*)(void*)pUnk)->pVtable->
                                pfnQueryInterface,
                                pUnk,
                                new IntPtr(unsafe_iid),
                                new IntPtr(&pComIUnk));
            }

            if (hr != 0)
            {
                ppv = default(IntPtr);
            }
            else
            {
                ppv = pComIUnk;
            }

            return hr;
        }

        public unsafe static int Release(IntPtr pUnk)
        {
            if (pUnk == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pUnk));

            return CalliIntrinsics.StdCall__Release(((__com_IUnknown*)(void*)pUnk)->pVtable->
                pfnRelease, pUnk);
        }

        public static int ReleaseComObject(object o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        public static bool SetComObjectData(object obj, object key, object? data)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ComInterop);
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct __com_IUnknown
        {
            internal __vtable_IUnknown* pVtable;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct __vtable_IUnknown
        {
            // IUnknown
            internal IntPtr pfnQueryInterface;
            internal IntPtr pfnAddRef;
            internal IntPtr pfnRelease;
        }

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
        }
    }
}
