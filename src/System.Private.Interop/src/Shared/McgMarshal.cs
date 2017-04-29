// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// Marshalling helpers used by MCG
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
using System.Threading;
using System.Text;
using System.Runtime;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Internal.NativeFormat;

#if !CORECLR
using Internal.Runtime.Augments;
#endif

#if RHTESTCL
using OutputClass = System.Console;
#else
using OutputClass = System.Diagnostics.Debug;
#endif

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Expose functionality from System.Private.CoreLib and forwards calls to InteropExtensions in System.Private.CoreLib
    /// </summary>
    [CLSCompliant(false)]
    public static partial class McgMarshal
    {
        public static void SaveLastWin32Error()
        {
            PInvokeMarshal.SaveLastWin32Error();
        }

        public static bool GuidEquals(ref Guid left, ref Guid right)
        {
            return InteropExtensions.GuidEquals(ref left, ref right);
        }

        public static bool ComparerEquals<T>(T left, T right)
        {
            return InteropExtensions.ComparerEquals<T>(left, right);
        }

        public static T CreateClass<T>() where T : class
        {
            return InteropExtensions.UncheckedCast<T>(InteropExtensions.RuntimeNewObject(typeof(T).TypeHandle));
        }

        public static bool IsEnum(object obj)
        {
#if RHTESTCL
            return false;
#else
            return InteropExtensions.IsEnum(obj.GetTypeHandle());
#endif
        }

        public static bool IsCOMObject(Type type)
        {
#if RHTESTCL
            return false;
#else
            return type.GetTypeInfo().IsSubclassOf(typeof(__ComObject));
#endif
        }

        public static T FastCast<T>(object value) where T : class
        {
            // We have an assert here, to verify that a "real" cast would have succeeded.
            // However, casting on weakly-typed RCWs modifies their state, by doing a QI and caching
            // the result.  This often makes things work which otherwise wouldn't work (especially variance).
            Debug.Assert(value == null || value is T);
            return InteropExtensions.UncheckedCast<T>(value);
        }

        /// <summary>
        /// Converts a managed DateTime to native OLE datetime
        /// Used by MCG marshalling code
        /// </summary>
        public static double ToNativeOleDate(DateTime dateTime)
        {
            return InteropExtensions.ToNativeOleDate(dateTime);
        }

        /// <summary>
        /// Converts native OLE datetime to managed DateTime
        /// Used by MCG marshalling code
        /// </summary>
        public static DateTime FromNativeOleDate(double nativeOleDate)
        {
            return InteropExtensions.FromNativeOleDate(nativeOleDate);
        }

        /// <summary>
        /// Used in Marshalling code
        /// Call safeHandle.InitializeHandle to set the internal _handle field
        /// </summary>
        public static void InitializeHandle(SafeHandle safeHandle, IntPtr win32Handle)
        {
            InteropExtensions.InitializeHandle(safeHandle, win32Handle);
        }

        /// <summary>
        /// Check if obj's type is the same as represented by normalized handle
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsOfType(object obj, RuntimeTypeHandle handle)
        {
            return obj.IsOfType(handle);
        }

#if ENABLE_MIN_WINRT
        public static unsafe void SetExceptionErrorCode(Exception exception, int errorCode)
        {
            InteropExtensions.SetExceptionErrorCode(exception, errorCode);
        }

        /// <summary>
        /// Used in Marshalling code
        /// Gets the handle of the CriticalHandle
        /// </summary>
        public static IntPtr GetHandle(CriticalHandle criticalHandle)
        {
            return InteropExtensions.GetCriticalHandle(criticalHandle);
        }

        /// <summary>
        /// Used in Marshalling code
        /// Sets the handle of the CriticalHandle
        /// </summary>
        public static void SetHandle(CriticalHandle criticalHandle, IntPtr handle)
        {
            InteropExtensions.SetCriticalHandle(criticalHandle, handle);
        }
#endif
    }

    /// <summary>
    /// McgMarshal helpers exposed to be used by MCG
    /// </summary>
    public static partial class McgMarshal
    {
        #region Type marshalling

        public static Type TypeNameToType(HSTRING nativeTypeName, int nativeTypeKind)
        {
#if ENABLE_WINRT
            return McgTypeHelpers.TypeNameToType(nativeTypeName, nativeTypeKind);
#else
            throw new NotSupportedException("TypeNameToType");
#endif
        }

        public static unsafe void TypeToTypeName(
            Type type,
            out HSTRING nativeTypeName,
            out int nativeTypeKind)
        {
#if ENABLE_WINRT
            McgTypeHelpers.TypeToTypeName(type, out nativeTypeName, out nativeTypeKind);
#else
            throw new NotSupportedException("TypeToTypeName");
#endif
        }

        #endregion

        #region String marshalling

        [CLSCompliant(false)]
        public static unsafe void StringBuilderToUnicodeString(System.Text.StringBuilder stringBuilder, ushort* destination)
        {
            PInvokeMarshal.StringBuilderToUnicodeString(stringBuilder, destination);
        }

        [CLSCompliant(false)]
        public static unsafe void UnicodeStringToStringBuilder(ushort* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            PInvokeMarshal.UnicodeStringToStringBuilder(newBuffer, stringBuilder);
        }

#if !RHTESTCL

        [CLSCompliant(false)]
        public static unsafe void StringBuilderToAnsiString(System.Text.StringBuilder stringBuilder, byte* pNative,
            bool bestFit, bool throwOnUnmappableChar)
        {
            PInvokeMarshal.StringBuilderToAnsiString(stringBuilder, pNative, bestFit, throwOnUnmappableChar);
        }

        [CLSCompliant(false)]
        public static unsafe void AnsiStringToStringBuilder(byte* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            PInvokeMarshal.AnsiStringToStringBuilder(newBuffer, stringBuilder);
        }

        /// <summary>
        /// Convert ANSI string to unicode string, with option to free native memory. Calls generated by MCG
        /// </summary>
        /// <remarks>Input assumed to be zero terminated. Generates String.Empty for zero length string.
        /// This version is more efficient than ConvertToUnicode in src\Interop\System\Runtime\InteropServices\Marshal.cs in that it can skip calling
        /// MultiByteToWideChar for ASCII string, and it does not need another char[] buffer</remarks>
        [CLSCompliant(false)]
        public static unsafe string AnsiStringToString(byte* pchBuffer)
        {
            return PInvokeMarshal.AnsiStringToString(pchBuffer);
        }

        /// <summary>
        /// Convert UNICODE string to ANSI string.
        /// </summary>
        /// <remarks>This version is more efficient than StringToHGlobalAnsi in Interop\System\Runtime\InteropServices\Marshal.cs in that
        /// it could allocate single byte per character, instead of SystemMaxDBCSCharSize per char, and it can skip calling WideCharToMultiByte for ASCII string</remarks>
        [CLSCompliant(false)]
        public static unsafe byte* StringToAnsiString(string str, bool bestFit, bool throwOnUnmappableChar)
        {
            return PInvokeMarshal.StringToAnsiString(str, bestFit, throwOnUnmappableChar);
        }

        /// <summary>
        /// Convert UNICODE wide char array to ANSI ByVal byte array.
        /// </summary>
        /// <remarks>
        /// * This version works with array instead string, it means that there will be NO NULL to terminate the array.
        /// * The buffer to store the byte array must be allocated by the caller and must fit managedArray.Length.
        /// </remarks>
        /// <param name="managedArray">UNICODE wide char array</param>
        /// <param name="pNative">Allocated buffer where the ansi characters must be placed. Could NOT be null. Buffer size must fit char[].Length.</param>
        [CLSCompliant(false)]
        public static unsafe void ByValWideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, int expectedCharCount,
            bool bestFit, bool throwOnUnmappableChar)
        {
            PInvokeMarshal.ByValWideCharArrayToAnsiCharArray(managedArray, pNative, expectedCharCount, bestFit, throwOnUnmappableChar);
        }

        [CLSCompliant(false)]
        public static unsafe void ByValAnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            PInvokeMarshal.ByValAnsiCharArrayToWideCharArray(pNative, managedArray);
        }

        [CLSCompliant(false)]
        public static unsafe void WideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, bool bestFit, bool throwOnUnmappableChar)
        {
            PInvokeMarshal.WideCharArrayToAnsiCharArray(managedArray, pNative, bestFit, throwOnUnmappableChar);
        }

        /// <summary>
        /// Convert ANSI ByVal byte array to UNICODE wide char array, best fit
        /// </summary>
        /// <remarks>
        /// * This version works with array instead to string, it means that the len must be provided and there will be NO NULL to
        /// terminate the array.
        /// * The buffer to the UNICODE wide char array must be allocated by the caller.
        /// </remarks>
        /// <param name="pNative">Pointer to the ANSI byte array. Could NOT be null.</param>
        /// <param name="lenInBytes">Maximum buffer size.</param>
        /// <param name="managedArray">Wide char array that has already been allocated.</param>
        [CLSCompliant(false)]
        public static unsafe void AnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            PInvokeMarshal.AnsiCharArrayToWideCharArray(pNative, managedArray);
        }

        /// <summary>
        /// Convert a single UNICODE wide char to a single ANSI byte.
        /// </summary>
        /// <param name="managedArray">single UNICODE wide char value</param>
        public static unsafe byte WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar)
        {
            return PInvokeMarshal.WideCharToAnsiChar(managedValue, bestFit, throwOnUnmappableChar);
        }

        /// <summary>
        /// Convert a single ANSI byte value to a single UNICODE wide char value, best fit.
        /// </summary>
        /// <param name="nativeValue">Single ANSI byte value.</param>
        public static unsafe char AnsiCharToWideChar(byte nativeValue)
        {
            return PInvokeMarshal.AnsiCharToWideChar(nativeValue);
        }

        /// <summary>
        /// Convert UNICODE string to ANSI ByVal string.
        /// </summary>
        /// <remarks>This version is more efficient than StringToHGlobalAnsi in Interop\System\Runtime\InteropServices\Marshal.cs in that
        /// it could allocate single byte per character, instead of SystemMaxDBCSCharSize per char, and it can skip calling WideCharToMultiByte for ASCII string</remarks>
        /// <param name="str">Unicode string.</param>
        /// <param name="pNative"> Allocated buffer where the ansi string must be placed. Could NOT be null. Buffer size must fit str.Length.</param>
        [CLSCompliant(false)]
        public static unsafe void StringToByValAnsiString(string str, byte* pNative, int charCount, bool bestFit, bool throwOnUnmappableChar)
        {
            PInvokeMarshal.StringToByValAnsiString(str, pNative, charCount, bestFit, throwOnUnmappableChar);
        }

        /// <summary>
        /// Convert ANSI string to unicode string, with option to free native memory. Calls generated by MCG
        /// </summary>
        /// <remarks>Input assumed to be zero terminated. Generates String.Empty for zero length string.
        /// This version is more efficient than ConvertToUnicode in src\Interop\System\Runtime\InteropServices\Marshal.cs in that it can skip calling
        /// MultiByteToWideChar for ASCII string, and it does not need another char[] buffer</remarks>
        [CLSCompliant(false)]
        public static unsafe string ByValAnsiStringToString(byte* pchBuffer, int charCount)
        {
            return PInvokeMarshal.ByValAnsiStringToString(pchBuffer, charCount);
        }
#endif

#if ENABLE_WINRT
       
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe HSTRING StringToHString(string sourceString)
        {
            if (sourceString == null)
                throw new ArgumentNullException(nameof(sourceString), SR.Null_HString);

            return StringToHStringInternal(sourceString);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe HSTRING StringToHStringForField(string sourceString)
        {
#if !RHTESTCL
            if (sourceString == null)
                throw new MarshalDirectiveException(SR.BadMarshalField_Null_HString);
#endif
            return StringToHStringInternal(sourceString);
        }

        private static unsafe HSTRING StringToHStringInternal(string sourceString)
        {
            HSTRING ret;
            int hr = StringToHStringNoNullCheck(sourceString, &ret);
            if (hr < 0)
                throw Marshal.GetExceptionForHR(hr);

            return ret;
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static unsafe int StringToHStringNoNullCheck(string sourceString, HSTRING* hstring)
        {
            fixed (char* pChars = sourceString)
            {
                int hr = ExternalInterop.WindowsCreateString(pChars, (uint)sourceString.Length, (void*)hstring);

                return hr;
            }
        }
#endif //ENABLE_WINRT

        #endregion

        #region COM marshalling

        /// <summary>
        /// Explicit AddRef for RCWs
        /// You can't call IFoo.AddRef anymore as IFoo no longer derive from IUnknown
        /// You need to call McgMarshal.AddRef();
        /// </summary>
        /// <remarks>
        /// Used by prefast MCG plugin (mcgimportpft) only
        /// </remarks>
        [CLSCompliant(false)]
        public static int AddRef(__ComObject obj)
        {
            return obj.AddRef();
        }

        /// <summary>
        /// Explicit Release for RCWs
        /// You can't call IFoo.Release anymore as IFoo no longer derive from IUnknown
        /// You need to call McgMarshal.Release();
        /// </summary>
        /// <remarks>
        /// Used by prefast MCG plugin (mcgimportpft) only
        /// </remarks>
        [CLSCompliant(false)]
        public static int Release(__ComObject obj)
        {
            return obj.Release();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static unsafe int ComAddRef(IntPtr pComItf)
        {
            return CalliIntrinsics.StdCall__AddRef(((__com_IUnknown*)(void*)pComItf)->pVtable->
                pfnAddRef, pComItf);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static unsafe int ComRelease_StdCall(IntPtr pComItf)
        {
            return CalliIntrinsics.StdCall__Release(((__com_IUnknown*)(void*)pComItf)->pVtable->
                pfnRelease, pComItf);
        }

        /// <summary>
        /// Inline version of ComRelease
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)] //reduces MCG-generated code size
        public static unsafe int ComRelease(IntPtr pComItf)
        {
            IntPtr pRelease = ((__com_IUnknown*)(void*)pComItf)->pVtable->pfnRelease;

            // Check if the COM object is implemented by PN Interop code, for which we can call directly
            if (pRelease == AddrOfIntrinsics.AddrOf<AddrOfRelease>(__vtable_IUnknown.Release))
            {
                return __interface_ccw.DirectRelease(pComItf);
            }

            // Normal slow path, do not inline
            return ComRelease_StdCall(pComItf);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static unsafe int ComSafeRelease(IntPtr pComItf)
        {
            if (pComItf != default(IntPtr))
            {
                return ComRelease(pComItf);
            }

            return 0;
        }

        public static int FinalReleaseComObject(object o)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));

            __ComObject co = null;

            // Make sure the obj is an __ComObject.
            try
            {
                co = (__ComObject)o;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(SR.Argument_ObjNotComObject, nameof(o));
            }
            co.FinalReleaseSelf();
            return 0;
        }


        /// <summary>
        /// Returns the cached WinRT factory RCW under the current context
        /// </summary>
        [CLSCompliant(false)]
        public static unsafe __ComObject GetActivationFactory(string className, RuntimeTypeHandle factoryIntf)
        {
#if ENABLE_MIN_WINRT
            return FactoryCache.Get().GetActivationFactory(className, factoryIntf);
#else
            throw new PlatformNotSupportedException("GetActivationFactory");
#endif
        }

        /// <summary>
        /// Used by CCW infrastructure code to return the target object from this pointer
        /// </summary>
        /// <returns>The target object pointed by this pointer</returns>
        public static object ThisPointerToTargetObject(IntPtr pUnk)
        {
            return ComCallableObject.GetTarget(pUnk);
        }

        [CLSCompliant(false)]
        public static object ComInterfaceToObject_NoUnboxing(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType)
        {
            return McgComHelpers.ComInterfaceToObjectInternal(
                pComItf,
                interfaceType,
                default(RuntimeTypeHandle),
                McgComHelpers.CreateComObjectFlags.SkipTypeResolutionAndUnboxing
            );
        }

        /// <summary>
        /// Shared CCW Interface To Object
        /// </summary>
        /// <param name="pComItf"></param>
        /// <param name="interfaceType"></param>
        /// <param name="classTypeInSignature"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public static object ComInterfaceToObject(
            System.IntPtr pComItf,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature)
        {
#if ENABLE_MIN_WINRT
            if (interfaceType.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.IInspectableToObject(pComItf);
            }

            if (interfaceType.Equals(typeof(System.String).TypeHandle))
            {
                return McgMarshal.HStringToString(pComItf);
            }

            if (interfaceType.IsComClass())
            {
                RuntimeTypeHandle defaultInterface = interfaceType.GetDefaultInterface();
                Debug.Assert(!defaultInterface.IsNull());
                return ComInterfaceToObjectInternal(pComItf, defaultInterface, interfaceType);
            }
#endif
            return ComInterfaceToObjectInternal(
                pComItf,
                interfaceType,
                classTypeInSignature
            );
        }

        [CLSCompliant(false)]
        public static object ComInterfaceToObject(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType)
        {
            return ComInterfaceToObject(pComItf, interfaceType, default(RuntimeTypeHandle));
        }


        private static object ComInterfaceToObjectInternal(
            IntPtr pComItf,
            RuntimeTypeHandle interfaceType,
            RuntimeTypeHandle classTypeInSignature)
        {
            object result = McgComHelpers.ComInterfaceToObjectInternal(pComItf, interfaceType, classTypeInSignature, McgComHelpers.CreateComObjectFlags.None);

            //
            // Make sure the type we returned is actually of the right type
            // NOTE: Don't pass null to IsInstanceOfClass as it'll return false
            //
            if (!classTypeInSignature.IsNull() && result != null)
            {
                if (!InteropExtensions.IsInstanceOfClass(result, classTypeInSignature))
                    throw new InvalidCastException();
            }

            return result;
        }

        public static unsafe IntPtr ComQueryInterfaceNoThrow(IntPtr pComItf, ref Guid iid)
        {
            int hr = 0;
            return ComQueryInterfaceNoThrow(pComItf, ref iid, out hr);
        }

        public static unsafe IntPtr ComQueryInterfaceNoThrow(IntPtr pComItf, ref Guid iid, out int hr)
        {
            IntPtr pComIUnk;
            hr = ComQueryInterfaceWithHR(pComItf, ref iid, out pComIUnk);

            return pComIUnk;
        }

        internal static unsafe int ComQueryInterfaceWithHR(IntPtr pComItf, ref Guid iid, out IntPtr ppv)
        {
            IntPtr pComIUnk;
            int hr;

            fixed (Guid* unsafe_iid = &iid)
            {
                hr = CalliIntrinsics.StdCall__QueryInterface(((__com_IUnknown*)(void*)pComItf)->pVtable->
                                pfnQueryInterface,
                                pComItf,
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

        /// <summary>
        /// Helper function to copy vTable to native heap on CoreCLR.
        /// </summary>
        /// <typeparam name="T">Vtbl type</typeparam>
        /// <param name="pVtbl">static v-table field , always a valid pointer</param>
        /// <param name="pNativeVtbl">Pointer to Vtable on native heap on CoreCLR , on N it's an alias for pVtbl</param>
        public static unsafe IntPtr GetCCWVTableCopy(void* pVtbl, ref IntPtr pNativeVtbl, int size)
        {
            if (pNativeVtbl == default(IntPtr))
            {
#if CORECLR
                // On CoreCLR copy vTable to native heap , on N VTable is frozen.
                IntPtr  pv = Marshal.AllocHGlobal(size);

                int* pSrc = (int*)pVtbl;
                int* pDest = (int*)pv.ToPointer();
                int pSize = sizeof(int);

                // this should never happen , if a CCW is discarded we never get here.
                Debug.Assert(size >= pSize);
                for (int i = 0; i < size; i += pSize)
                {
                    *pDest++ = *pSrc++;
                }
                if (Interlocked.CompareExchange(ref pNativeVtbl, pv, default(IntPtr)) != default(IntPtr))
                {
                    // Another thread sneaked-in and updated pNativeVtbl , just use the update from other thread
                    Marshal.FreeHGlobal(pv);
                }
#else  // .NET NATIVE
                // Wrap it in an IntPtr
                pNativeVtbl = (IntPtr)pVtbl;
#endif // CORECLR
            }
            return pNativeVtbl;
        }

        [CLSCompliant(false)]
        public static IntPtr ObjectToComInterface(
            object obj,
            RuntimeTypeHandle typeHnd)
        {
#if ENABLE_MIN_WINRT
            if (typeHnd.Equals(typeof(object).TypeHandle))
            {
                return McgMarshal.ObjectToIInspectable(obj);
            }

            if (typeHnd.Equals(typeof(System.String).TypeHandle))
            {
                return McgMarshal.StringToHString((string)obj).handle;
            }

            if (typeHnd.IsComClass())
            {
                Debug.Assert(obj == null || obj is __ComObject);
                ///
                /// This code path should be executed only for WinRT classes
                ///
                typeHnd = typeHnd.GetDefaultInterface();
                Debug.Assert(!typeHnd.IsNull());
            }
#endif
            return McgComHelpers.ObjectToComInterfaceInternal(
                obj,
                typeHnd
            );
        }

        public static IntPtr ObjectToIInspectable(Object obj)
        {
#if ENABLE_MIN_WINRT
            return ObjectToComInterface(obj, InternalTypes.IInspectable);
#else
            throw new PlatformNotSupportedException("ObjectToIInspectable");
#endif
        }

        // This is not a safe function to use for any funtion pointers that do not point
        // at a static function. This is due to the behavior of shared generics,
        // where instance function entry points may share the exact same address
        // but static functions are always represented in delegates with customized
        // stubs.
        private static bool DelegateTargetMethodEquals(Delegate del, IntPtr pfn)
        {
            RuntimeTypeHandle thDummy;
            return del.GetFunctionPointer(out thDummy) == pfn;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr DelegateToComInterface(Delegate del, RuntimeTypeHandle typeHnd)
        {
            if (del == null)
                return default(IntPtr);

            IntPtr stubFunctionAddr = typeHnd.GetDelegateInvokeStub();

            object targetObj;

            //
            // If the delegate points to the forward stub for the native delegate,
            // then we want the RCW associated with the native interface.  Otherwise,
            // this is a managed delegate, and we want the CCW associated with it.
            //
            if (DelegateTargetMethodEquals(del, stubFunctionAddr))
                targetObj = del.Target;
            else
                targetObj = del;

            return McgMarshal.ObjectToComInterface(targetObj, typeHnd);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Delegate ComInterfaceToDelegate(IntPtr pComItf, RuntimeTypeHandle typeHnd)
        {
            if (pComItf == default(IntPtr))
                return null;

            object obj = ComInterfaceToObject(pComItf, typeHnd, /* classIndexInSignature */ default(RuntimeTypeHandle));

            //
            // If the object we got back was a managed delegate, then we're good.  Otherwise,
            // the object is an RCW for a native delegate, so we need to wrap it with a managed
            // delegate that invokes the correct stub.
            //
            Delegate del = obj as Delegate;
            if (del == null)
            {
                Debug.Assert(obj is __ComObject);
                IntPtr stubFunctionAddr = typeHnd.GetDelegateInvokeStub();

                del = InteropExtensions.CreateDelegate(
                    typeHnd,
                    stubFunctionAddr,
                    obj,
                    /*isStatic:*/ true,
                    /*isVirtual:*/ false,
                    /*isOpen:*/ false);
            }

            return del;
        }

        /// <summary>
        /// Marshal array of objects
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static void ObjectArrayToComInterfaceArray(uint len, System.IntPtr* dst, object[] src, RuntimeTypeHandle typeHnd)
        {
            for (uint i = 0; i < len; i++)
            {
                dst[i] = McgMarshal.ObjectToComInterface(src[i], typeHnd);
            }
        }

        /// <summary>
        /// Allocate native memory, and then marshal array of objects
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        unsafe public static System.IntPtr* ObjectArrayToComInterfaceArrayAlloc(object[] src, RuntimeTypeHandle typeHnd, out uint len)
        {
            System.IntPtr* dst = null;

            len = 0;

            if (src != null)
            {
                len = (uint)src.Length;

                dst = (System.IntPtr*)PInvokeMarshal.CoTaskMemAlloc((System.UIntPtr)(len * (sizeof(System.IntPtr))));

                for (uint i = 0; i < len; i++)
                {
                    dst[i] = McgMarshal.ObjectToComInterface(src[i], typeHnd);
                }
            }

            return dst;
        }

        /// <summary>
        /// Get outer IInspectable for managed object deriving from native scenario
        /// At this point the inner is not created yet - you need the outer first and pass it to the factory
        /// to create the inner
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static IntPtr GetOuterIInspectableForManagedObject(__ComObject managedObject)
        {
            ComCallableObject ccw = null;

            try
            {
                //
                // Create the CCW over the RCW
                // Note that they are actually both the same object
                // Base class = inner
                // Derived class = outer
                //
                ccw = new ComCallableObject(
                    managedObject,      // The target object              = managedObject
                    managedObject       // The inner RCW (as __ComObject) = managedObject
                );

                //
                // Retrieve the outer IInspectable
                // Pass skipInterfaceCheck = true to avoid redundant checks
                //
                return ccw.GetComInterfaceForType_NoCheck(InternalTypes.IInspectable, ref Interop.COM.IID_IInspectable);
            }
            finally
            {
                //
                // Free the extra ref count initialized by __native_ccw.Init (to protect the CCW from being collected)
                //
                if (ccw != null)
                    ccw.Release();
            }
        }

        [CLSCompliant(false)]
        public static unsafe IntPtr ManagedObjectToComInterface(Object obj, RuntimeTypeHandle interfaceType)
        {
            return McgComHelpers.ManagedObjectToComInterface(obj, interfaceType);
        }

        public static unsafe object IInspectableToObject(IntPtr pComItf)
        {
#if ENABLE_WINRT
            return ComInterfaceToObject(pComItf, InternalTypes.IInspectable);
#else
            throw new PlatformNotSupportedException("IInspectableToObject");
#endif
        }

        public static unsafe IntPtr CreateInstanceFromApp(Guid clsid)
        {
#if ENABLE_WINRT
            Interop.COM.MULTI_QI results;
            IntPtr pResults = new IntPtr(&results);
            fixed (Guid* pIID = &Interop.COM.IID_IUnknown)
            {
                Guid* pClsid = &clsid;

                results.pIID = new IntPtr(pIID);
                results.pItf = IntPtr.Zero;
                results.hr = 0;
                int hr = ExternalInterop.CoCreateInstanceFromApp(pClsid, IntPtr.Zero, 0x15 /* (CLSCTX_SERVER) */, IntPtr.Zero, 1, pResults);
                if (hr < 0)
                {
                    throw McgMarshal.GetExceptionForHR(hr, /*isWinRTScenario = */ false);
                }
                if (results.hr < 0)
                {
                    throw McgMarshal.GetExceptionForHR(results.hr, /* isWinRTScenario = */ false);
                }
                return results.pItf;
            }
#else
            throw new PlatformNotSupportedException("CreateInstanceFromApp");
#endif
        }

        #endregion

        #region Testing

        /// <summary>
        /// Internal-only method to allow testing of apartment teardown code
        /// </summary>
        public static void ReleaseRCWsInCurrentApartment()
        {
            ContextEntry.RemoveCurrentContext();
        }

        /// <summary>
        /// Used by detecting leaks
        /// Used in prefast MCG only
        /// </summary>
        public static int GetTotalComObjectCount()
        {
            return ComObjectCache.s_comObjectMap.Count;
        }

        /// <summary>
        /// Used by detecting and dumping leaks
        /// Used in prefast MCG only
        /// </summary>
        public static IEnumerable<__ComObject> GetAllComObjects()
        {
            List<__ComObject> list = new List<__ComObject>();
            for (int i = 0; i < ComObjectCache.s_comObjectMap.GetMaxCount(); ++i)
            {
                IntPtr pHandle = default(IntPtr);
                if (ComObjectCache.s_comObjectMap.GetValue(i, ref pHandle) && (pHandle != default(IntPtr)))
                {
                    GCHandle handle = GCHandle.FromIntPtr(pHandle);
                    list.Add(InteropExtensions.UncheckedCast<__ComObject>(handle.Target));
                }
            }

            return list;
        }

        #endregion

        /// <summary>
        /// This method returns HR for the exception being thrown.
        /// 1. On Windows8+, WinRT scenarios we do the following.
        ///      a. Check whether the exception has any IRestrictedErrorInfo associated with it.
        ///          If so, it means that this exception was actually caused by a native exception in which case we do simply use the same
        ///              message and stacktrace.
        ///      b.  If not, this is actually a managed exception and in this case we RoOriginateLanguageException with the msg, hresult and the IErrorInfo
        ///          aasociated with the managed exception. This helps us to retrieve the same exception in case it comes back to native.
        /// 2. On win8 and for classic COM scenarios.
        ///     a. We create IErrorInfo for the given Exception object and SetErrorInfo with the given IErrorInfo.
        /// </summary>
        /// <param name="ex"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetHRForExceptionWinRT(Exception ex)
        {
#if ENABLE_WINRT
            return ExceptionHelpers.GetHRForExceptionWithErrorPropogationNoThrow(ex, true);
#else
            // TODO : ExceptionHelpers should be platform specific , move it to
            // seperate source files
            return 0;
            //return Marshal.GetHRForException(ex);
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetHRForException(Exception ex)
        {
#if ENABLE_WINRT
            return ExceptionHelpers.GetHRForExceptionWithErrorPropogationNoThrow(ex, false);
#else
            return ex.HResult;
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowOnExternalCallFailed(int hr, System.RuntimeTypeHandle typeHnd)
        {
            bool isWinRTScenario
#if ENABLE_WINRT
            = typeHnd.IsSupportIInspectable();
#else
            = false;
#endif
            throw McgMarshal.GetExceptionForHR(hr, isWinRTScenario);
        }

        /// <summary>
        /// This method returns a new Exception object given the HR value.
        /// </summary>
        /// <param name="hr"></param>
        /// <param name="isWinRTScenario"></param>
        public static Exception GetExceptionForHR(int hr, bool isWinRTScenario)
        {
#if ENABLE_WINRT
            return ExceptionHelpers.GetExceptionForHRInternalNoThrow(hr, isWinRTScenario, !isWinRTScenario);
#elif CORECLR
            return Marshal.GetExceptionForHR(hr);
#else
            return new COMException(hr.ToString(), hr);
#endif
        }

        #region Shared templates
#if ENABLE_MIN_WINRT
        public static void CleanupNative<T>(IntPtr pObject)
        {
            if (typeof(T) == typeof(string))
            {
                global::System.Runtime.InteropServices.McgMarshal.FreeHString(pObject);
            }
            else
            {
                global::System.Runtime.InteropServices.McgMarshal.ComSafeRelease(pObject);
            }
        }
#endif
        #endregion

#if ENABLE_MIN_WINRT
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static unsafe IntPtr ActivateInstance(string typeName)
        {
            __ComObject target = McgMarshal.GetActivationFactory(
                typeName,
                InternalTypes.IActivationFactoryInternal
            );

            IntPtr pIActivationFactoryInternalItf = target.QueryInterface_NoAddRef_Internal(
                InternalTypes.IActivationFactoryInternal,
                /* cacheOnly= */ false,
                /* throwOnQueryInterfaceFailure= */ true
            );

            __com_IActivationFactoryInternal* pIActivationFactoryInternal = (__com_IActivationFactoryInternal*)pIActivationFactoryInternalItf;

            IntPtr pResult = default(IntPtr);

            int hr = CalliIntrinsics.StdCall<int>(
                pIActivationFactoryInternal->pVtable->pfnActivateInstance,
                pIActivationFactoryInternal,
                &pResult
            );

            GC.KeepAlive(target);

            if (hr < 0)
            {
                throw McgMarshal.GetExceptionForHR(hr, /* isWinRTScenario = */ true);
            }

            return pResult;
        }
#endif

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr GetInterface(
            __ComObject obj,
            RuntimeTypeHandle typeHnd)
        {
            return obj.QueryInterface_NoAddRef_Internal(
                typeHnd);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static object GetDynamicAdapter(__ComObject obj, RuntimeTypeHandle requestedType, RuntimeTypeHandle existingType)
        {
            return obj.GetDynamicAdapter(requestedType, existingType);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static object GetDynamicAdapter(__ComObject obj, RuntimeTypeHandle requestedType)
        {
            return obj.GetDynamicAdapter(requestedType, default(RuntimeTypeHandle));
        }

        #region "PInvoke Delegate"

        public static IntPtr GetStubForPInvokeDelegate(RuntimeTypeHandle delegateType, Delegate dele)
        {
#if CORECLR
             throw new NotSupportedException();
#else
            return PInvokeMarshal.GetStubForPInvokeDelegate(dele);
#endif
        }

        /// <summary>
        /// Retrieve the corresponding P/invoke instance from the stub
        /// </summary>
        public static Delegate GetPInvokeDelegateForStub(IntPtr pStub, RuntimeTypeHandle delegateType)
        {
#if CORECLR
            if (pStub == IntPtr.Zero)
                return null;

            McgPInvokeDelegateData pInvokeDelegateData;
            if (!McgModuleManager.GetPInvokeDelegateData(delegateType, out pInvokeDelegateData))
            {
                return null;
            }

            return CalliIntrinsics.Call__Delegate(
                pInvokeDelegateData.ForwardDelegateCreationStub,
                pStub
            );
#else
            return PInvokeMarshal.GetPInvokeDelegateForStub(pStub, delegateType);
#endif
        }

        /// <summary>
        /// Retrieves the function pointer for the current open static delegate that is being called
        /// </summary>
        public static IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
#if RHTESTCL || CORECLR || CORERT
            throw new NotSupportedException();
#else
            return PInvokeMarshal.GetCurrentCalleeOpenStaticDelegateFunctionPointer();
#endif
        }

        /// <summary>
        /// Retrieves the current delegate that is being called
        /// </summary>
        public static T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
#if RHTESTCL || CORECLR || CORERT
            throw new NotSupportedException();
#else
            return PInvokeMarshal.GetCurrentCalleeDelegate<T>();
#endif
        }
#endregion
    }

    /// <summary>
    /// McgMarshal helpers exposed to be used by MCG
    /// </summary>
    public static partial class McgMarshal
    {
        public static object UnboxIfBoxed(object target)
        {
            return UnboxIfBoxed(target, null);
        }

        public static object UnboxIfBoxed(object target, string className)
        {
            //
            // If it is a managed wrapper, unbox it
            //
            object unboxedObj = McgComHelpers.UnboxManagedWrapperIfBoxed(target);
            if (unboxedObj != target)
                return unboxedObj;

            if (className == null)
                className = System.Runtime.InteropServices.McgComHelpers.GetRuntimeClassName(target);

            if (!String.IsNullOrEmpty(className))
            {
                IntPtr unboxingStub;
                if (McgModuleManager.TryGetUnboxingStub(className, out unboxingStub))
                {
                    object ret = CalliIntrinsics.Call<object>(unboxingStub, target);

                    if (ret != null)
                        return ret;
                }
#if ENABLE_WINRT
                else if(McgModuleManager.UseDynamicInterop)
                {
                    BoxingInterfaceKind boxingInterfaceKind;
                    RuntimeTypeHandle genericTypeArgument;
                    if (DynamicInteropBoxingHelpers.TryGetBoxingArgumentTypeHandleFromString(className, out boxingInterfaceKind, out genericTypeArgument))
                    {
                        Debug.Assert(target is __ComObject);
                        return DynamicInteropBoxingHelpers.Unboxing(boxingInterfaceKind, genericTypeArgument, target);
                    }
                }
#endif
            }
            return null;
        }

        internal static object BoxIfBoxable(object target)
        {
            return BoxIfBoxable(target, default(RuntimeTypeHandle));
        }

        /// <summary>
        /// Given a boxed value type, return a wrapper supports the IReference interface
        /// </summary>
        /// <param name="typeHandleOverride">
        /// You might want to specify how to box this. For example, any object[] derived array could
        /// potentially boxed as object[] if everything else fails
        /// </param>
        internal static object BoxIfBoxable(object target, RuntimeTypeHandle typeHandleOverride)
        {
            RuntimeTypeHandle expectedTypeHandle = typeHandleOverride;
            if (expectedTypeHandle.Equals(default(RuntimeTypeHandle)))
                expectedTypeHandle = target.GetTypeHandle();

            RuntimeTypeHandle boxingWrapperType;
            IntPtr boxingStub;
            int boxingPropertyType;
            if (McgModuleManager.TryGetBoxingWrapperType(expectedTypeHandle, target, out boxingWrapperType, out boxingPropertyType, out boxingStub))
            {
                if (!boxingWrapperType.IsInvalid())
                {
                    //
                    // IReference<T> / IReferenceArray<T> / IKeyValuePair<K, V>
                    // All these scenarios require a managed wrapper
                    //

                    // Allocate the object
                    object refImplType = InteropExtensions.RuntimeNewObject(boxingWrapperType);

                    if (boxingPropertyType >= 0)
                    {
                        Debug.Assert(refImplType is BoxedValue);

                        BoxedValue boxed = InteropExtensions.UncheckedCast<BoxedValue>(refImplType);

                        // Call ReferenceImpl<T>.Initialize(obj, type);
                        boxed.Initialize(target, boxingPropertyType);
                    }
                    else
                    {
                        Debug.Assert(refImplType is BoxedKeyValuePair);

                        BoxedKeyValuePair boxed = InteropExtensions.UncheckedCast<BoxedKeyValuePair>(refImplType);

                        // IKeyValuePair<,>,   call CLRIKeyValuePairImpl<K,V>.Initialize(object obj);
                        // IKeyValuePair<,>[], call CLRIKeyValuePairArrayImpl<K,V>.Initialize(object obj);
                        refImplType = boxed.Initialize(target);
                    }

                    return refImplType;
                }
                else
                {
                    //
                    // General boxing for projected types, such as System.Uri
                    //
                    return CalliIntrinsics.Call<object>(boxingStub, target);
                }
            }

            return null;
        }
    }
}
