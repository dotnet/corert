// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    /// <summary>
    ///     Hooks for System.Private.Interop.dll code to access internal functionality in System.Private.CoreLib.dll.
    ///     
    ///     Methods added to InteropExtensions should also be added to the System.Private.CoreLib.InteropServices contract
    ///     in order to be accessible from System.Private.Interop.dll.
    /// </summary>
    [CLSCompliant(false)]
    public static class InteropExtensions
    {
        // Converts a managed DateTime to native OLE datetime
        // Used by MCG marshalling code
        public static double ToNativeOleDate(DateTime dateTime)
        {
            return dateTime.ToOADate();
        }

        // Converts native OLE datetime to managed DateTime
        // Used by MCG marshalling code
        public static DateTime FromNativeOleDate(double nativeOleDate)
        {
            return new DateTime(DateTime.DoubleDateToTicks(nativeOleDate));
        }

        // Used by MCG's SafeHandle marshalling code to initialize a handle
        public static void InitializeHandle(SafeHandle safeHandle, IntPtr win32Handle)
        {
            safeHandle.InitializeHandle(win32Handle);
        }

        // Used for methods in System.Private.Interop.dll that need to work from offsets on boxed structs
        public static unsafe void PinObjectAndCall(Object obj, Action<IntPtr> del)
        {
            fixed (IntPtr* pEEType = &obj.m_pEEType)
            {
                del((IntPtr)pEEType);
            }
        }

        public static int GetElementSize(this Array array)
        {
            return array.EETypePtr.ComponentSize;
        }

        internal static bool MightBeBlittable(this EETypePtr eeType)
        {
            //
            // This is used as the approximate implementation of MethodTable::IsBlittable(). It  will err in the direction of declaring
            // things blittable since it is used for argument validation only.
            //
            return !eeType.HasPointers;
        }

        public static bool IsBlittable(this RuntimeTypeHandle handle)
        {
            return handle.ToEETypePtr().MightBeBlittable();
        }

        public static bool IsBlittable(this Object obj)
        {
            return obj.EETypePtr.MightBeBlittable();
        }

        public static bool IsGenericType(this RuntimeTypeHandle handle)
        {
            EETypePtr eeType = handle.ToEETypePtr();
            return eeType.IsGeneric;
        }

        public static bool IsGenericTypeDefinition(this RuntimeTypeHandle handle)
        {
            EETypePtr eeType = handle.ToEETypePtr();
            return eeType.IsGenericTypeDefinition;
        }

        public static unsafe int GetGenericArgumentCount(this RuntimeTypeHandle genericTypeDefinitionHandle)
        {
            Debug.Assert(IsGenericTypeDefinition(genericTypeDefinitionHandle));
            return genericTypeDefinitionHandle.ToEETypePtr().ToPointer()->GenericArgumentCount;
        }

        //TODO:Remove Delegate.GetNativeFunctionPointer
        public static IntPtr GetNativeFunctionPointer(this Delegate del)
        {
            return del.GetNativeFunctionPointer();
        }
        public static IntPtr GetFunctionPointer(this Delegate del, out RuntimeTypeHandle typeOfFirstParameterIfInstanceDelegate)
        {
            return del.GetFunctionPointer(out typeOfFirstParameterIfInstanceDelegate, out bool _, out bool _);
        }

        //
        // Returns the raw function pointer for a open static delegate - if the function has a jump stub 
        // it returns the jump target. Therefore the function pointer returned
        // by two delegates may NOT be unique
        //
        public static IntPtr GetRawFunctionPointerForOpenStaticDelegate(this Delegate del)
        {
            //If it is not open static then return IntPtr.Zero
            if (!del.IsOpenStatic)
                return IntPtr.Zero;

            RuntimeTypeHandle typeOfFirstParameterIfInstanceDelegate;

            IntPtr funcPtr = del.GetFunctionPointer(out typeOfFirstParameterIfInstanceDelegate, out bool _, out bool _);

            // if the function pointer points to a jump stub return the target
            return RuntimeImports.RhGetJmpStubCodeTarget(funcPtr);
        }

        public static IntPtr GetRawValue(this RuntimeTypeHandle handle)
        {
            return handle.RawValue;
        }

        /// <summary>
        /// Comparing RuntimeTypeHandle with an object's RuntimeTypeHandle, avoiding going through expensive Object.GetType().TypeHandle path
        /// </summary>
        public static bool IsOfType(this Object obj, RuntimeTypeHandle handle)
        {
            RuntimeTypeHandle objType = new RuntimeTypeHandle(obj.EETypePtr);

            return handle.Equals(objType);
        }

        public static bool IsNull(this RuntimeTypeHandle handle)
        {
            return handle.IsNull;
        }

        public static Type GetTypeFromHandle(IntPtr typeHandle)
        {
            return Type.GetTypeFromHandle(new RuntimeTypeHandle(new EETypePtr(typeHandle)));
        }

        public static Type GetTypeFromHandle(RuntimeTypeHandle typeHandle)
        {
            return Type.GetTypeFromHandle(typeHandle);
        }

        public static int GetValueTypeSize(this RuntimeTypeHandle handle)
        {
            return (int)handle.ToEETypePtr().ValueTypeSize;
        }

        public static bool IsValueType(this RuntimeTypeHandle handle)
        {
            return handle.ToEETypePtr().IsValueType;
        }

        public static bool IsClass(this RuntimeTypeHandle handle)
        {
            return handle.ToEETypePtr().IsDefType && !handle.ToEETypePtr().IsInterface && !handle.ToEETypePtr().IsValueType && !handle.IsDelegate();
        }

        public static bool IsEnum(this RuntimeTypeHandle handle)
        {
            return handle.ToEETypePtr().IsEnum;
        }

        public static bool IsInterface(this RuntimeTypeHandle handle)
        {
            return handle.ToEETypePtr().IsInterface;
        }

        public static bool IsPrimitive(this RuntimeTypeHandle handle)
        {
            return handle.ToEETypePtr().IsPrimitive;
        }

        public static bool IsDelegate(this RuntimeTypeHandle handle)
        {
            return InteropExtensions.AreTypesAssignable(handle, typeof(MulticastDelegate).TypeHandle) ||
                InteropExtensions.AreTypesAssignable(handle, typeof(Delegate).TypeHandle);
        }

        public static bool AreTypesAssignable(RuntimeTypeHandle sourceType, RuntimeTypeHandle targetType)
        {
            return RuntimeImports.AreTypesAssignable(sourceType.ToEETypePtr(), targetType.ToEETypePtr());
        }

        public static unsafe void Memcpy(IntPtr destination, IntPtr source, int bytesToCopy)
        {
            Buffer.Memmove((byte*)destination, (byte*)source, (uint)bytesToCopy);
        }

        public static bool RuntimeRegisterGcCalloutForGCStart(IntPtr pCalloutMethod)
        {
            return RuntimeImports.RhRegisterGcCallout(RuntimeImports.GcRestrictedCalloutKind.StartCollection, pCalloutMethod);
        }

        public static bool RuntimeRegisterGcCalloutForGCEnd(IntPtr pCalloutMethod)
        {
            return RuntimeImports.RhRegisterGcCallout(RuntimeImports.GcRestrictedCalloutKind.EndCollection, pCalloutMethod);
        }

        public static bool RuntimeRegisterGcCalloutForAfterMarkPhase(IntPtr pCalloutMethod)
        {
            return RuntimeImports.RhRegisterGcCallout(RuntimeImports.GcRestrictedCalloutKind.AfterMarkPhase, pCalloutMethod);
        }

        public static bool RuntimeRegisterRefCountedHandleCallback(IntPtr pCalloutMethod, RuntimeTypeHandle pTypeFilter)
        {
            return RuntimeImports.RhRegisterRefCountedHandleCallback(pCalloutMethod, pTypeFilter.ToEETypePtr());
        }

        public static void RuntimeUnregisterRefCountedHandleCallback(IntPtr pCalloutMethod, RuntimeTypeHandle pTypeFilter)
        {
            RuntimeImports.RhUnregisterRefCountedHandleCallback(pCalloutMethod, pTypeFilter.ToEETypePtr());
        }

        /// <summary>
        /// The type of a RefCounted handle
        /// A ref-counted handle is a handle that acts as strong if the callback returns true, and acts as 
        /// weak handle if the callback returns false, which is perfect for controlling lifetime of a CCW
        /// </summary>
        internal const int RefCountedHandleType = 5;
        public static IntPtr RuntimeHandleAllocRefCounted(Object value)
        {
            return RuntimeImports.RhHandleAlloc(value, (GCHandleType)RefCountedHandleType);
        }

        public static void RuntimeHandleSet(IntPtr handle, Object value)
        {
            RuntimeImports.RhHandleSet(handle, value);
        }

        public static void RuntimeHandleFree(IntPtr handle)
        {
            RuntimeImports.RhHandleFree(handle);
        }

        public static IntPtr RuntimeHandleAllocDependent(object primary, object secondary)
        {
            return RuntimeImports.RhHandleAllocDependent(primary, secondary);
        }

        public static bool RuntimeIsPromoted(object obj)
        {
            return RuntimeImports.RhIsPromoted(obj);
        }

        public static void RuntimeHandleSetDependentSecondary(IntPtr handle, Object secondary)
        {
            RuntimeImports.RhHandleSetDependentSecondary(handle, secondary);
        }

        public static T UncheckedCast<T>(object obj) where T : class
        {
            return Unsafe.As<T>(obj);
        }

        public static bool IsArray(RuntimeTypeHandle type)
        {
            return type.ToEETypePtr().IsArray;
        }

        public static RuntimeTypeHandle GetArrayElementType(RuntimeTypeHandle arrayType)
        {
            return new RuntimeTypeHandle(arrayType.ToEETypePtr().ArrayElementType);
        }

        /// <summary>
        /// Whether the type is a single dimension zero lower bound array
        /// </summary>
        /// <param name="type">specified type</param>
        /// <returns>true iff it is a single dimension zeo lower bound array</returns>
        public static bool IsSzArray(RuntimeTypeHandle type)
        {
            return type.ToEETypePtr().IsSzArray;
        }

        public static RuntimeTypeHandle GetTypeHandle(this object target)
        {
            return new RuntimeTypeHandle(target.EETypePtr);
        }

        public static bool IsInstanceOf(object obj, RuntimeTypeHandle typeHandle)
        {
            return (null != RuntimeImports.IsInstanceOf(obj, typeHandle.ToEETypePtr()));
        }

        public static bool IsInstanceOfClass(object obj, RuntimeTypeHandle classTypeHandle)
        {
            return (null != RuntimeImports.IsInstanceOfClass(obj, classTypeHandle.ToEETypePtr()));
        }

        public static bool IsInstanceOfInterface(object obj, RuntimeTypeHandle interfaceTypeHandle)
        {
            return (null != RuntimeImports.IsInstanceOfInterface(obj, interfaceTypeHandle.ToEETypePtr()));
        }

        public static bool GuidEquals(ref Guid left, ref Guid right)
        {
            return left.Equals(ref right);
        }

        public static bool ComparerEquals<T>(T left, T right)
        {
            return EqualOnlyComparer<T>.Equals(left, right);
        }

        public static object RuntimeNewObject(RuntimeTypeHandle typeHnd)
        {
            return RuntimeImports.RhNewObject(typeHnd.ToEETypePtr());
        }

        public static unsafe void UnsafeCopyTo(this System.Text.StringBuilder stringBuilder, char* destination)
        {
            stringBuilder.UnsafeCopyTo(destination);
        }

        public static unsafe void ReplaceBuffer(this System.Text.StringBuilder stringBuilder, char* newBuffer)
        {
            stringBuilder.ReplaceBuffer(newBuffer);
        }

        public static void ReplaceBuffer(this System.Text.StringBuilder stringBuilder, char[] newBuffer)
        {
            stringBuilder.ReplaceBuffer(newBuffer);
        }

        public static char[] GetBuffer(this System.Text.StringBuilder stringBuilder, out int len)
        {
            return stringBuilder.GetBuffer(out len);
        }

        public static IntPtr RuntimeHandleAllocVariable(Object value, uint type)
        {
            return RuntimeImports.RhHandleAllocVariable(value, type);
        }

        public static uint RuntimeHandleGetVariableType(IntPtr handle)
        {
            return RuntimeImports.RhHandleGetVariableType(handle);
        }

        public static void RuntimeHandleSetVariableType(IntPtr handle, uint type)
        {
            RuntimeImports.RhHandleSetVariableType(handle, type);
        }

        public static uint RuntimeHandleCompareExchangeVariableType(IntPtr handle, uint oldType, uint newType)
        {
            return RuntimeImports.RhHandleCompareExchangeVariableType(handle, oldType, newType);
        }

        public static void SetExceptionErrorCode(Exception exception, int hr)
        {
            exception.SetErrorCode(hr);
        }

        public static void SetExceptionMessage(Exception exception, string message)
        {
            exception.SetMessage(message);
        }

        public static Exception CreateDataMisalignedException(string message)
        {
            return new DataMisalignedException(message);
        }

        public static Delegate CreateDelegate(RuntimeTypeHandle typeHandleForDelegate, IntPtr ldftnResult, Object thisObject, bool isStatic, bool isVirtual, bool isOpen)
        {
            return Delegate.CreateDelegate(typeHandleForDelegate.ToEETypePtr(), ldftnResult, thisObject, isStatic, isOpen);
        }

        public enum VariableHandleType
        {
            WeakShort = 0x00000100,
            WeakLong = 0x00000200,
            Strong = 0x00000400,
            Pinned = 0x00000800,
        }

        public static void AddExceptionDataForRestrictedErrorInfo(Exception ex, string restrictedError, string restrictedErrorReference, string restrictedCapabilitySid, object restrictedErrorObject)
        {
            ex.AddExceptionDataForRestrictedErrorInfo(restrictedError, restrictedErrorReference, restrictedCapabilitySid, restrictedErrorObject);
        }

        public static bool TryGetRestrictedErrorObject(Exception ex, out object restrictedErrorObject)
        {
            return ex.TryGetRestrictedErrorObject(out restrictedErrorObject);
        }

        public static bool TryGetRestrictedErrorDetails(Exception ex, out string restrictedError, out string restrictedErrorReference, out string restrictedCapabilitySid)
        {
            return ex.TryGetRestrictedErrorDetails(out restrictedError, out restrictedErrorReference, out restrictedCapabilitySid);
        }

        public static TypeInitializationException CreateTypeInitializationException(string message)
        {
            return new TypeInitializationException(message);
        }

        public static unsafe IntPtr GetObjectID(object obj)
        {
            fixed (void* p = &obj.m_pEEType)
            {
                return (IntPtr)p;
            }
        }

        public static bool RhpETWShouldWalkCom()
        {
            return RuntimeImports.RhpETWShouldWalkCom();
        }

        public static void RhpETWLogLiveCom(int eventType, IntPtr CCWHandle, IntPtr objectID, IntPtr typeRawValue, IntPtr IUnknown, IntPtr VTable, Int32 comRefCount, Int32 jupiterRefCount, Int32 flags)
        {
            RuntimeImports.RhpETWLogLiveCom(eventType, CCWHandle, objectID, typeRawValue, IUnknown, VTable, comRefCount, jupiterRefCount, flags);
        }

        public static bool SupportsReflection(this Type type)
        {
            return RuntimeAugments.Callbacks.SupportsReflection(type);
        }

        public static void SuppressReentrantWaits()
        {
            RuntimeThread.SuppressReentrantWaits();
        }

        public static void RestoreReentrantWaits()
        {
            RuntimeThread.RestoreReentrantWaits();
        }

        public static IntPtr GetCriticalHandle(CriticalHandle criticalHandle)
        {
            return criticalHandle.GetHandleInternal();
        }

        public static void SetCriticalHandle(CriticalHandle criticalHandle, IntPtr handle)
        {
            criticalHandle.SetHandleInternal(handle);
        }
    }
}
