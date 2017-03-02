// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Internal.TypeSystem;
using Internal.IL.Stubs;
using Internal.IL;
using Debug = System.Diagnostics.Debug;
using ILLocalVariable = Internal.IL.Stubs.ILLocalVariable;

namespace Internal.TypeSystem.Interop
{
    enum MarshallerKind
    {
        Unknown,
        BlittableValue,
        Array,
        BlittableArray,
        Bool,   // 4 byte bool
        CBool,  // 1 byte bool
        Enum,
        AnsiChar,  // Marshal char (Unicode 16bits) for byte (Ansi 8bits)
        UnicodeChar,
        AnsiCharArray,
        ByValArray,
        ByValAnsiCharArray, // Particular case of ByValArray because the conversion between wide Char and Byte need special treatment.
        AnsiString,
        UnicodeString,
        AnsiStringBuilder,
        UnicodeStringBuilder,
        FunctionPointer,
        SafeHandle,
        CriticalHandle,
        HandleRef,
        VoidReturn,
        Variant,
        Object,
        OleDateTime,
        Decimal,
        Guid,
        Struct,
        BlittableStruct,
        BlittableStructPtr,   // Additional indirection on top of blittable struct. Used by MarshalAs(LpStruct)
        Invalid
    }
    public enum MarshalDirection
    {
        Forward,    // safe-to-unsafe / managed-to-native
        Reverse,    // unsafe-to-safe / native-to-managed
    }

    public enum MarshallerType
    {
        Argument,
        Element,
        Field
    }

    // Each type of marshaller knows how to generate the marshalling code for the argument it marshals.
    // Marshallers contain method related marshalling information (which is common to all the Marshallers)
    // and also argument specific marshalling informaiton
    abstract class Marshaller
    {
        public PInvokeMethodData PInvokeMethodData;
        #region Instance state information
        public ParameterMetadata PInvokeParameterMetadata;
        public MarshallerKind MarshallerKind;
        public MarshallerType MarshallerType;
        public MarshallerKind ElementMarshallerKind;
        public TypeDesc ManagedType;
        public TypeDesc ManagedParameterType;
        protected Marshaller[] Marshallers;
        private TypeDesc _nativeType;
        private TypeDesc _nativeParamType;

        public TypeDesc NativeType
        {
            get
            {
                if (_nativeType == null)
                {
                    _nativeType = GetNativeTypeFromMarshallerKind(ManagedType,
                                                        MarshallerKind, ElementMarshallerKind,
                                                    PInvokeParameterMetadata.MarshalAsDescriptor);
                    Debug.Assert(_nativeType != null);
                }

                return _nativeType;
            }
        }

        /// <summary>
        /// NativeType appears in function parameters
        /// </summary>
        public TypeDesc NativeParameterType
        {
            get
            {
                if (_nativeParamType == null)
                {
                    TypeDesc nativeParamType = NativeType;
                    if (IsNativeByRef)
                        nativeParamType = nativeParamType.MakePointerType();
                    _nativeParamType = nativeParamType;
                }

                return _nativeParamType;
            }
        }

        public bool In;
        public bool Out;
        public bool Return;
        public bool Optional;
        public bool IsManagedByRef;
        public bool IsNativeByRef;
        public MarshalDirection MarshalDirection;
        protected PInvokeILCodeStreams _ilCodeStreams;
        protected ILLocalVariable _vManaged;
        protected ILLocalVariable _vNative;
        #endregion

        #region Creation of marshallers

        /// <summary>
        /// Protected ctor
        /// Only Marshaller.CreateMarshaller can create a marshaller
        /// </summary>
        protected Marshaller()
        {
        }
        /// <summary>
        /// Create a marshaller
        /// </summary>
        /// <param name="parameterType">type of the parameter to marshal</param>
        /// <param name="pInvokeMethodData">PInvoke Method specific marshal data</param>
        /// <param name="pInvokeParameterdata">PInvoke parameter specific marshal data</param>
        /// <returns>The  created Marshaller</returns>
        public static Marshaller CreateMarshaller(TypeDesc parameterType, PInvokeMethodData pInvokeMethodData,
            ParameterMetadata pInvokeParameterdata,
            Marshaller[] marshallers,
            MarshalDirection direction)
        {
            MarshallerKind elementMarshallerKind;
            MarshallerKind marshallerKind = GetMarshallerKind(parameterType,
                                                pInvokeParameterdata,
                                                pInvokeMethodData,
                                                MarshallerType.Argument,      /* isField*/
                                                out elementMarshallerKind);

            // Create the marshaller based on MarshallerKind
            Marshaller marshaller = Marshaller.CreateMarshaller(marshallerKind);
            marshaller.PInvokeMethodData = pInvokeMethodData;
            marshaller.PInvokeParameterMetadata = pInvokeParameterdata;
            marshaller.MarshallerKind = marshallerKind;
            marshaller.MarshallerType = MarshallerType.Argument;
            marshaller.ElementMarshallerKind = elementMarshallerKind;
            marshaller.ManagedParameterType = parameterType;
            marshaller.ManagedType = parameterType.IsByRef? parameterType.GetParameterType() : parameterType;
            marshaller.Optional = pInvokeParameterdata.Optional;
            marshaller.Return = pInvokeParameterdata.Return;
            marshaller.IsManagedByRef = parameterType.IsByRef;
            marshaller.IsNativeByRef = marshaller.IsManagedByRef /* || isRetVal || LpStruct /etc */;
            marshaller.In = pInvokeParameterdata.In;
            marshaller.MarshalDirection = direction;
            marshaller.Marshallers = marshallers;

            //
            // Desktop ignores [Out] on marshaling scenarios where they don't make sense (such as passing
            // value types and string as [out] without byref). 
            //
            if (marshaller.IsManagedByRef)
            {
                // Passing as [Out] by ref is valid
                marshaller.Out = pInvokeParameterdata.Out;
            }
            else
            {
                // Passing as [Out] is valid only if it is not ValueType nor string
                if (!parameterType.IsValueType && !parameterType.IsString)
                    marshaller.Out = pInvokeParameterdata.Out;
            }

            if (!marshaller.In && !marshaller.Out)
            {
                //
                // Rules for in/out
                // 1. ByRef args: [in]/[out] implied by default
                // 2. StringBuilder: [in, out] by default
                // 3. non-ByRef args: [In] is implied if no [In]/[Out] is specified
                //
                if (marshaller.IsManagedByRef)
                {
                    marshaller.In = true;
                    marshaller.Out = true;
                }
                else if (pInvokeMethodData.IsStringBuilder(parameterType))
                {
                    marshaller.In = true;
                    marshaller.Out = true;
                }
                else
                {
                    marshaller.In = true;
                }
            }

            // For unicodestring/ansistring, ignore out when it's in
            if (!marshaller.IsManagedByRef && marshaller.In)
            {
                if (marshaller.MarshallerKind == MarshallerKind.AnsiString || marshaller.MarshallerKind == MarshallerKind.UnicodeString)
                    marshaller.Out = false;
            }

            return marshaller;
        }

        protected static Marshaller CreateMarshaller(MarshallerKind kind)
        {
            switch (kind)
            {
                case MarshallerKind.Enum:
                case MarshallerKind.BlittableValue:
                case MarshallerKind.BlittableStruct:
                case MarshallerKind.UnicodeChar:
                    return new BlittableValueMarshaller();
                case MarshallerKind.Array:
                    return new ArrayMarshaller();
                case MarshallerKind.BlittableArray:
                    return new BlittableArrayMarshaller();
                case MarshallerKind.Bool:
                    return new BooleanMarshaller();
                case MarshallerKind.AnsiString:
                    return new AnsiStringMarshaller();
                case MarshallerKind.UnicodeString:
                    return new UnicodeStringMarshaller();
                case MarshallerKind.SafeHandle:
                    return new SafeHandleMarshaller();
                case MarshallerKind.UnicodeStringBuilder:
                    return new UnicodeStringBuilderMarshaller();
                case MarshallerKind.VoidReturn:
                    return new VoidReturnMarshaller();
                case MarshallerKind.FunctionPointer:
                    return new DelegateMarshaller();
                default:
                    throw new NotSupportedException();
            }
        }

        public bool IsMarshallingRequired()
        {
            if (Out)
                return true;

            switch (MarshallerKind)
            {
                case MarshallerKind.Enum:
                case MarshallerKind.BlittableValue:
                case MarshallerKind.BlittableStruct:
                case MarshallerKind.UnicodeChar:
                case MarshallerKind.VoidReturn:
                    return false;
            }
            return true;
        }
        private TypeDesc GetNativeTypeFromMarshallerKind(TypeDesc type, MarshallerKind kind, MarshallerKind elementMarshallerKind,
                MarshalAsDescriptor marshalAs)
        {
            TypeSystemContext context = PInvokeMethodData.Context;
            NativeType nativeType = TypeSystem.NativeType.Invalid;
            if (marshalAs != null)
                nativeType = marshalAs.Type;

            switch (kind)
            {
                case MarshallerKind.BlittableValue:
                    {
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.I1:
                                return context.GetWellKnownType(WellKnownType.SByte);
                            case TypeSystem.NativeType.U1:
                                return context.GetWellKnownType(WellKnownType.Byte);
                            case TypeSystem.NativeType.I2:
                                return context.GetWellKnownType(WellKnownType.Int16);
                            case TypeSystem.NativeType.U2:
                                return context.GetWellKnownType(WellKnownType.UInt16);
                            case TypeSystem.NativeType.I4:
                                return context.GetWellKnownType(WellKnownType.Int32);
                            case TypeSystem.NativeType.U4:
                                return context.GetWellKnownType(WellKnownType.UInt32);
                            case TypeSystem.NativeType.I8:
                                return context.GetWellKnownType(WellKnownType.Int64);
                            case TypeSystem.NativeType.U8:
                                return context.GetWellKnownType(WellKnownType.UInt64);
                            case TypeSystem.NativeType.R4:
                                return context.GetWellKnownType(WellKnownType.Single);
                            case TypeSystem.NativeType.R8:
                                return context.GetWellKnownType(WellKnownType.Double);
                            default:
                                return type.UnderlyingType;
                        }
                    }

                case MarshallerKind.Bool:
                    return context.GetWellKnownType(WellKnownType.Int32);

                case MarshallerKind.Enum:
                case MarshallerKind.BlittableStruct:
                case MarshallerKind.Struct:
                case MarshallerKind.Decimal:
                case MarshallerKind.VoidReturn:
                    return type;

                case MarshallerKind.BlittableStructPtr:
                    return type.MakePointerType();

                case MarshallerKind.HandleRef:
                    return context.GetWellKnownType(WellKnownType.IntPtr);

                case MarshallerKind.UnicodeChar:
                    if (nativeType == TypeSystem.NativeType.U2)
                        return context.GetWellKnownType(WellKnownType.UInt16);
                    else
                        return context.GetWellKnownType(WellKnownType.Int16);

                case MarshallerKind.OleDateTime:
                    return context.GetWellKnownType(WellKnownType.Double);

                case MarshallerKind.SafeHandle:
                case MarshallerKind.CriticalHandle:
                    return context.GetWellKnownType(WellKnownType.IntPtr);

                case MarshallerKind.UnicodeString:
                case MarshallerKind.UnicodeStringBuilder:
                    return context.GetWellKnownType(WellKnownType.Char).MakePointerType();

                case MarshallerKind.AnsiString:
                case MarshallerKind.AnsiStringBuilder:
                    return context.GetWellKnownType(WellKnownType.Byte).MakePointerType();

                case MarshallerKind.CBool:
                    return context.GetWellKnownType(WellKnownType.Byte);

                case MarshallerKind.BlittableArray:
                case MarshallerKind.Array:
                case MarshallerKind.AnsiCharArray:
                    {
                        ArrayType arrayType = type as ArrayType;
                        Debug.Assert(arrayType != null, "Expecting array");

                        //
                        // We need to construct the unsafe array from the right unsafe array element type
                        //
                        TypeDesc elementNativeType = GetNativeTypeFromMarshallerKind(
                            arrayType.ElementType,
                            ElementMarshallerKind,
                            MarshallerKind.Unknown, null);

                        return elementNativeType.MakePointerType();
                    }

                case MarshallerKind.AnsiChar:
                    return context.GetWellKnownType(WellKnownType.Byte);

                case MarshallerKind.FunctionPointer:
                    return context.GetWellKnownType(WellKnownType.IntPtr);

                case MarshallerKind.ByValArray:
                case MarshallerKind.ByValAnsiCharArray:
                case MarshallerKind.Unknown:
                default:
                    Debug.Assert(false, "unknown/unexpected marshaller kind: " + kind);
                    return null;
            }
        }


        private static MarshallerKind GetMarshallerKind(
            TypeDesc type,
            ParameterMetadata parameterData,
            PInvokeMethodData methodData,
            MarshallerType marshallerType,
            out MarshallerKind elementMarshallerKind)
        {
            if (type.IsByRef)
            {
                type = type.GetParameterType();
            }

            NativeType nativeType = TypeSystem.NativeType.Invalid;
            bool isReturn = parameterData.Return;
            MarshalAsDescriptor marshalAs = parameterData.MarshalAsDescriptor;
            bool isField = marshallerType == MarshallerType.Field;

            if (marshalAs != null)
                nativeType = (NativeType)marshalAs.Type;


            bool isAnsi = (methodData.GetCharSet() & PInvokeAttributes.CharSetAnsi) == PInvokeAttributes.CharSetAnsi;
            elementMarshallerKind = MarshallerKind.Invalid;

            //
            // Determine MarshalerKind
            //
            // This mostly resembles desktop CLR and .NET Native code as we need to match their behavior
            // 
            if (type.IsPrimitive)
            {
                switch (type.Category)
                {
                    case TypeFlags.Void:
                        return MarshallerKind.VoidReturn;

                    case TypeFlags.Boolean:
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.Invalid:
                            case TypeSystem.NativeType.Boolean:
                                return MarshallerKind.Bool;

                            case TypeSystem.NativeType.U1:
                            case TypeSystem.NativeType.I1:
                                return MarshallerKind.CBool;

                            default:
                                return MarshallerKind.Invalid;
                        }

                    case TypeFlags.Char:
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.I1:
                            case TypeSystem.NativeType.U1:
                                return MarshallerKind.AnsiChar;

                            case TypeSystem.NativeType.I2:
                            case TypeSystem.NativeType.U2:
                                return MarshallerKind.UnicodeChar;

                            case TypeSystem.NativeType.Invalid:
                                if (isAnsi)
                                    return MarshallerKind.AnsiChar;
                                else
                                    return MarshallerKind.UnicodeChar;
                            default:
                                return MarshallerKind.Invalid;
                        }

                    case TypeFlags.SByte:
                    case TypeFlags.Byte:
                        if (nativeType == TypeSystem.NativeType.I1 || nativeType == TypeSystem.NativeType.U1 || nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int16:
                    case TypeFlags.UInt16:
                        if (nativeType == TypeSystem.NativeType.I2 || nativeType == TypeSystem.NativeType.U2 || nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int32:
                    case TypeFlags.UInt32:
                        if (nativeType == TypeSystem.NativeType.I4 || nativeType == TypeSystem.NativeType.U4 || nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int64:
                    case TypeFlags.UInt64:
                        if (nativeType == TypeSystem.NativeType.I8 || nativeType == TypeSystem.NativeType.U8 || nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.IntPtr:
                    case TypeFlags.UIntPtr:
                        if (nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Single:
                        if (nativeType == TypeSystem.NativeType.R4 || nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Double:
                        if (nativeType == TypeSystem.NativeType.R8 || nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    default:
                        return MarshallerKind.Invalid;
                }
            }
            else if (type.IsValueType)
            {
                if (type.IsEnum)
                    return MarshallerKind.Enum;

                if (methodData.IsSystemDateTime(type))
                {
                    if (nativeType == TypeSystem.NativeType.Invalid ||
                        nativeType == TypeSystem.NativeType.Struct)
                        return MarshallerKind.OleDateTime;
                    else
                        return MarshallerKind.Invalid;
                }
                /*              
                                TODO: Bring HandleRef to CoreLib
                                https://github.com/dotnet/corert/issues/2570

                                else if (methodData.IsHandleRef(type))
                                {
                                    if (nativeType == NativeType.Invalid)
                                        return MarshallerKind.HandleRef;
                                    else
                                        return MarshallerKind.Invalid;
                                }
                */

                switch (nativeType)
                {
                    case TypeSystem.NativeType.Invalid:
                    case TypeSystem.NativeType.Struct:
                        if (methodData.IsSystemDecimal(type))
                            return MarshallerKind.Decimal;
                        break;

                    case TypeSystem.NativeType.LPStruct:
                        if (methodData.IsSystemGuid(type) ||
                            methodData.IsSystemDecimal(type))
                        {
                            if (isField || isReturn)
                                return MarshallerKind.Invalid;
                            else
                                return MarshallerKind.BlittableStructPtr;
                        }
                        break;

                    default:
                        return MarshallerKind.Invalid;
                }

                if (MarshalHelpers.IsBlittableType(type))
                {
                    return MarshallerKind.BlittableStruct;
                }
                else
                {
                    return MarshallerKind.Struct;
                }
            }
            else                  // !ValueType
            {
                if (type.Category == TypeFlags.Class)
                {
                    if (type.IsString)
                    {
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.LPWStr:
                                return MarshallerKind.UnicodeString;

                            case TypeSystem.NativeType.LPStr:
                                return MarshallerKind.AnsiString;

                            case TypeSystem.NativeType.Invalid:
                                if (isAnsi)
                                    return MarshallerKind.AnsiString;
                                else
                                    return MarshallerKind.UnicodeString;

                            default:
                                return MarshallerKind.Invalid;
                        }
                    }
                    else if (type.IsDelegate)
                    {
                        if (nativeType == TypeSystem.NativeType.Invalid || nativeType == TypeSystem.NativeType.Func)
                            return MarshallerKind.FunctionPointer;
                        else
                            return MarshallerKind.Invalid;
                    }
                    else if (type.IsObject)
                    {
                        if (nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.Variant;
                        else
                            return MarshallerKind.Invalid;
                    }
                    else if (methodData.IsStringBuilder(type))
                    {
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.Invalid:
                                if (isAnsi)
                                {
                                    return MarshallerKind.AnsiStringBuilder;
                                }
                                else
                                {
                                    return MarshallerKind.UnicodeStringBuilder;
                                }

                            case TypeSystem.NativeType.LPStr:
                                return MarshallerKind.AnsiStringBuilder;

                            case TypeSystem.NativeType.LPWStr:
                                return MarshallerKind.UnicodeStringBuilder;
                            default:
                                return MarshallerKind.Invalid;
                        }
                    }
                    else if (methodData.IsSafeHandle(type))
                    {
                        if (nativeType == TypeSystem.NativeType.Invalid)
                            return MarshallerKind.SafeHandle;
                        else
                            return MarshallerKind.Invalid;
                    }
                    /*
                                        TODO: Bring CriticalHandle to CoreLib
                                        https://github.com/dotnet/corert/issues/2570

                                        else if (methodData.IsCriticalHandle(type))
                                        {
                                            if (nativeType != NativeType.Invalid || isField)
                                            {
                                                return MarshallerKind.Invalid;
                                            }
                                            else
                                            {
                                                return MarshallerKind.CriticalHandle;
                                            }
                                        }
                    */
                    return MarshallerKind.Invalid;
                }
                else if (methodData.IsSystemArray(type))
                {
                    return MarshallerKind.Invalid;
                }
                else if (type.IsSzArray)
                {
                    if (nativeType == TypeSystem.NativeType.Invalid)
                        nativeType = TypeSystem.NativeType.Array;

                    switch (nativeType)
                    {
                        case TypeSystem.NativeType.Array:
                            {
                                if (isField || isReturn)
                                    return MarshallerKind.Invalid;

                                var arrayType = (ArrayType)type;

                                elementMarshallerKind = GetArrayElementMarshallerKind(
                                    arrayType,
                                    marshalAs,
                                    methodData);

                                // If element is invalid type, the array itself is invalid
                                if (elementMarshallerKind == MarshallerKind.Invalid)
                                    return MarshallerKind.Invalid;

                                if (elementMarshallerKind == MarshallerKind.AnsiChar)
                                    return MarshallerKind.AnsiCharArray;
                                else if (elementMarshallerKind == MarshallerKind.UnicodeChar    // Arrays of unicode char should be marshalled as blittable arrays
                                    || elementMarshallerKind == MarshallerKind.Enum
                                    || elementMarshallerKind == MarshallerKind.BlittableValue)
                                    return MarshallerKind.BlittableArray;
                                else
                                    return MarshallerKind.Array;
                            }

                        case TypeSystem.NativeType.ByValArray:         // fix sized array
                            {
                                var arrayType = (ArrayType)type;
                                elementMarshallerKind = GetArrayElementMarshallerKind(
                                    arrayType,
                                    marshalAs,
                                    methodData);

                                // If element is invalid type, the array itself is invalid
                                if (elementMarshallerKind == MarshallerKind.Invalid)
                                    return MarshallerKind.Invalid;

                                if (elementMarshallerKind == MarshallerKind.AnsiChar)
                                    return MarshallerKind.ByValAnsiCharArray;
                                else
                                    return MarshallerKind.ByValArray;
                            }

                        default:
                            return MarshallerKind.Invalid;
                    }
                }
                else if (type.Category == TypeFlags.Pointer)
                {
                    //
                    // @TODO - add checks for the pointee type in case the pointee type is not blittable
                    // C# already does this and will emit compilation errors (can't declare pointers to 
                    // managed type).
                    //
                    if (nativeType == TypeSystem.NativeType.Invalid)
                        return MarshallerKind.BlittableValue;
                    else
                        return MarshallerKind.Invalid;
                }
            }

            return MarshallerKind.Invalid;
        }

        protected static MarshallerKind GetArrayElementMarshallerKind(
                   ArrayType arrayType,
                   MarshalAsDescriptor marshalAs,
                   PInvokeMethodData methodData)
        {
            TypeDesc elementType = arrayType.ElementType;
            bool isAnsi = (methodData.GetCharSet() & PInvokeAttributes.CharSetAnsi) == PInvokeAttributes.CharSetAnsi;
            NativeType nativeType = TypeSystem.NativeType.Invalid;

            if (marshalAs != null)
                nativeType = (NativeType)marshalAs.ArraySubType;

            if (elementType.IsPrimitive)
            {
                switch (elementType.Category)
                {
                    case TypeFlags.Char:
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.I1:
                            case TypeSystem.NativeType.U1:
                                return MarshallerKind.AnsiChar;
                            case TypeSystem.NativeType.I2:
                            case TypeSystem.NativeType.U2:
                                return MarshallerKind.UnicodeChar;
                            default:
                                if (isAnsi)
                                    return MarshallerKind.AnsiChar;
                                else
                                    return MarshallerKind.UnicodeChar;
                        }

                    case TypeFlags.Boolean:
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.Boolean:
                                return MarshallerKind.Bool;
                            case TypeSystem.NativeType.I1:
                            case TypeSystem.NativeType.U1:
                                return MarshallerKind.CBool;
                            case TypeSystem.NativeType.Invalid:
                            default:
                                return MarshallerKind.Bool;
                        }
                    case TypeFlags.IntPtr:
                    case TypeFlags.UIntPtr:
                        return MarshallerKind.BlittableValue;

                    case TypeFlags.Void:
                        return MarshallerKind.Invalid;

                    case TypeFlags.SByte:
                    case TypeFlags.Int16:
                    case TypeFlags.Int32:
                    case TypeFlags.Int64:
                    case TypeFlags.Byte:
                    case TypeFlags.UInt16:
                    case TypeFlags.UInt32:
                    case TypeFlags.UInt64:
                    case TypeFlags.Single:
                    case TypeFlags.Double:
                        return MarshallerKind.BlittableValue;
                    default:
                        return MarshallerKind.Invalid;
                }
            }
            else if (elementType.IsValueType)
            {
                if (elementType.IsEnum)
                    return MarshallerKind.Enum;

                if (methodData.IsSystemDecimal(elementType))
                {
                    switch (nativeType)
                    {
                        case TypeSystem.NativeType.Invalid:
                        case TypeSystem.NativeType.Struct:
                            return MarshallerKind.Decimal;

                        case TypeSystem.NativeType.LPStruct:
                            return MarshallerKind.BlittableStructPtr;

                        default:
                            return MarshallerKind.Invalid;
                    }
                }
                else if (methodData.IsSystemGuid(elementType))
                {
                    switch (nativeType)
                    {
                        case TypeSystem.NativeType.Invalid:
                        case TypeSystem.NativeType.Struct:
                            return MarshallerKind.BlittableValue;

                        case TypeSystem.NativeType.LPStruct:
                            return MarshallerKind.BlittableStructPtr;

                        default:
                            return MarshallerKind.Invalid;
                    }
                }
                else if (methodData.IsSystemDateTime(elementType))
                {
                    if (nativeType == TypeSystem.NativeType.Invalid ||
                        nativeType == TypeSystem.NativeType.Struct)
                    {
                        return MarshallerKind.OleDateTime;
                    }
                    else
                    {
                        return MarshallerKind.Invalid;
                    }
                }
                /*              
                                TODO: Bring HandleRef to CoreLib
                                https://github.com/dotnet/corert/issues/2570

                                else if (methodData.IsHandleRef(elementType))
                                {
                                    return MarshallerKind.HandleRef;
                                }
                */
                else
                {

                    if (MarshalHelpers.IsBlittableType(elementType))
                    {
                        switch (nativeType)
                        {
                            case TypeSystem.NativeType.Invalid:
                            case TypeSystem.NativeType.Struct:
                                return MarshallerKind.BlittableStruct;

                            default:
                                return MarshallerKind.Invalid;
                        }
                    }
                    else
                    {
                        // TODO: Differentiate between struct and Union, we only need to support struct not union here
                        return MarshallerKind.Struct;
                    }
                }
            }
            else                          //  !valueType
            {
                if (elementType.IsString)
                {
                    switch (nativeType)
                    {
                        case TypeSystem.NativeType.Invalid:
                            if (isAnsi)
                                return MarshallerKind.AnsiString;
                            else
                                return MarshallerKind.UnicodeString;
                        case TypeSystem.NativeType.LPStr:
                            return MarshallerKind.AnsiString;
                        case TypeSystem.NativeType.LPWStr:
                            return MarshallerKind.UnicodeString;
                        default:
                            return MarshallerKind.Invalid;
                    }
                }

                if (elementType.IsObject)
                {
                    if (nativeType == TypeSystem.NativeType.Invalid)
                        return MarshallerKind.Variant;
                    else
                        return MarshallerKind.Invalid;
                }

                if (elementType.IsSzArray)
                {
                    return MarshallerKind.Invalid;
                }

                if (elementType.IsPointer)
                {
                    return MarshallerKind.Invalid;
                }

                if (methodData.IsSafeHandle(elementType))
                {
                    return MarshallerKind.Invalid;
                }
                /*          
                                TODO: Bring CriticalHandle to CoreLib
                                https://github.com/dotnet/corert/issues/2570

                                if (methodData.IsCriticalHandle(elementType))
                                {
                                    return MarshallerKind.Invalid;
                                }
                */
            }

            return MarshallerKind.Invalid;
        }
        #endregion

        public virtual void EmitMarshallingIL(PInvokeILCodeStreams pInvokeILCodeStreams)
        {
            _ilCodeStreams = pInvokeILCodeStreams;

            switch (MarshallerType)
            {
                case MarshallerType.Argument: EmitArgumentMarshallingIL(); return;
                case MarshallerType.Element: EmitElementMarshallingIL(); return;
            }
        }

        public void EmitArgumentMarshallingIL()
        {
            switch (MarshalDirection)
            {
                case MarshalDirection.Forward: EmitForwardArgumentMarshallingIL(); return;
                case MarshalDirection.Reverse: EmitReverseArgumentMarshallingIL(); return;
            }
        }

        public void EmitElementMarshallingIL()
        {
            switch (MarshalDirection)
            {
                case MarshalDirection.Forward: EmitForwardElementMarshallingIL(); return;
                case MarshalDirection.Reverse: EmitReverseElementMarshallingIL(); return;
            }
        }

        protected virtual void EmitForwardArgumentMarshallingIL()
        {
            if (Return)
            {
                EmitMarshalReturnValueManagedToNative();
            }
            else
            {
                EmitMarshalArgumentManagedToNative();
            }
        }

        protected virtual void EmitReverseArgumentMarshallingIL()
        {
            if (Return)
            {
                EmitMarshalReturnValueNativeToManaged();
            }
            else
            {
                EmitMarshalArgumentNativeToManaged();
            }
        }

        protected virtual void EmitForwardElementMarshallingIL()
        {
            if (In)
                EmitMarshalElementManagedToNative();
            else
                EmitMarshalElementNativeToManaged();
        }
        
        protected virtual void EmitReverseElementMarshallingIL()
        {
            if (In)
                EmitMarshalElementNativeToManaged();
            else
                EmitMarshalElementManagedToNative();
        }

        protected virtual void EmitMarshalReturnValueManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            _vManaged = emitter.NewLocal(ManagedType);
            _vNative = emitter.NewLocal(NativeType);
            _ilCodeStreams.ReturnValueMarshallingCodeStream.EmitStLoc(_vNative);

            AllocAndTransformNativeToManaged(_ilCodeStreams.ReturnValueMarshallingCodeStream);

            _ilCodeStreams.ReturnValueMarshallingCodeStream.EmitLdLoc(_vManaged);
        }

        protected virtual void LoadArguments()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            _vManaged = emitter.NewLocal(ManagedType);
            _vNative = emitter.NewLocal(NativeType);
            marshallingCodeStream.EmitLdArg(PInvokeParameterMetadata.Index - 1);
            bool isForwardInterop = MarshalDirection == MarshalDirection.Forward;
            if (!isForwardInterop && IsNativeByRef)
                EmitLdInd(marshallingCodeStream, NativeType);
            marshallingCodeStream.EmitStLoc(isForwardInterop? _vManaged : _vNative);
        }

        protected virtual void EmitMarshalArgumentManagedToNative()
        {
            LoadArguments();

            //
            // marshal
            //
            if (IsManagedByRef && !In)
            {
                ReInitNativeTransform();
            }
            else
            {
                AllocAndTransformManagedToNative(_ilCodeStreams.MarshallingCodeStream);
            }

            if (IsNativeByRef)
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoca(_vNative);
            else
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoc(_vNative);

            //
            // unmarshal
            //
            if (Out)
            {
                if (In)
                {
                    ClearManagedTransform(_ilCodeStreams.UnmarshallingCodestream);
                }

                if (IsManagedByRef && !In)
                {
                    AllocNativeToManaged(_ilCodeStreams.UnmarshallingCodestream);
                }

                TransformNativeToManaged(_ilCodeStreams.UnmarshallingCodestream);
            }
            EmitCleanupManagedToNative();
        }

        /// <summary>
        /// Reads managed parameter from _vManaged and writes the marshalled parameter in _vNative
        /// </summary>
        protected virtual void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            AllocManagedToNative(codeStream);
            if (In)
            {
                TransformManagedToNative(codeStream);
            }
        }

        protected virtual void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            AllocNativeToManaged(codeStream);
            TransformNativeToManaged(codeStream);
        }

        protected virtual void AllocManagedToNative(ILCodeStream codeStream)
        {
        }
        protected virtual void TransformManagedToNative(ILCodeStream codeStream)
        {
            codeStream.EmitLdLoc(_vManaged);
            codeStream.EmitStLoc(_vNative);
        }

        protected virtual void ClearManagedTransform(ILCodeStream codeStream)
        {
        }
        protected virtual void AllocNativeToManaged(ILCodeStream codeStream)
        {
        }

        protected virtual void TransformNativeToManaged(ILCodeStream codeStream)
        {
            codeStream.EmitLdLoc(_vNative);
            codeStream.EmitStLoc(_vManaged);
        }

        protected virtual void EmitCleanupManagedToNative()
        {
        }

        protected virtual void EmitMarshalReturnValueNativeToManaged()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            _vManaged = emitter.NewLocal(ManagedType);
            _vNative = emitter.NewLocal(NativeType);
            _ilCodeStreams.ReturnValueMarshallingCodeStream.EmitStLoc(_vManaged);

            AllocAndTransformManagedToNative(_ilCodeStreams.ReturnValueMarshallingCodeStream);

            _ilCodeStreams.ReturnValueMarshallingCodeStream.EmitLdLoc(_vNative);
        }

        protected virtual void EmitMarshalArgumentNativeToManaged()
        {
            LoadArguments();

            AllocAndTransformNativeToManaged(_ilCodeStreams.MarshallingCodeStream);

            if (IsManagedByRef)
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoca(_vManaged);
            else
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoc(_vManaged);

            if (Out)
            {
                TransformManagedToNative(_ilCodeStreams.UnmarshallingCodestream);
            }
        }

        protected virtual void EmitMarshalElementManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            _vManaged = emitter.NewLocal(ManagedType);
            _vNative = emitter.NewLocal(NativeType);
            marshallingCodeStream.EmitStLoc(_vManaged);

            // marshal
            AllocAndTransformManagedToNative(marshallingCodeStream);

            marshallingCodeStream.EmitLdLoc(_vNative);
        }

        protected virtual void EmitMarshalElementNativeToManaged()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream codeStream;
            if (Return)
                codeStream = _ilCodeStreams.ReturnValueMarshallingCodeStream;
            else
                codeStream = _ilCodeStreams.UnmarshallingCodestream;


            _vManaged = emitter.NewLocal(ManagedType);
            _vNative = emitter.NewLocal(NativeType);
            codeStream.EmitStLoc(_vNative);

            // unmarshal
            AllocAndTransformNativeToManaged(codeStream);
            codeStream.EmitLdLoc(_vManaged);
        }

        protected virtual void ReInitNativeTransform()
        {
        }

        protected void EmitLdInd(ILCodeStream codestream, TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                case TypeFlags.Boolean:
                    codestream.Emit(ILOpcode.ldind_i1);
                    break;
                case TypeFlags.Char:
                case TypeFlags.UInt16:
                case TypeFlags.Int16:
                    codestream.Emit(ILOpcode.ldind_i2);
                    break;
                case TypeFlags.UInt32:
                case TypeFlags.Int32:
                    codestream.Emit(ILOpcode.ldind_i4);
                    break;
                case TypeFlags.UInt64:
                case TypeFlags.Int64:
                    codestream.Emit(ILOpcode.ldind_i8);
                    break;
                case TypeFlags.Single:
                    codestream.Emit(ILOpcode.ldind_r4);
                    break;
                case TypeFlags.Double:
                    codestream.Emit(ILOpcode.ldind_r8);
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    codestream.Emit(ILOpcode.ldind_i);
                    break;
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                    codestream.Emit(ILOpcode.ldind_ref);
                    break;
                default:
                    codestream.Emit(ILOpcode.ldobj, _ilCodeStreams.Emitter.NewToken(type));
                    break;
            }
        }
        protected void EmitStInd(ILCodeStream codestream, TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                case TypeFlags.Boolean:
                    codestream.Emit(ILOpcode.stind_i1);
                    break;
                case TypeFlags.Char:
                case TypeFlags.UInt16:
                case TypeFlags.Int16:
                    codestream.Emit(ILOpcode.stind_i2);
                    break;
                case TypeFlags.UInt32:
                case TypeFlags.Int32:
                    codestream.Emit(ILOpcode.stind_i4);
                    break;
                case TypeFlags.UInt64:
                case TypeFlags.Int64:
                    codestream.Emit(ILOpcode.stind_i8);
                    break;
                case TypeFlags.Single:
                    codestream.Emit(ILOpcode.stind_r4);
                    break;
                case TypeFlags.Double:
                    codestream.Emit(ILOpcode.stind_r8);
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    codestream.Emit(ILOpcode.stind_i);
                    break;
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                    codestream.Emit(ILOpcode.stind_ref);
                    break;
                default:
                    codestream.Emit(ILOpcode.stobj, _ilCodeStreams.Emitter.NewToken(type));
                    break;
            }
        }

        protected void EmitStElem(ILCodeStream codestream, TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                case TypeFlags.Boolean:
                    codestream.Emit(ILOpcode.stelem_i1);
                    break;
                case TypeFlags.Char:
                case TypeFlags.UInt16:
                case TypeFlags.Int16:
                    codestream.Emit(ILOpcode.stelem_i2);
                    break;
                case TypeFlags.UInt32:
                case TypeFlags.Int32:
                    codestream.Emit(ILOpcode.stelem_i4);
                    break;
                case TypeFlags.UInt64:
                case TypeFlags.Int64:
                    codestream.Emit(ILOpcode.stelem_i8);
                    break;
                case TypeFlags.Single:
                    codestream.Emit(ILOpcode.stelem_r4);
                    break;
                case TypeFlags.Double:
                    codestream.Emit(ILOpcode.stelem_r8);
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    codestream.Emit(ILOpcode.stelem_i);
                    break;
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                    codestream.Emit(ILOpcode.stelem_ref);
                    break;
                default:
                    codestream.Emit(ILOpcode.stelem, _ilCodeStreams.Emitter.NewToken(type));
                    break;
            }
        }

        protected void EmitLdElem(ILCodeStream codestream, TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                case TypeFlags.Boolean:
                    codestream.Emit(ILOpcode.ldelem_i1);
                    break;
                case TypeFlags.Char:
                case TypeFlags.UInt16:
                case TypeFlags.Int16:
                    codestream.Emit(ILOpcode.ldelem_i2);
                    break;
                case TypeFlags.UInt32:
                case TypeFlags.Int32:
                    codestream.Emit(ILOpcode.ldelem_i4);
                    break;
                case TypeFlags.UInt64:
                case TypeFlags.Int64:
                    codestream.Emit(ILOpcode.ldelem_i8);
                    break;
                case TypeFlags.Single:
                    codestream.Emit(ILOpcode.ldelem_r4);
                    break;
                case TypeFlags.Double:
                    codestream.Emit(ILOpcode.ldelem_r8);
                    break;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    codestream.Emit(ILOpcode.ldelem_i);
                    break;
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                    codestream.Emit(ILOpcode.ldelem_ref);
                    break;
                default:
                    codestream.Emit(ILOpcode.ldelem, _ilCodeStreams.Emitter.NewToken(type));
                    break;
            }
        }
    }

    class VoidReturnMarshaller : Marshaller
    {
        protected override void EmitMarshalReturnValueManagedToNative()
        {
        }
        protected override void EmitMarshalReturnValueNativeToManaged()
        {
        }
    }

    class BlittableValueMarshaller : BlittableByRefMarshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            if (Out)
            {
                ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
                ILEmitter emitter = _ilCodeStreams.Emitter;
                _vNative = emitter.NewLocal(PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr));


                ILLocalVariable vPinnedByRef = emitter.NewLocal(ManagedParameterType, true);
                marshallingCodeStream.EmitLdArg(PInvokeParameterMetadata.Index - 1);
                marshallingCodeStream.EmitStLoc(vPinnedByRef);
                marshallingCodeStream.EmitLdLoc(vPinnedByRef);
                marshallingCodeStream.Emit(ILOpcode.conv_i);
                marshallingCodeStream.EmitStLoc(_vNative);

                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoc(_vNative);
            }
            else
            {
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdArg(PInvokeParameterMetadata.Index - 1);
            }
        }

        protected override void EmitMarshalArgumentNativeToManaged()
        {
            if (Out)
            {
                base.EmitMarshalArgumentNativeToManaged();
            }
            else
            {
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdArg(PInvokeParameterMetadata.Index - 1);
            }
        }
    }

    class ArrayMarshaller : Marshaller
    {
        
        private Marshaller _elementMarshaller;

        protected Marshaller GetElementMarshaller(MarshalDirection direction)
        {
            if (_elementMarshaller == null)
            {
                _elementMarshaller = CreateMarshaller(ElementMarshallerKind);
                _elementMarshaller.MarshallerKind = ElementMarshallerKind;
                _elementMarshaller.MarshallerType = MarshallerType.Element;
                _elementMarshaller.Return = Return;
                _elementMarshaller.PInvokeMethodData = PInvokeMethodData;
                _elementMarshaller.PInvokeMethodData = PInvokeMethodData;
                if (IsManagedByRef)
                    _elementMarshaller.ManagedType = ((ArrayType)((ByRefType)ManagedType).ParameterType).ElementType;
                else
                    _elementMarshaller.ManagedType = ((ArrayType)ManagedType).ElementType;
            }
            _elementMarshaller.In = (direction == MarshalDirection);
            _elementMarshaller.Out = !In;
            _elementMarshaller.MarshalDirection = MarshalDirection;

            return _elementMarshaller;
        }

        protected virtual void EmitElementCount(ILCodeStream codeStream, MarshalDirection direction)
        {
            if (direction == MarshalDirection.Forward)
            {
                // In forward direction we skip whatever is passed through SizeParamIndex, becaus the
                // size of the managed array is already known
                codeStream.EmitLdLoc(_vManaged);
                codeStream.Emit(ILOpcode.ldlen);
                codeStream.Emit(ILOpcode.conv_i4);

            }
            else if (MarshalDirection == MarshalDirection.Forward
                    && MarshallerType == MarshallerType.Argument
                    && !Return
                    && !IsManagedByRef)
            {
                EmitElementCount(codeStream, MarshalDirection.Forward);
            }
            else
            { 

                uint? sizeParamIndex = PInvokeParameterMetadata.MarshalAsDescriptor.SizeParamIndex;
                uint? sizeConst = PInvokeParameterMetadata.MarshalAsDescriptor.SizeConst;

                if (sizeConst.HasValue)
                {
                    codeStream.EmitLdc((int)sizeConst.Value);
                }

                if (sizeParamIndex.HasValue)
                {
                    uint index = sizeParamIndex.Value;

                    if (index < 0 || index >= Marshallers.Length -1)
                    {
                        throw new InvalidProgramException("Invalid SizeParamIndex, must be between 0 and parameter count");
                    }

                    //zero-th index is for return type
                    index++;
                    var indexType = Marshallers[index].ManagedType;
                    bool isByRef = indexType.IsByRef;

                    if (isByRef)
                    {
                        indexType = ((ByRefType)indexType).ParameterType;
                    }

                    switch (indexType.Category)
                    {
                        case TypeFlags.Byte:
                        case TypeFlags.SByte:
                        case TypeFlags.Int16:
                        case TypeFlags.UInt16:
                        case TypeFlags.Int32:
                        case TypeFlags.UInt32:
                        case TypeFlags.Int64:
                        case TypeFlags.UInt64:
                            break;
                        default:
                            throw new InvalidProgramException("Invalid SizeParamIndex, parameter must be  of type int/uint");
                    }

                    codeStream.EmitLdArg(Marshallers[index].PInvokeParameterMetadata.Index - 1);

                    if (isByRef)
                        EmitLdInd(codeStream, indexType);

                    if (sizeConst.HasValue)
                        codeStream.Emit(ILOpcode.add);
                }

                if (!sizeConst.HasValue && !sizeParamIndex.HasValue)
                {
                    // if neither sizeConst or sizeParamIndex are specified, default to 1
                    codeStream.EmitLdc(1);
                }
            }
        }

        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var arrayType = (ArrayType)ManagedType;
            var elementType = arrayType.ElementType;
            TypeSystemContext context = PInvokeMethodData.Context;

            ILLocalVariable vSizeOf = emitter.NewLocal(context.GetWellKnownType(WellKnownType.IntPtr));
            ILLocalVariable vLength = emitter.NewLocal(context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable vNativeTemp = emitter.NewLocal(NativeType);

            ILCodeLabel lNullArray = emitter.NewCodeLabel();
            ILCodeLabel lRangeCheck = emitter.NewCodeLabel();
            ILCodeLabel lLoopHeader = emitter.NewCodeLabel();

            codeStream.EmitLdLoca(_vNative);
            codeStream.Emit(ILOpcode.initobj, emitter.NewToken(NativeType));
            
            // Check for null array
            codeStream.EmitLdLoc(_vManaged);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);

            // allocate memory
            // nativeParameter = (byte**)CoTaskMemAllocAndZeroMemory((IntPtr)(checked(manageParameter.Length * sizeof(byte*))));

            EmitElementCount(codeStream, MarshalDirection.Forward);
            codeStream.Emit(ILOpcode.dup);
            codeStream.EmitStLoc(vLength);

            TypeDesc nativeType = ((PointerType)NativeType).ParameterType;
            if (elementType.IsPrimitive)
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(elementType));
            else
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(context.GetWellKnownType(WellKnownType.IntPtr)));

            codeStream.Emit(ILOpcode.dup);
            codeStream.EmitStLoc(vSizeOf);
            codeStream.Emit(ILOpcode.mul_ovf);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                                context.SystemModule.
                                    GetKnownType("System", "IntPtr").
                                        GetKnownMethod("op_Explicit", null)));

            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                                context.GetHelperEntryPoint("InteropHelpers", "CoTaskMemAllocAndZeroMemory")));
            codeStream.EmitStLoc(_vNative);

            // initialize content
            var vIndex = emitter.NewLocal(context.GetWellKnownType(WellKnownType.Int32));

            codeStream.EmitLdLoc(_vNative);
            codeStream.EmitStLoc(vNativeTemp);
            codeStream.EmitLdc(0);
            codeStream.EmitStLoc(vIndex);
            codeStream.Emit(ILOpcode.br, lRangeCheck);

            codeStream.EmitLabel(lLoopHeader);
            codeStream.EmitLdLoc(vNativeTemp);

            codeStream.EmitLdLoc(_vManaged);
            codeStream.EmitLdLoc(vIndex);
            EmitLdElem(codeStream, elementType);
            // generate marshalling IL for the element
            GetElementMarshaller(MarshalDirection.Forward).EmitMarshallingIL(_ilCodeStreams);

            EmitStInd(codeStream, elementType);
            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdc(1);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vIndex);
            codeStream.EmitLdLoc(vNativeTemp);
            codeStream.EmitLdLoc(vSizeOf);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vNativeTemp);

            codeStream.EmitLabel(lRangeCheck);

            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdLoc(vLength);
            codeStream.Emit(ILOpcode.clt);
            codeStream.Emit(ILOpcode.brtrue, lLoopHeader);


            codeStream.EmitLabel(lNullArray);
        }        

        protected override void TransformNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ArrayType arrayType;

            if (IsManagedByRef)
                arrayType = (ArrayType)((ByRefType)ManagedType).ParameterType;
            else 
                arrayType = (ArrayType)ManagedType;

            var elementType = arrayType.ElementType;
            TypeSystemContext context = PInvokeMethodData.Context;

            ILLocalVariable vSizeOf = emitter.NewLocal(context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable vLength = emitter.NewLocal(context.GetWellKnownType(WellKnownType.IntPtr));

            ILCodeLabel lRangeCheck = emitter.NewCodeLabel();
            ILCodeLabel lLoopHeader = emitter.NewCodeLabel();

            EmitElementCount(codeStream, MarshalDirection.Reverse);

            codeStream.EmitStLoc(vLength);

            if (elementType.IsPrimitive)
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(elementType));
            else 
                codeStream.Emit(ILOpcode.sizeof_, emitter.NewToken(context.GetWellKnownType(WellKnownType.IntPtr)));

            codeStream.EmitStLoc(vSizeOf);

            var vIndex = emitter.NewLocal(context.GetWellKnownType(WellKnownType.Int32));
            ILLocalVariable vNativeTemp;

            if (IsManagedByRef)
                vNativeTemp = emitter.NewLocal(((PointerType)NativeType).ParameterType);
            else
                vNativeTemp = emitter.NewLocal(NativeType);

            codeStream.EmitLdLoc(_vNative);
            codeStream.EmitStLoc(vNativeTemp);
            codeStream.EmitLdc(0);
            codeStream.EmitStLoc(vIndex);
            codeStream.Emit(ILOpcode.br, lRangeCheck);


            codeStream.EmitLabel(lLoopHeader);

            codeStream.EmitLdLoc(_vManaged);
            if (IsManagedByRef)
            {
                codeStream.Emit(ILOpcode.ldind_ref);
            }

            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdLoc(vNativeTemp);

            EmitLdInd(codeStream, elementType);
 
            // generate marshalling IL for the element
            GetElementMarshaller(MarshalDirection.Reverse).EmitMarshallingIL(_ilCodeStreams);

            EmitStElem(codeStream, elementType);

            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdc(1);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vIndex);
            codeStream.EmitLdLoc(vNativeTemp);
            codeStream.EmitLdLoc(vSizeOf);
            codeStream.Emit(ILOpcode.add);
            codeStream.EmitStLoc(vNativeTemp);


            codeStream.EmitLabel(lRangeCheck);
            codeStream.EmitLdLoc(vIndex);
            codeStream.EmitLdLoc(vLength);
            codeStream.Emit(ILOpcode.clt);
            codeStream.Emit(ILOpcode.brtrue, lLoopHeader);
        }

        protected override void AllocNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ArrayType arrayType;

            if (IsManagedByRef)
                arrayType = (ArrayType)((ByRefType)ManagedType).ParameterType;
            else
                arrayType = (ArrayType)ManagedType;

            var elementType = arrayType.ElementType;
            codeStream.EmitLdLoc(_vManaged);
            EmitElementCount(codeStream, MarshalDirection.Reverse);
            codeStream.Emit(ILOpcode.newarr, emitter.NewToken(elementType));
            codeStream.Emit(ILOpcode.stind_ref);
        }

        protected override void EmitCleanupManagedToNative()
        {
            ILCodeStream codeStream;
            if (Return)
                codeStream = _ilCodeStreams.ReturnValueMarshallingCodeStream;
            else
                codeStream = _ilCodeStreams.UnmarshallingCodestream;
            codeStream.EmitLdLoc(_vNative);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                                PInvokeMethodData.Context.GetHelperEntryPoint("InteropHelpers", "CoTaskMemFree")));
        }
    }

    class BlittableArrayMarshaller : ArrayMarshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var arrayType = (ArrayType)ManagedType;
            Debug.Assert(arrayType.IsSzArray);

            ILLocalVariable vPinnedFirstElement = emitter.NewLocal(arrayType.ParameterType.MakeByRefType(), true);
            ILCodeLabel lNullArray = emitter.NewCodeLabel();

            // Check for null array, or 0 element array.
            codeStream.EmitLdLoc(_vManaged);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);
            codeStream.EmitLdLoc(_vManaged);
            codeStream.Emit(ILOpcode.ldlen);
            codeStream.Emit(ILOpcode.conv_i4);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);

            // Array has elements.
            codeStream.EmitLdLoc(_vManaged);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ldelema, emitter.NewToken(arrayType.ElementType));
            codeStream.EmitStLoc(vPinnedFirstElement);

            // Fall through. If array didn't have elements, vPinnedFirstElement is zeroinit.
            codeStream.EmitLabel(lNullArray);
            codeStream.EmitLdLoc(vPinnedFirstElement);
            codeStream.Emit(ILOpcode.conv_i);
            codeStream.EmitStLoc(_vNative);
        }

        protected override void ReInitNativeTransform()
        {
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            marshallingCodeStream.EmitLdc(0);
            marshallingCodeStream.Emit(ILOpcode.conv_u);
            marshallingCodeStream.EmitStLoc(_vNative);
        }

        protected override void TransformNativeToManaged(ILCodeStream codeStream)
        {
            if (IsManagedByRef && !In)
                base.TransformNativeToManaged(codeStream);
        }
        protected override void EmitCleanupManagedToNative()
        {
            if (IsManagedByRef && !In)
                base.EmitCleanupManagedToNative();
        }
    }

    abstract class BlittableByRefMarshaller : Marshaller
    {
        protected virtual void EmitByRefManagedToNative()
        {
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var byRefType = (ByRefType)ManagedType;
            ILLocalVariable vPinnedByRef = emitter.NewLocal(byRefType, true);
            marshallingCodeStream.EmitLdLoc(_vManaged);
            marshallingCodeStream.EmitStLoc(vPinnedByRef);
            marshallingCodeStream.EmitLdLoc(vPinnedByRef);
            marshallingCodeStream.Emit(ILOpcode.conv_i);
            marshallingCodeStream.EmitStLoc(_vNative);
        }
    }

    class BooleanMarshaller : Marshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            codeStream.EmitLdLoc(_vManaged);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            codeStream.EmitStLoc(_vNative);
        }

        protected override void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            codeStream.EmitLdLoc(_vNative);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ceq);
            codeStream.EmitStLoc(_vManaged);
        }
    }

    class UnicodeStringMarshaller : Marshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            TypeSystemContext context = PInvokeMethodData.Context;
            //
            // Unicode marshalling. Pin the string and push a pointer to the first character on the stack.
            //

            TypeDesc stringType = context.GetWellKnownType(WellKnownType.String);

            ILLocalVariable vPinnedString = emitter.NewLocal(stringType, true);
            ILCodeLabel lNullString = emitter.NewCodeLabel();

            codeStream.EmitLdLoc(_vManaged);
            codeStream.EmitStLoc(vPinnedString);
            codeStream.EmitLdLoc(vPinnedString);

            codeStream.Emit(ILOpcode.conv_i);
            codeStream.Emit(ILOpcode.dup);

            // Marshalling a null string?
            codeStream.Emit(ILOpcode.brfalse, lNullString);

            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                context.SystemModule.
                    GetKnownType("System.Runtime.CompilerServices", "RuntimeHelpers").
                        GetKnownMethod("get_OffsetToStringData", null)));

            codeStream.Emit(ILOpcode.add);

            codeStream.EmitLabel(lNullString);
            codeStream.EmitStLoc(_vNative);
        }
    }

    class AnsiStringMarshaller : BlittableArrayMarshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            TypeSystemContext context = PInvokeMethodData.Context;
            //
            // ANSI marshalling. Allocate a byte array, copy characters, pin first element.
            //

            var stringToAnsi = context.GetHelperEntryPoint("InteropHelpers", "StringToAnsi");
            codeStream.EmitLdLoc(_vManaged);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(stringToAnsi));

            // back up the managed types 
            TypeDesc tempType  = ManagedType;
            ILLocalVariable vTemp = _vManaged;
            ManagedType = context.GetWellKnownType(WellKnownType.Byte).MakeArrayType();
            _vManaged = emitter.NewLocal(ManagedType);
            codeStream.EmitStLoc(_vManaged);
            
            // Call the Array marshaller MarshalArgument
            base.AllocAndTransformManagedToNative(codeStream);

            //restore the types
            ManagedType = tempType;
            _vManaged = vTemp;
        }

        protected override void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            TypeSystemContext context = PInvokeMethodData.Context;
            var ansiToString = context.GetHelperEntryPoint("InteropHelpers", "AnsiStringToString");
            codeStream.EmitLdLoc(_vNative);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(ansiToString));
            codeStream.EmitStLoc(_vManaged);
        }
    }

    class SafeHandleMarshaller : Marshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            ILCodeStream callsiteCodeStream = _ilCodeStreams.CallsiteSetupCodeStream;
            ILCodeStream unmarshallingCodeStream = _ilCodeStreams.UnmarshallingCodestream;

            LoadArguments();

            // we don't support [IN,OUT] together yet, either IN or OUT
            Debug.Assert(!(PInvokeParameterMetadata.Out && PInvokeParameterMetadata.In));

            var safeHandleType = PInvokeMethodData.SafeHandleType;

            if (Out)
            {
                // 1) If this is an output parameter we need to preallocate a SafeHandle to wrap the new native handle value. We
                //    must allocate this before the native call to avoid a failure point when we already have a native resource
                //    allocated. We must allocate a new SafeHandle even if we have one on input since both input and output native
                //    handles need to be tracked and released by a SafeHandle.
                // 2) Initialize a local IntPtr that will be passed to the native call. 
                // 3) After the native call, the new handle value is written into the output SafeHandle and that SafeHandle
                //    is propagated back to the caller.

                Debug.Assert(ManagedType is ByRefType);

                TypeDesc resolvedType = ((ByRefType)ManagedType).ParameterType;

                var vOutValue = emitter.NewLocal(PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr));
                var vSafeHandle = emitter.NewLocal(resolvedType);
                marshallingCodeStream.Emit(ILOpcode.newobj, emitter.NewToken(resolvedType.GetParameterlessConstructor()));
                marshallingCodeStream.EmitStLoc(vSafeHandle);
                marshallingCodeStream.EmitLdLoca(vOutValue);
                marshallingCodeStream.EmitStLoc(_vNative);

                unmarshallingCodeStream.EmitLdLoc(vSafeHandle);
                unmarshallingCodeStream.EmitLdLoc(vOutValue);
                unmarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    PInvokeMethodData.SafeHandleType.GetKnownMethod("SetHandle", null)));

                unmarshallingCodeStream.EmitLdLoc(_vManaged);
                unmarshallingCodeStream.EmitLdLoc(vSafeHandle);
                unmarshallingCodeStream.Emit(ILOpcode.stind_ref);
            }
            else
            {
                var vAddRefed = emitter.NewLocal(PInvokeMethodData.Context.GetWellKnownType(WellKnownType.Boolean));
                marshallingCodeStream.EmitLdLoc(_vManaged);
                marshallingCodeStream.EmitLdLoca(vAddRefed);
                marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousAddRef", null)));

                marshallingCodeStream.EmitLdLoc(_vManaged);
                marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousGetHandle", null)));
                marshallingCodeStream.EmitStLoc(_vNative);

                // TODO: This should be inside finally block and only executed it the handle was addrefed
                unmarshallingCodeStream.EmitLdLoc(_vManaged);
                unmarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousRelease", null)));
            }

            callsiteCodeStream.EmitLdLoc(_vNative);
        }

        protected override void AllocAndTransformNativeToManaged(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;

            marshallingCodeStream.Emit(ILOpcode.newobj, emitter.NewToken(ManagedType.GetParameterlessConstructor()));
            marshallingCodeStream.EmitStLoc(_vManaged);

            codeStream.EmitLdLoc(_vManaged);
            codeStream.EmitLdLoc(_vNative);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(
            PInvokeMethodData.SafeHandleType.GetKnownMethod("SetHandle", null)));
        }
    }

    class UnicodeStringBuilderMarshaller : BlittableArrayMarshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            TypeSystemContext context = PInvokeMethodData.Context;
            // TODO: Handles [out] marshalling only for now

            var stringBuilderType = context.SystemModule.GetKnownType("System.Text", "StringBuilder");

            codeStream.EmitLdLoc(_vManaged);
            codeStream.Emit(ILOpcode.call, emitter.NewToken(
                context.GetHelperEntryPoint("InteropHelpers", "GetEmptyStringBuilderBuffer")));

            // back up the managed types 
            TypeDesc tempType = ManagedType;
            ILLocalVariable vTemp = _vManaged;

            ManagedType = context.GetWellKnownType(WellKnownType.Char).MakeArrayType();
            _vManaged = emitter.NewLocal(ManagedType);
            codeStream.EmitStLoc(_vManaged);

            // Call the Array marshaller MarshalArgument
            base.AllocAndTransformManagedToNative(codeStream);

            //restore the types
            ManagedType = tempType;
            _vManaged = vTemp;
        }

        protected override void TransformNativeToManaged(ILCodeStream codeStream)
        {
            codeStream.EmitLdLoc(_vManaged);
            codeStream.EmitLdLoc(_vNative);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
                PInvokeMethodData.StringBuilder.GetKnownMethod("ReplaceBuffer", null)));
        }
    }

    class DelegateMarshaller : Marshaller
    {
        protected override void AllocAndTransformManagedToNative(ILCodeStream codeStream)
        {
            codeStream.EmitLdLoc(_vManaged);
            codeStream.Emit(ILOpcode.call, _ilCodeStreams.Emitter.NewToken(
            PInvokeMethodData.Context.GetHelperEntryPoint("InteropHelpers", "GetStubForPInvokeDelegate")));
            codeStream.EmitStLoc(_vNative);
        }
    }
}