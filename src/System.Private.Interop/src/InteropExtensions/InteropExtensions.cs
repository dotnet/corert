// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Runtime;
using System.Reflection;

namespace System.Runtime.InteropServices
{
    /// <summary>
    ///     Hooks for System.Private.Interop.dll code to access internal functionality in System.Private.CoreLib.dll.
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
            return new DateTime(nativeOleDate.DoubleDateToTicks());
        }

        // Used by MCG's SafeHandle marshalling code to initialize a handle
        public static void InitializeHandle(SafeHandle safeHandle, IntPtr win32Handle)
        {
            // We need private reflection here to access the handle field.
            FieldInfo fieldInfo = safeHandle.GetType().GetField("handle", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(safeHandle, win32Handle);
        }

        // Used for methods in System.Private.Interop.dll that need to work from offsets on boxed structs
        public unsafe static void PinObjectAndCall(Object obj, Action<IntPtr> del)
        {
            throw new NotSupportedException("PinObjectAndCall");
        }

        public static void CopyToManaged(IntPtr source, Array destination, int startIndex, int length)
        {
            throw new NotSupportedException("CopyToManaged");
        }

        public static void CopyToNative(Array array, int startIndex, IntPtr destination, int length)
        {
            throw new NotSupportedException("CopyToNative");
        }

        public static int GetElementSize(this Array array)
        {
            throw new NotSupportedException("GetElementSize");
        }

        public static bool IsBlittable(this RuntimeTypeHandle handle)
        {
            throw new NotSupportedException("IsBlittable");
        }

        public static bool IsElementTypeBlittable(this Array array)
        {
            throw new NotSupportedException("IsElementTypeBlittable");
        }

        public static bool IsGenericType(this RuntimeTypeHandle handle)
        {
            throw new NotSupportedException("IsGenericType");
        }


        public static bool IsClass(RuntimeTypeHandle handle)
        {
            return Type.GetTypeFromHandle(handle).GetTypeInfo().IsClass;
        }

        public static IntPtr GetNativeFunctionPointer(this Delegate del)
        {
            throw new PlatformNotSupportedException("GetNativeFunctionPointer");
        }
        public static IntPtr GetFunctionPointer(this Delegate del, out RuntimeTypeHandle typeOfFirstParameterIfInstanceDelegate)
        {
            // Note this work only for non-static methods , for static methods 
            // _methodPtr points to a stub that remove the this pointer and 
            // _methodPtrAux points actual pointer.
            typeOfFirstParameterIfInstanceDelegate = default(RuntimeTypeHandle);
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = del.GetType().GetField("_methodPtr", bindFlags);
            return (IntPtr)field.GetValue(del);
        }

        public static IntPtr GetRawFunctionPointerForOpenStaticDelegate(this Delegate del)
        {
            throw new NotSupportedException("GetRawFunctionPointerForOpenStaticDelegate");
        }

        public static IntPtr GetRawValue(this RuntimeTypeHandle handle)
        {
            throw new NotSupportedException("GetRawValue");
        }
      
        public static bool IsOfType(this Object obj, RuntimeTypeHandle handle)
        {
            return obj.GetType() == Type.GetTypeFromHandle(handle);
        }

        public static bool IsNull(this RuntimeTypeHandle handle)
        {
            return handle.Equals(default(RuntimeTypeHandle));
        }

        public static Type GetTypeFromHandle(IntPtr typeHandle)
        {
            throw new NotSupportedException("GetTypeFromHandle(IntPtr)");
        }

        public static Type GetTypeFromHandle(RuntimeTypeHandle typeHandle)
        {
            return Type.GetTypeFromHandle(typeHandle);
        }

        public static int GetValueTypeSize(this RuntimeTypeHandle handle)
        {
            throw new NotSupportedException("GetValueTypeSize");
        }

        public static bool IsValueType(this RuntimeTypeHandle handle)
        {
            return Type.GetTypeFromHandle(handle).GetTypeInfo().IsValueType;
        }

        public static bool IsEnum(this RuntimeTypeHandle handle)
        {
            return Type.GetTypeFromHandle(handle).GetTypeInfo().IsEnum;
        }

        public static bool AreTypesAssignable(RuntimeTypeHandle sourceHandle, RuntimeTypeHandle targetHandle)
        {
            Type srcType = Type.GetTypeFromHandle(sourceHandle);
            Type targetType = Type.GetTypeFromHandle(targetHandle);
            return targetType.IsAssignableFrom(srcType);
        }

        public static unsafe void Memcpy(IntPtr destination, IntPtr source, int bytesToCopy)
        {
            throw new NotSupportedException("Memcpy");
        }

        public static bool RuntimeRegisterGcCalloutForGCStart(IntPtr pCalloutMethod)
        {
            //Nop
            return true;
        }

        public static bool RuntimeRegisterGcCalloutForGCEnd(IntPtr pCalloutMethod)
        {
            //Nop
            return true;
        }

        public static bool RuntimeRegisterGcCalloutForAfterMarkPhase(IntPtr pCalloutMethod)
        {
            //Nop
            return true;
        }

        public static bool RuntimeRegisterRefCountedHandleCallback(IntPtr pCalloutMethod, RuntimeTypeHandle pTypeFilter)
        {
            // Nop 
            return true;
        }

        public static void RuntimeUnregisterRefCountedHandleCallback(IntPtr pCalloutMethod, RuntimeTypeHandle pTypeFilter)
        {
            //Nop 
        }
        public static IntPtr RuntimeHandleAllocRefCounted(Object value)
        {
            return GCHandle.ToIntPtr( GCHandle.Alloc(value, GCHandleType.Normal));
        }

        public static void RuntimeHandleSet(IntPtr handle, Object value)
        {
            GCHandle gcHandle = GCHandle.FromIntPtr(handle);
            gcHandle.Target = value;
        }

        public static void RuntimeHandleFree(IntPtr handlePtr)
        {
            GCHandle handle = GCHandle.FromIntPtr(handlePtr);
            handle.Free();
        }

        public static IntPtr RuntimeHandleAllocDependent(object primary, object secondary)
        {
            throw new NotSupportedException("RuntimeHandleAllocDependent");
        }

        public static bool RuntimeIsPromoted(object obj)
        {
            throw new NotSupportedException("RuntimeIsPromoted");
        }

        public static void RuntimeHandleSetDependentSecondary(IntPtr handle, Object secondary)
        {
            throw new NotSupportedException("RuntimeHandleSetDependentSecondary");
        }

        public static T UncheckedCast<T>(object obj) where T : class
        {
            return obj as T;
        }

        public static bool IsArray(RuntimeTypeHandle type)
        {
            throw new NotSupportedException("IsArray");
        }

        public static RuntimeTypeHandle GetArrayElementType(RuntimeTypeHandle arrayType)
        {
            throw new NotSupportedException("GetArrayElementType");
        }

        public static RuntimeTypeHandle GetTypeHandle(this object target)
        {
            Type type = target.GetType();
            return type.TypeHandle;
        }

        public static bool IsInstanceOf(object obj, RuntimeTypeHandle typeHandle)
        {
            Type type =  Type.GetTypeFromHandle(typeHandle);
            return type.IsInstanceOfType(obj);
        }

        public static bool IsInstanceOfClass(object obj, RuntimeTypeHandle classTypeHandle)
        {
            return obj.GetType() == Type.GetTypeFromHandle(classTypeHandle);
        }

        public static bool IsInstanceOfInterface(object obj, RuntimeTypeHandle interfaceTypeHandle)
        {
            Type interfaceType = Type.GetTypeFromHandle(interfaceTypeHandle);
            return TypeExtensions.IsInstanceOfType(interfaceType, obj);
        }

        public static bool GuidEquals(ref Guid left, ref Guid right)
        {
            return left.Equals(right);
        }

        public static bool ComparerEquals<T>(T left, T right)
        {
            throw new NotSupportedException("ComparerEquals");
        }

        public static object RuntimeNewObject(RuntimeTypeHandle typeHnd)
        {
            Func<Type, object> getUninitializedObjectDelegate = 
                                                    (Func<Type, object>)
                                                     typeof(string)
                                                      .GetTypeInfo()
                                                     .Assembly
                                                     .GetType("System.Runtime.Serialization.FormatterServices")
                                                     ?.GetMethod("GetUninitializedObject", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                                                     ?.CreateDelegate(typeof(Func<Type, object>));

            return getUninitializedObjectDelegate.Invoke(Type.GetTypeFromHandle(typeHnd));
        }

        internal static unsafe int wcslen(char* ptr)
        {
            char* end = ptr;

            // The following code is (somewhat surprisingly!) significantly faster than a naive loop,
            // at least on x86 and the current jit.

            // First make sure our pointer is aligned on a dword boundary
            while (((uint)end & 3) != 0 && *end != 0)
                end++;
            if (*end != 0)
            {
                // The loop condition below works because if "end[0] & end[1]" is non-zero, that means
                // neither operand can have been zero. If is zero, we have to look at the operands individually,
                // but we hope this going to fairly rare.

                // In general, it would be incorrect to access end[1] if we haven't made sure
                // end[0] is non-zero. However, we know the ptr has been aligned by the loop above
                // so end[0] and end[1] must be in the same page, so they're either both accessible, or both not.

                while ((end[0] & end[1]) != 0 || (end[0] != 0 && end[1] != 0))
                {
                    end += 2;
                }
            }
            // finish up with the naive loop
            for (; *end != 0; end++)
                ;

            int count = (int)(end - ptr);

            return count;
        }

        /// <summary>
        /// Copy StringBuilder's contents to the char*, and appending a '\0'
        /// The destination buffer must be big enough, and include space for '\0'
        /// NOTE: There is no guarantee the destination pointer has enough size, but we have no choice
        /// because the pointer might come from native code.
        /// </summary>
        /// <param name="stringBuilder"></param>
        /// <param name="destination"></param>
        public static unsafe void UnsafeCopyTo(this System.Text.StringBuilder stringBuilder, char* destination)
        {
            for (int i = 0; i < stringBuilder.Length; i++)
            {
                destination[i] = stringBuilder[i];
            }
            destination[stringBuilder.Length] = '\0';
        }

        public static unsafe void ReplaceBuffer(this System.Text.StringBuilder stringBuilder, char* newBuffer)
        {
            // Mimic N stringbuilder replacebuffer behaviour.wcslen assume newBuffer to be '\0' terminated.
            int len = wcslen(newBuffer);
            stringBuilder.Clear();
            // the '+1' is for back-compat with desktop CLR in terms of length calculation because desktop
            // CLR had '\0'
            stringBuilder.EnsureCapacity(len + 1);
            stringBuilder.Append(newBuffer, len);
        }

        public static void ReplaceBuffer(this System.Text.StringBuilder stringBuilder, char[] newBuffer)
        {
            // mimic N stringbuilder replacebuffer behaviour. this's safe to do since we know the 
            // length of newBuffer.
            stringBuilder.Clear();
            // the '+1' is for back-compat with desktop CLR in terms of length calculation because desktop
            // CLR had '\0'
            stringBuilder.EnsureCapacity(newBuffer.Length + 1);
            stringBuilder.Append(newBuffer);
        }

        public static char[] GetBuffer(this System.Text.StringBuilder stringBuilder, out int len)
        {
            return stringBuilder.GetBuffer(out len);
        }

        public static IntPtr RuntimeHandleAllocVariable(Object value, uint type)
        {
            throw new NotSupportedException("RuntimeHandleAllocVariable");
        }

        public static uint RuntimeHandleGetVariableType(IntPtr handle)
        {
            throw new NotSupportedException("RuntimeHandleGetVariableType");
        }

        public static void RuntimeHandleSetVariableType(IntPtr handle, uint type)
        {
            throw new NotSupportedException("RuntimeHandleSetVariableType");
        }

        public static uint RuntimeHandleCompareExchangeVariableType(IntPtr handle, uint oldType, uint newType)
        {
            throw new NotSupportedException("RuntimeHandleCompareExchangeVariableType");
        }
        
        public static void SetExceptionErrorCode(Exception exception, int hr)
        {
            throw new NotSupportedException("SetExceptionErrorCode");
        }

        public static Exception CreateDataMisalignedException(string message)
        {
            return new DataMisalignedException(message);
        }

        public static Delegate CreateDelegate(RuntimeTypeHandle typeHandleForDelegate, IntPtr ldftnResult, Object thisObject, bool isStatic, bool isVirtual, bool isOpen)
        {
            throw new NotSupportedException("CreateDelegate");
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
            throw new NotSupportedException("AddExceptionDataForRestrictedErrorInfo");
        }

        public static bool TryGetRestrictedErrorObject(Exception ex, out object restrictedErrorObject)
        {
            throw new NotSupportedException("TryGetRestrictedErrorObject");
        }

        public static bool TryGetRestrictedErrorDetails(Exception ex, out string restrictedError, out string restrictedErrorReference, out string restrictedCapabilitySid)
        {
            throw new NotSupportedException("TryGetRestrictedErrorDetails");
        }

        public static TypeInitializationException CreateTypeInitializationException(string message)
        {
            return new TypeInitializationException(message,null);
        }

        public unsafe static IntPtr GetObjectID(object obj)
        {
            throw new NotSupportedException("GetObjectID");
        }

        public static bool RhpETWShouldWalkCom()
        {
            throw new NotSupportedException("RhpETWShouldWalkCom");
        }

        public static void RhpETWLogLiveCom(int eventType, IntPtr CCWHandle, IntPtr objectID, IntPtr typeRawValue, IntPtr IUnknown, IntPtr VTable, Int32 comRefCount, Int32 jupiterRefCount, Int32 flags)
        {
            throw new NotSupportedException("RhpETWLogLiveCom");
        }

        public static bool SupportsReflection(this Type type)
        {
            return true;
        }

        public static void SuppressReentrantWaits()
        {
            // Nop 
        }

        public static void RestoreReentrantWaits()
        {
            //Nop
        }

        public static IntPtr GetCriticalHandle(CriticalHandle criticalHandle)
        { 
            throw new NotSupportedException("GetCriticalHandle"); 
        }
 
        public static void SetCriticalHandle(CriticalHandle criticalHandle, IntPtr handle)
        { 
            throw new NotSupportedException("SetCriticalHandle"); 
        }
    }

    public class GCHelpers
    {
        public static void RhpSetThreadDoNotTriggerGC() { }
        public static void RhpClearThreadDoNotTriggerGC() { }
    }
}
