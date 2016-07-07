// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Diagnostics;

using Internal.Reflection.Core.NonPortable;

namespace System
{
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    public static class InvokeUtils
    {
        //
        // Various reflection scenarios (Array.SetValue(), reflection Invoke, delegate DynamicInvoke and FieldInfo.Set()) perform
        // automatic conveniences such as automatically widening primitive types to fit the destination type.
        //
        // This method attempts to collect as much of that logic as possible in place. (This may not be completely possible
        // as the desktop CLR is not particularly consistent across all these scenarios either.)
        //
        // The transforms supported are:
        //
        //    Value-preserving widenings of primitive integrals and floats. 
        //    Enums can be converted to the same or wider underlying primitive.
        //    Primitives can be converted to an enum with the same or wider underlying primitive.
        //
        //    null converted to default(T) (this is important when T is a valuetype.)
        //
        // There is also another transform of T -> Nullable<T>. This method acknowleges that rule but does not actually transform the T.
        // Rather, the transformation happens naturally when the caller unboxes the value to its final destination.
        //
        // This method is targeted by the Delegate ILTransformer.
        //    
        //
        public static Object CheckArgument(Object srcObject, RuntimeTypeHandle dstType)
        {
            EETypePtr dstEEType = dstType.ToEETypePtr();
            return CheckArgument(srcObject, dstEEType, CheckArgumentSemantics.DynamicInvoke);
        }

        // This option does nothing but decide which type of exception to throw to match the legacy behavior.
        internal enum CheckArgumentSemantics
        {
            ArraySet,            // Throws InvalidCastException
            DynamicInvoke,       // Throws ArgumentException
        }

        internal static Object CheckArgument(Object srcObject, EETypePtr dstEEType, CheckArgumentSemantics semantics)
        {
            if (srcObject == null)
            {
                // null -> default(T) 
                if (dstEEType.IsValueType && !dstEEType.IsNullable)
                    return Runtime.RuntimeImports.RhNewObject(dstEEType);
                else
                    return null;
            }
            else
            {
                EETypePtr srcEEType = srcObject.EETypePtr;

                if (RuntimeImports.AreTypesAssignable(srcEEType, dstEEType))
                    return srcObject;


                if (dstEEType.IsInterface)
                {
                    ICastable castable = srcObject as ICastable;
                    Exception castError;

                    if (castable != null && castable.IsInstanceOfInterface(new RuntimeTypeHandle(dstEEType), out castError))
                        return srcObject;
                }

                if (!((srcEEType.IsEnum || srcEEType.IsPrimitive) && (dstEEType.IsEnum || dstEEType.IsPrimitive)))
                    throw CreateChangeTypeException(srcEEType, dstEEType, semantics);

                RuntimeImports.RhCorElementType dstCorElementType = dstEEType.CorElementType;
                if (!srcEEType.CorElementTypeInfo.CanWidenTo(dstCorElementType))
                    throw CreateChangeTypeArgumentException(srcEEType, dstEEType);

                Object dstObject;
                switch (dstCorElementType)
                {
                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN:
                        dstObject = Convert.ToBoolean(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                        dstObject = Convert.ToChar(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                        dstObject = Convert.ToSByte(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                        dstObject = Convert.ToInt16(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                        dstObject = Convert.ToInt32(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                        dstObject = Convert.ToInt64(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                        dstObject = Convert.ToByte(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                        dstObject = Convert.ToUInt16(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                        dstObject = Convert.ToUInt32(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                        dstObject = Convert.ToUInt64(srcObject);
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                        if (srcEEType.CorElementType == RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR)
                        {
                            dstObject = (float)(char)srcObject;
                        }
                        else
                        {
                            dstObject = Convert.ToSingle(srcObject);
                        }
                        break;

                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                        if (srcEEType.CorElementType == RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR)
                        {
                            dstObject = (double)(char)srcObject;
                        }
                        else
                        {
                            dstObject = Convert.ToDouble(srcObject);
                        }
                        break;

                    default:
                        Debug.Assert(false, "Unexpected CorElementType: " + dstCorElementType + ": Not a valid widening target.");
                        throw CreateChangeTypeException(srcEEType, dstEEType, semantics);
                }

                if (dstEEType.IsEnum)
                {
                    Type dstType = ReflectionCoreNonPortable.GetRuntimeTypeForEEType(dstEEType);
                    dstObject = Enum.ToObject(dstType, dstObject);
                }

                Debug.Assert(dstObject.EETypePtr == dstEEType);
                return dstObject;
            }
        }

        private static Exception CreateChangeTypeException(EETypePtr srcEEType, EETypePtr dstEEType, CheckArgumentSemantics semantics)
        {
            switch (semantics)
            {
                case CheckArgumentSemantics.DynamicInvoke:
                    return CreateChangeTypeArgumentException(srcEEType, dstEEType);
                case CheckArgumentSemantics.ArraySet:
                    return CreateChangeTypeInvalidCastException(srcEEType, dstEEType);
                default:
                    Debug.Assert(false, "Unexpected CheckArgumentSemantics value: " + semantics);
                    throw new InvalidOperationException();
            }
        }

        private static ArgumentException CreateChangeTypeArgumentException(EETypePtr srcEEType, EETypePtr dstEEType)
        {
            return new ArgumentException(SR.Format(SR.Arg_ObjObjEx, Type.GetTypeFromHandle(new RuntimeTypeHandle(srcEEType)), Type.GetTypeFromHandle(new RuntimeTypeHandle(dstEEType))));
        }

        private static InvalidCastException CreateChangeTypeInvalidCastException(EETypePtr srcEEType, EETypePtr dstEEType)
        {
            return new InvalidCastException(SR.InvalidCast_StoreArrayElement);
        }

        // -----------------------------------------------
        // Infrastructure and logic for Dynamic Invocation
        // -----------------------------------------------
        public enum DynamicInvokeParamType
        {
            In = 0,
            Ref = 1
        }

        public enum DynamicInvokeParamLookupType
        {
            ValuetypeObjectReturned = 0,
            IndexIntoObjectArrayReturned = 1,
        }

        public struct ArgSetupState
        {
            public bool fComplete;
            public object[] nullableCopyBackObjects;
        }

        // These thread static fields are used instead of passing parameters normally through to the helper functions
        // that actually implement dynamic invocation. This allows the large number of dynamically generated 
        // functions to be just that little bit smaller, which, when spread across the many invocation helper thunks
        // generated adds up quite a bit.
        [ThreadStatic]
        private static object[] s_parameters;
        [ThreadStatic]
        private static object[] s_nullableCopyBackObjects;
        [ThreadStatic]
        private static int s_curIndex;
        [ThreadStatic]
        private static string s_defaultValueString;

        // Default parameter support
        private const int DefaultParamTypeNone = 0;
        private const int DefaultParamTypeBool = 1;
        private const int DefaultParamTypeChar = 2;
        private const int DefaultParamTypeI1 = 3;
        private const int DefaultParamTypeI2 = 4;
        private const int DefaultParamTypeI4 = 5;
        private const int DefaultParamTypeI8 = 6;
        private const int DefaultParamTypeR4 = 7;
        private const int DefaultParamTypeR8 = 8;
        private const int DefaultParamTypeString = 9;
        private const int DefaultParamTypeDefault = 10;
        private const int DefaultParamTypeDecimal = 11;
        private const int DefaultParamTypeDateTime = 12;
        private const int DefaultParamTypeNoneButOptional = 13;

        private struct StringDataParser
        {
            private string _str;
            private int _offset;
            public StringDataParser(string str)
            {
                _str = str;
                _offset = 0;
            }

            public void SetOffset(int offset)
            {
                _offset = offset;
            }

            public long GetLong()
            {
                long returnValue;

                char curVal = _str[_offset++];

                // High bit is used to indicate an extended value
                // Low bit is sign bit
                // The middle 14 bits are used to hold 14 bits of the actual long value.
                // A sign bit approach is used so that a negative number can be represented with 1 char value.
                returnValue = (long)(curVal & (char)0x7FFE);
                returnValue = returnValue >> 1;
                bool isNegative = ((curVal & (char)1)) == 1;
                if ((returnValue == 0) && isNegative)
                {
                    return Int64.MinValue;
                }
                int additionalCharCount = 0;
                int bitsAcquired = 14;
                // For additional characters, the first 3 additional characters hold 15 bits of data
                // and the last character may hold 5 bits of data.
                while ((curVal & (char)0x8000) != 0)
                {
                    additionalCharCount++;
                    curVal = _str[_offset++];
                    long grabValue = (long)(curVal & (char)0x7FFF);
                    grabValue <<= bitsAcquired;
                    bitsAcquired += 15;
                    returnValue |= grabValue;
                }

                if (isNegative)
                    returnValue = -returnValue;

                return returnValue;
            }

            public int GetInt()
            {
                return checked((int)GetLong());
            }

            public unsafe float GetFloat()
            {
                int inputLocation = checked((int)GetLong());
                float result = 0;
                byte* inputPtr = (byte*)&inputLocation;
                byte* outputPtr = (byte*)&result;
                for (int i = 0; i < 4; i++)
                {
                    outputPtr[i] = inputPtr[i];
                }

                return result;
            }

            public unsafe double GetDouble()
            {
                long inputLocation = GetLong();
                double result = 0;
                byte* inputPtr = (byte*)&inputLocation;
                byte* outputPtr = (byte*)&result;
                for (int i = 0; i < 8; i++)
                {
                    outputPtr[i] = inputPtr[i];
                }

                return result;
            }

            public unsafe string GetString()
            {
                fixed (char* strData = _str)
                {
                    int length = (int)GetLong();
                    char c = _str[_offset]; // Check for index out of range concerns.
                    string strRet = new string(strData, _offset, length);
                    _offset += length;

                    return strRet;
                }
            }

            public void Skip(int count)
            {
                _offset += count;
            }
        }

        private static unsafe object GetDefaultValue(RuntimeTypeHandle thType, int argIndex)
        {
            // Group index of 0 indicates there are no default parameters
            if (s_defaultValueString == null)
            {
                throw new ArgumentException(SR.Arg_DefaultValueMissingException);
            }
            StringDataParser dataParser = new StringDataParser(s_defaultValueString);

            // Skip to current argument
            int curArgIndex = 0;
            while (curArgIndex != argIndex)
            {
                int skip = dataParser.GetInt();
                dataParser.Skip(skip);
                curArgIndex++;
            }

            // Discard size of current argument
            int sizeOfCurrentArg = dataParser.GetInt();

            int defaultValueType = dataParser.GetInt();

            switch (defaultValueType)
            {
                case DefaultParamTypeNone:
                default:
                    throw new ArgumentException(SR.Arg_DefaultValueMissingException);

                case DefaultParamTypeString:
                    return dataParser.GetString();

                case DefaultParamTypeDefault:
                    if (thType.ToEETypePtr().IsValueType)
                    {
                        if (thType.ToEETypePtr().IsNullable)
                        {
                            return null;
                        }
                        else
                        {
                            return RuntimeImports.RhNewObject(thType.ToEETypePtr());
                        }
                    }
                    else
                    {
                        return null;
                    }

                case DefaultParamTypeBool:
                    return (dataParser.GetInt() == 1);
                case DefaultParamTypeChar:
                    return (char)dataParser.GetInt();
                case DefaultParamTypeI1:
                    return (sbyte)dataParser.GetInt();
                case DefaultParamTypeI2:
                    return (short)dataParser.GetInt();
                case DefaultParamTypeI4:
                    return dataParser.GetInt();
                case DefaultParamTypeI8:
                    return dataParser.GetLong();
                case DefaultParamTypeR4:
                    return dataParser.GetFloat();
                case DefaultParamTypeR8:
                    return dataParser.GetDouble();
                case DefaultParamTypeDecimal:
                    int[] decimalBits = new int[4];
                    decimalBits[0] = dataParser.GetInt();
                    decimalBits[1] = dataParser.GetInt();
                    decimalBits[2] = dataParser.GetInt();
                    decimalBits[3] = dataParser.GetInt();
                    return new Decimal(decimalBits);
                case DefaultParamTypeDateTime:
                    return new DateTime(dataParser.GetLong());
                case DefaultParamTypeNoneButOptional:
                    return System.Reflection.Missing.Value;
            }
        }

        [DebuggerGuidedStepThroughAttribute]
        internal static object CallDynamicInvokeMethod(object thisPtr, IntPtr methodToCall, object thisPtrDynamicInvokeMethod, IntPtr dynamicInvokeHelperMethod, IntPtr dynamicInvokeHelperGenericDictionary, string defaultValueString, object[] parameters, bool invokeMethodHelperIsThisCall = true, bool methodToCallIsThisCall = true)
        {
            bool fDontWrapInTargetInvocationException = false;
            bool parametersNeedCopyBack = false;
            ArgSetupState argSetupState = default(ArgSetupState);

            // Capture state of thread static invoke helper statics
            object[] parametersOld = s_parameters;
            object[] nullableCopyBackObjectsOld = s_nullableCopyBackObjects;
            int curIndexOld = s_curIndex;
            string defaultValueStringOld = s_defaultValueString;

            try
            {
                // If the passed in array is not an actual object[] instance, we need to copy it over to an actual object[]
                // instance so that the rest of the code can safely create managed object references to individual elements.
                if (parameters != null && EETypePtr.EETypePtrOf<object[]>() != parameters.EETypePtr)
                {
                    s_parameters = new object[parameters.Length];
                    Array.Copy(parameters, s_parameters, parameters.Length);
                    parametersNeedCopyBack = true;
                }
                else
                {
                    s_parameters = parameters;
                }

                s_nullableCopyBackObjects = null;
                s_curIndex = 0;
                s_defaultValueString = defaultValueString;

                try
                {
                    object result = null;
                    if (invokeMethodHelperIsThisCall)
                    {
                        Debug.Assert(methodToCallIsThisCall == true);
                        result = CallIHelperThisCall(thisPtr, methodToCall, thisPtrDynamicInvokeMethod, dynamicInvokeHelperMethod, ref argSetupState);
                        System.Diagnostics.DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
                    }
                    else
                    {
                        if (dynamicInvokeHelperGenericDictionary != IntPtr.Zero)
                        {
                            result = CallIHelperStaticCallWithInstantiation(thisPtr, methodToCall, dynamicInvokeHelperMethod, ref argSetupState, methodToCallIsThisCall, dynamicInvokeHelperGenericDictionary);
                            DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
                        }
                        else
                        {
                            result = CallIHelperStaticCall(thisPtr, methodToCall, dynamicInvokeHelperMethod, ref argSetupState, methodToCallIsThisCall);
                            DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
                        }
                    }
                    
                    return result;
                }
                finally
                {
                    if (parametersNeedCopyBack)
                    {
                        Array.Copy(s_parameters, parameters, parameters.Length);
                    }

                    if (!argSetupState.fComplete)
                    {
                        fDontWrapInTargetInvocationException = true;
                    }
                    else
                    {
                        // Nullable objects can't take advantage of the ability to update the boxed value on the heap directly, so perform
                        // an update of the parameters array now.
                        if (argSetupState.nullableCopyBackObjects != null)
                        {
                            for (int i = 0; i < argSetupState.nullableCopyBackObjects.Length; i++)
                            {
                                if (argSetupState.nullableCopyBackObjects[i] != null)
                                {
                                    parameters[i] = DynamicInvokeBoxIntoNonNullable(argSetupState.nullableCopyBackObjects[i]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (fDontWrapInTargetInvocationException)
                {
                    throw;
                }
                else
                {
                    throw new System.Reflection.TargetInvocationException(e);
                }
            }
            finally
            {
                // Restore state of thread static helper statics
                s_parameters = parametersOld;
                s_nullableCopyBackObjects = nullableCopyBackObjectsOld;
                s_curIndex = curIndexOld;
                s_defaultValueString = defaultValueStringOld;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void DynamicInvokeArgSetupComplete(ref ArgSetupState argSetupState)
        {
            int parametersLength = s_parameters != null ? s_parameters.Length : 0;

            if (s_curIndex != parametersLength)
            {
                throw new System.Reflection.TargetParameterCountException();
            }
            argSetupState.fComplete = true;
            argSetupState.nullableCopyBackObjects = s_nullableCopyBackObjects;
            s_nullableCopyBackObjects = null;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void DynamicInvokeArgSetupPtrComplete(IntPtr argSetupStatePtr)
        {
            // Intrinsic filled by the ImplementLibraryDynamicInvokeHelpers transform.
            // argSetupStatePtr is a pointer to a *pinned* ArgSetupState object

            // ldarg.0
            // call void System.InvokeUtils.DynamicInvokeArgSetupComplete(ref ArgSetupState)
            // ret

            throw new PlatformNotSupportedException();
        }

#if CORERT
        [Intrinsic]
        [DebuggerStepThrough] // Added by transform on non-CoreRT
#endif
        internal static object CallIHelperThisCall(object thisPtr, IntPtr methodToCall, object thisPtrForDynamicInvokeHelperMethod, IntPtr dynamicInvokeHelperMethod, ref ArgSetupState argSetupState)
        {
            // Calli the dynamicInvokeHelper method with a bunch of parameters  As this can't actually be defined in C# there is an IL transform that fills this in.
            return null;
        }

#if CORERT
        [Intrinsic]
        [DebuggerStepThrough] // Added by transform on non-CoreRT
#endif
        internal static object CallIHelperStaticCall(object thisPtr, IntPtr methodToCall, IntPtr dynamicInvokeHelperMethod, ref ArgSetupState argSetupState, bool isTargetThisCall)
        {
            // Calli the dynamicInvokeHelper method with a bunch of parameters  As this can't actually be defined in C# there is an IL transform that fills this in.
            return null;
        }

#if CORERT
        [Intrinsic]
        [DebuggerStepThrough] // Added by transform on non-CoreRT
#endif
        internal static object CallIHelperStaticCallWithInstantiation(object thisPtr, IntPtr methodToCall, IntPtr dynamicInvokeHelperMethod, ref ArgSetupState argSetupState, bool isTargetThisCall, IntPtr dynamicInvokeHelperGenericDictionary)
        {
            // Calli the dynamicInvokeHelper method with a bunch of parameters  As this can't actually be defined in C# there is an IL transform that fills this in.
            return null;
        }

        // Template function that is used to call dynamically
        internal static object DynamicInvokeThisCallTemplate(object thisPtr, IntPtr methodToCall, ref ArgSetupState argSetupState)
        {
            // This function will look like
            //
            // !For each parameter to the method
            //    !if (parameter is In Parameter)
            //       localX is TypeOfParameterX&
            //       ldtoken TypeOfParameterX
            //       call DynamicInvokeParamHelperIn(RuntimeTypeHandle)
            //       stloc localX
            //    !else
            //       localX is TypeOfParameter
            //       ldtoken TypeOfParameterX
            //       call DynamicInvokeParamHelperRef(RuntimeTypeHandle)
            //       stloc localX

            // ldarg.2
            // call DynamicInvokeArgSetupComplete(ref ArgSetupState)

            // ldarg.0 // Load this pointer
            // !For each parameter
            //    !if (parameter is In Parameter)
            //       ldloc localX
            //       ldobj TypeOfParameterX
            //    !else
            //       ldloc localX
            // ldarg.1
            // calli ReturnType thiscall(TypeOfParameter1, ...)
            // !if ((ReturnType != void) && !(ReturnType is a byref)
            //    ldnull
            // !else
            //    box ReturnType
            // ret
            return null;
        }

        internal static object DynamicInvokeCallTemplate(object thisPtr, IntPtr methodToCall, ref ArgSetupState argSetupState, bool targetIsThisCall)
        {
            // This function will look like
            //
            // !For each parameter to the method
            //    !if (parameter is In Parameter)
            //       localX is TypeOfParameterX&
            //       ldtoken TypeOfParameterX
            //       call DynamicInvokeParamHelperIn(RuntimeTypeHandle)
            //       stloc localX
            //    !else
            //       localX is TypeOfParameter
            //       ldtoken TypeOfParameterX
            //       call DynamicInvokeParamHelperRef(RuntimeTypeHandle)
            //       stloc localX

            // ldarg.2
            // call DynamicInvokeArgSetupComplete(ref ArgSetupState)

            // !if (targetIsThisCall)
            //    ldarg.0 // Load this pointer
            //    !For each parameter
            //       !if (parameter is In Parameter)
            //          ldloc localX
            //          ldobj TypeOfParameterX
            //       !else
            //          ldloc localX
            //    ldarg.1
            //    calli ReturnType thiscall(TypeOfParameter1, ...)
            //    !if ((ReturnType != void) && !(ReturnType is a byref)
            //       ldnull
            //    !else
            //       box ReturnType
            //    ret
            // !else
            //    !For each parameter
            //       !if (parameter is In Parameter)
            //          ldloc localX
            //          ldobj TypeOfParameterX
            //       !else
            //          ldloc localX
            //    ldarg.1
            //    calli ReturnType (TypeOfParameter1, ...)
            //    !if ((ReturnType != void) && !(ReturnType is a byref)
            //       ldnull
            //    !else
            //       box ReturnType
            //    ret
            return null;
        }

#if CORERT
        [Intrinsic]
        [DebuggerStepThrough] // Added by transform on non-CoreRT
#endif
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void DynamicInvokeUnboxIntoActualNullable(object actualBoxedNullable, object boxedFillObject, EETypePtr nullableType)
        {
            // pin the actualBoxedNullable
            // local0 is object pinned pinnedActualBoxedNullable
            // ldarg.0
            // stloc.0

            // ldarg.1 // object to unbox
            // ldarg.0
            // ldflda System.Object::m_eetype
            // sizeof EETypePtr
            // add // byref to data data region within actualBoxedNullable
            // ldarg.2
            // get a byref to the data within the actual boxed nullable, and then call RhUnBox with the boxedFillObject as the boxed object, and nullableType as the unbox type, and unbox into the actualBoxedNullable
            // call static void RuntimeImports.RhUnbox(object obj, void* pData, EETypePtr pUnboxToEEType);
            // ret
        }

#if CORERT
        [Intrinsic]
        [DebuggerStepThrough] // Added by transform on non-CoreRT
#endif
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static object DynamicInvokeBoxIntoNonNullable(object actualBoxedNullable)
        {
            // pin the actualBoxedNullable
            // local0 is object pinned pinnedActualBoxedNullable
            // ldarg.0
            // stloc.0
            //
            // grab the pointer to data, box using the EEType of the actualBoxedNullable, and then return the boxed object
            // ldarg.0
            // ldfld System.Object::m_eetype
            // ldarg.0
            // ldflda System.Object::m_eetype
            // sizeof EETypePtr
            // add
            // call RuntimeImports.RhBox(EETypePtr, void*data)
            // ret
            return null;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static /* Transform change this into byref to IntPtr*/ IntPtr DynamicInvokeParamHelperIn(RuntimeTypeHandle rth)
        {
            // Call DynamicInvokeParamHelperCore as an in parameter, and return a managed byref to the interesting bit. As this can't actually be defined in C# there is an IL transform that fills this in.
            //
            // This function exactly matches DynamicInvokeParamHelperRef except for the value of the enum passed to DynamicInvokeParamHelperCore
            // 
            // zeroinit
            // local0 is int index
            // local1 is DynamicInvokeParamLookupType
            // local2 is object
            // 
            // Capture information about the parameter.
            // ldarg.0 
            // ldloca 1 // ref DynamicInvokeParamLookupType
            // ldloca 0 // ref index
            // ldc.i4.0 // DynamicInvokeParamType.In
            // call object DynamicInvokeParamHelperCore(RuntimeTypeHandle type, out DynamicInvokeParamLookupType paramLookupType, out int index, DynamicInvokeParamType paramType)
            // stloc.2

            // decode output of DynamicInvokeParamHelperCore
            // if (paramLookupType == DynamicInvokeParamLookupType.ValuetypeObjectReturned)
            //     return &local2.m_pEEType + sizeof(EETypePtr)
            // else
            //     return &(((object[])local2)[index])
            //
            // ldloc.1
            // ldc.i4.0
            // bne.un arrayCase
            // ldloc.2
            // ldflda System.Object::m_eetype
            // sizeof EETypePtr
            // add
            // ret
            // arrayCase:
            // ldloc.2
            // ldloc.0
            // ldelema System.Object
            // ret
            return IntPtr.Zero;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static /* Transform change this into byref to IntPtr*/ IntPtr DynamicInvokeParamHelperRef(RuntimeTypeHandle rth)
        {
            // Call DynamicInvokeParamHelperCore as a ref parameter, and return a managed byref to the interesting bit. As this can't actually be defined in C# there is an IL transform that fills this in.
            //
            // This function exactly matches DynamicInvokeParamHelperIn except for the value of the enum passed to DynamicInvokeParamHelperCore
            // 
            // zeroinit
            // local0 is int index
            // local1 is DynamicInvokeParamLookupType
            // local2 is object
            // 
            // Capture information about the parameter.
            // ldarg.0 
            // ldloca 1 // ref DynamicInvokeParamLookupType
            // ldloca 0 // ref index
            // ldc.i4.1 // DynamicInvokeParamType.Ref
            // call object DynamicInvokeParamHelperCore(RuntimeTypeHandle type, out DynamicInvokeParamLookupType paramLookupType, out int index, DynamicInvokeParamType paramType)
            // stloc.2

            // decode output of DynamicInvokeParamHelperCore
            // if (paramLookupType == DynamicInvokeParamLookupType.ValuetypeObjectReturned)
            //     return &local2.m_pEEType + sizeof(EETypePtr)
            // else
            //     return &(((object[])local2)[index])
            //
            // ldloc.1
            // ldc.i4.0
            // bne.un arrayCase
            // ldloc.2
            // ldflda System.Object::m_eetype
            // sizeof EETypePtr
            // add
            // ret
            // arrayCase:
            // ldloc.2
            // ldloc.0
            // ldelema System.Object
            // ret

            return IntPtr.Zero;
        }

        internal static object DynamicInvokeBoxedValuetypeReturn(out DynamicInvokeParamLookupType paramLookupType, object boxedValuetype, int index, RuntimeTypeHandle type, DynamicInvokeParamType paramType)
        {
            object finalObjectToReturn = boxedValuetype;
            EETypePtr eeType = type.ToEETypePtr();
            bool nullable = eeType.IsNullable;

            if (finalObjectToReturn == null || nullable || paramType == DynamicInvokeParamType.Ref)
            {
                finalObjectToReturn = RuntimeImports.RhNewObject(eeType);
                if (boxedValuetype != null)
                {
                    DynamicInvokeUnboxIntoActualNullable(finalObjectToReturn, boxedValuetype, eeType);
                }
            }

            if (nullable)
            {
                if (paramType == DynamicInvokeParamType.Ref)
                {
                    if (s_nullableCopyBackObjects == null)
                    {
                        s_nullableCopyBackObjects = new object[s_parameters.Length];
                    }

                    s_nullableCopyBackObjects[index] = finalObjectToReturn;
                    s_parameters[index] = null;
                }
            }
            else
            {
                System.Diagnostics.Debug.Assert(finalObjectToReturn != null);
                if (paramType == DynamicInvokeParamType.Ref)
                    s_parameters[index] = finalObjectToReturn;
            }

            paramLookupType = DynamicInvokeParamLookupType.ValuetypeObjectReturned;
            return finalObjectToReturn;
        }

        public static object DynamicInvokeParamHelperCore(RuntimeTypeHandle type, out DynamicInvokeParamLookupType paramLookupType, out int index, DynamicInvokeParamType paramType)
        {
            index = s_curIndex++;
            int parametersLength = s_parameters != null ? s_parameters.Length : 0;

            if (index >= parametersLength)
                throw new System.Reflection.TargetParameterCountException();

            object incomingParam = s_parameters[index];

            // Handle default parameters
            if ((incomingParam == System.Reflection.Missing.Value) && paramType == DynamicInvokeParamType.In)
            {
                incomingParam = GetDefaultValue(type, index);

                // The default value is captured into the parameters array
                s_parameters[index] = incomingParam;
            }

            RuntimeTypeHandle widenAndCompareType = type;
            bool nullable = type.ToEETypePtr().IsNullable;
            if (nullable)
            {
                widenAndCompareType = new RuntimeTypeHandle(type.ToEETypePtr().NullableType);
            }

            if (widenAndCompareType.ToEETypePtr().IsPrimitive || type.ToEETypePtr().IsEnum)
            {
                // Nullable requires exact matching
                if (incomingParam != null)
                {
                    if ((nullable || paramType == DynamicInvokeParamType.Ref) && incomingParam != null)
                    {
                        if (widenAndCompareType.ToEETypePtr() != incomingParam.EETypePtr)
                        {
                            throw CreateChangeTypeArgumentException(incomingParam.EETypePtr, type.ToEETypePtr());
                        }
                    }
                    else
                    {
                        if (widenAndCompareType.ToEETypePtr().CorElementType != incomingParam.EETypePtr.CorElementType)
                        {
                            System.Diagnostics.Debug.Assert(paramType == DynamicInvokeParamType.In);
                            incomingParam = InvokeUtils.CheckArgument(incomingParam, widenAndCompareType.ToEETypePtr(), InvokeUtils.CheckArgumentSemantics.DynamicInvoke);
                        }
                    }
                }

                return DynamicInvokeBoxedValuetypeReturn(out paramLookupType, incomingParam, index, type, paramType);
            }
            else if (type.ToEETypePtr().IsValueType)
            {
                incomingParam = InvokeUtils.CheckArgument(incomingParam, type.ToEETypePtr(), InvokeUtils.CheckArgumentSemantics.DynamicInvoke);
                System.Diagnostics.Debug.Assert(s_parameters[index] == null || Object.ReferenceEquals(incomingParam, s_parameters[index]));
                return DynamicInvokeBoxedValuetypeReturn(out paramLookupType, incomingParam, index, type, paramType);
            }
            else
            {
                incomingParam = InvokeUtils.CheckArgument(incomingParam, widenAndCompareType.ToEETypePtr(), InvokeUtils.CheckArgumentSemantics.DynamicInvoke);
                System.Diagnostics.Debug.Assert(Object.ReferenceEquals(incomingParam, s_parameters[index]));
                paramLookupType = DynamicInvokeParamLookupType.IndexIntoObjectArrayReturned;
                return s_parameters;
            }
        }
    }
}

