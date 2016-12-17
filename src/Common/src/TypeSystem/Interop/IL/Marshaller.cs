// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Internal.TypeSystem;
using Internal.IL.Stubs;
using Debug = System.Diagnostics.Debug;
using Internal.IL;

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

    // Each type of marshaller knows how to generate the marshalling code for the argument it marshals.
    // Marshallers contain method related marshalling information (which is common to all the Marshallers)
    // and also argument specific marshalling informaiton
    abstract class Marshaller
    {
        public PInvokeMethodData PInvokeMethodData;
        #region Instance state information
        public ParameterMetadata PInvokeParameterMetadata;
        public MarshallerKind MarshallerKind;
        public TypeDesc NativeParameterType;
        public TypeDesc ManagedParameterType;
        public bool In;
        public bool Out;
        public bool Return;
        public bool Optional;
        public bool IsByRef;
        protected PInvokeILCodeStreams _ilCodeStreams;
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
        public static Marshaller CreateMarshaller(TypeDesc parameterType, PInvokeMethodData pInvokeMethodData, ParameterMetadata pInvokeParameterdata)
        {
            MarshallerKind marshallerKind = GetMarshallerKind(parameterType, 
                                                pInvokeParameterdata, 
                                                pInvokeMethodData,
                                                isField: false);

            // Create the marshaller based on MarshallerKind
            Marshaller marshaller = Marshaller.CreateMarshallerInternal(marshallerKind);
            marshaller.PInvokeMethodData = pInvokeMethodData;
            marshaller.PInvokeParameterMetadata = pInvokeParameterdata;
            marshaller.MarshallerKind = marshallerKind;
            marshaller.NativeParameterType = null;
            marshaller.ManagedParameterType = parameterType;
            marshaller.Optional = pInvokeParameterdata.Optional;
            marshaller.Return = pInvokeParameterdata.Return;
            marshaller.IsByRef = parameterType.IsByRef;
            marshaller.In = pInvokeParameterdata.In;
            //
            // Desktop ignores [Out] on marshaling scenarios where they don't make sense (such as passing
            // value types and string as [out] without byref). 
            //
            if (parameterType.IsByRef)
            {
                // Passing as [Out] by ref is valid
                marshaller.Out = pInvokeParameterdata.Out;
            }
            else
            {
                // Passing as [Out] is valid only if it is not ValueType nor string
                if (!parameterType.IsValueType && !parameterType.IsString)
                    marshaller.Out= pInvokeParameterdata.Out;
            }

            if (!marshaller.In && !marshaller.Out)
            {
                //
                // Rules for in/out
                // 1. ByRef args: [in]/[out] implied by default
                // 2. StringBuilder: [in, out] by default
                // 3. non-ByRef args: [In] is implied if no [In]/[Out] is specified
                //
                if (parameterType.IsByRef)
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
            return marshaller;
        }

        private static Marshaller CreateMarshallerInternal(MarshallerKind kind)
        {
            switch (kind)
            {
                case MarshallerKind.Enum:
                case MarshallerKind.BlittableValue:
                case MarshallerKind.BlittableStruct:
                case MarshallerKind.UnicodeChar:
                    return new BlittableValueMarshaller();
                case MarshallerKind.Array:
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

        private static MarshallerKind GetMarshallerKind(
            TypeDesc type,
            ParameterMetadata parameterData,
            PInvokeMethodData methodData,
            bool isField)
        {
            if (type.IsByRef)
            {
                var byRefType = (ByRefType)type;
                type = byRefType.ParameterType;
            }

            NativeType nativeType = NativeType.Invalid;
            bool isReturn = parameterData.Return;
            MarshalAsDescriptor marshalAs = parameterData.MarshalAsDescriptor;

            if (marshalAs != null)
                nativeType = (NativeType)marshalAs.Type;


            bool isAnsi = (methodData.GetCharSet() & PInvokeAttributes.CharSetAnsi) == PInvokeAttributes.CharSetAnsi;
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
                            case NativeType.Invalid:
                            case NativeType.Boolean:
                                return MarshallerKind.Bool;

                            case NativeType.U1:
                            case NativeType.I1:
                                return MarshallerKind.CBool;

                            default:
                                return MarshallerKind.Invalid;
                        }

                    case TypeFlags.Char:
                        switch (nativeType)
                        {
                            case NativeType.I1:
                            case NativeType.U1:
                                return MarshallerKind.AnsiChar;

                            case NativeType.I2:
                            case NativeType.U2:
                                return MarshallerKind.UnicodeChar;

                            case NativeType.Invalid:
                                if (isAnsi)
                                    return MarshallerKind.AnsiChar;
                                else
                                    return MarshallerKind.UnicodeChar;
                            default:
                                return MarshallerKind.Invalid;
                        }

                    case TypeFlags.SByte:
                    case TypeFlags.Byte:
                        if (nativeType == NativeType.I1 || nativeType == NativeType.U1 || nativeType == NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int16:
                    case TypeFlags.UInt16:
                        if (nativeType == NativeType.I2 || nativeType == NativeType.U2 || nativeType == NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int32:
                    case TypeFlags.UInt32:
                        if (nativeType == NativeType.I4 || nativeType == NativeType.U4 || nativeType == NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int64:
                    case TypeFlags.UInt64:
                        if (nativeType == NativeType.I8 || nativeType == NativeType.U8 || nativeType == NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.IntPtr:
                    case TypeFlags.UIntPtr:
                        if (nativeType == NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Single:
                        if (nativeType == NativeType.R4 || nativeType == NativeType.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Double:
                        if (nativeType == NativeType.R8 || nativeType == NativeType.Invalid)
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
                    if (nativeType == NativeType.Invalid ||
                        nativeType == NativeType.Struct)
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
                    case NativeType.Invalid:
                    case NativeType.Struct:
                        if (methodData.IsSystemDecimal(type))
                            return MarshallerKind.Decimal;
                        break;

                    case NativeType.LPStruct:
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
                            case NativeType.LPWStr:
                                return MarshallerKind.UnicodeString;

                            case NativeType.LPStr:
                                return MarshallerKind.AnsiString;

                            case NativeType.Invalid:
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
                        if (nativeType == NativeType.Invalid || nativeType == NativeType.Func)
                            return MarshallerKind.FunctionPointer;
                        else
                            return MarshallerKind.Invalid;
                    }
                    else if (type.IsObject)
                    {
                        if (nativeType == NativeType.Invalid)
                            return MarshallerKind.Variant;
                        else
                            return MarshallerKind.Invalid;
                    }
                    else if (methodData.IsStringBuilder(type))
                    {
                        switch (nativeType)
                        {
                            case NativeType.Invalid:
                                if (isAnsi)
                                {
                                    return MarshallerKind.AnsiStringBuilder;
                                }
                                else
                                {
                                    return MarshallerKind.UnicodeStringBuilder;
                                }

                            case NativeType.LPStr:
                                return MarshallerKind.AnsiStringBuilder;

                            case NativeType.LPWStr:
                                return MarshallerKind.UnicodeStringBuilder;
                            default:
                                return MarshallerKind.Invalid;
                        }
                    }
                    else if (methodData.IsSafeHandle(type))
                    {
                        if (nativeType == NativeType.Invalid)
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
                    MarshallerKind elementMarshallerKind;

                    if (nativeType == NativeType.Invalid)
                        nativeType = NativeType.Array;

                    switch (nativeType)
                    {
                        case NativeType.Array:
                            {
                                if (isField || isReturn)
                                    return MarshallerKind.Invalid;

                                var arrayType = (ArrayType)type;

                                elementMarshallerKind = GetArrayElementMarshallerKind(
                                    arrayType,
                                    arrayType.ElementType,
                                    marshalAs,
                                    methodData,
                                    isField);

                                // If element is invalid type, the array itself is invalid
                                if (elementMarshallerKind == MarshallerKind.Invalid)
                                    return MarshallerKind.Invalid;

                                if (elementMarshallerKind == MarshallerKind.AnsiChar)
                                    return MarshallerKind.AnsiCharArray;
                                else if (elementMarshallerKind == MarshallerKind.UnicodeChar)
                                    // Arrays of unicode char should be marshalled as blittable arrays
                                    return MarshallerKind.BlittableArray;
                                else
                                    return MarshallerKind.Array;
                            }

                        case NativeType.ByValArray:         // fix sized array
                            {
                                var arrayType = (ArrayType)type;
                                elementMarshallerKind = GetArrayElementMarshallerKind(
                                    arrayType,
                                    arrayType.ElementType,
                                    marshalAs,
                                    methodData,
                                    isField);

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
                    if (nativeType == NativeType.Invalid)
                        return MarshallerKind.BlittableValue;
                    else
                        return MarshallerKind.Invalid;
                }
            }

            return MarshallerKind.Invalid;
        }

        private static MarshallerKind GetArrayElementMarshallerKind(
                   ArrayType arrayType,
                   TypeDesc elementType,
                   MarshalAsDescriptor marshalAs,
                   PInvokeMethodData methodData,
                   bool isField)
        {
            bool isAnsi = (methodData.GetCharSet() & PInvokeAttributes.CharSetAnsi) == PInvokeAttributes.CharSetAnsi;
            NativeType nativeType = NativeType.Invalid;

            if (marshalAs != null)
                nativeType = (NativeType)marshalAs.ArraySubType;

            if (elementType.IsPrimitive)
            {
                switch (elementType.Category)
                {
                    case TypeFlags.Char:
                        switch (nativeType)
                        {
                            case NativeType.I1:
                            case NativeType.U1:
                                return MarshallerKind.AnsiChar;
                            case NativeType.I2:
                            case NativeType.U2:
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
                            case NativeType.Boolean:
                                return MarshallerKind.Bool;
                            case NativeType.I1:
                            case NativeType.U1:
                                return MarshallerKind.CBool;
                            case NativeType.Invalid:
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
                else if (PInvokeMethodData.IsSafeHandle(byRefType.ParameterType))
                {
                    // HACK
                    return MarshallerKind.SafeHandle;
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
                        case NativeType.Invalid:
                        case NativeType.Struct:
                            return MarshallerKind.Decimal;

                        case NativeType.LPStruct:
                            return MarshallerKind.BlittableStructPtr;

                        default:
                            return MarshallerKind.Invalid;
                    }
                }
                else if (methodData.IsSystemGuid(elementType))
                {
                    switch (nativeType)
                    {
                        case NativeType.Invalid:
                        case NativeType.Struct:
                            return MarshallerKind.BlittableValue;

                        case NativeType.LPStruct:
                            return MarshallerKind.BlittableStructPtr;

                        default:
                            return MarshallerKind.Invalid;
                    }
                }
                else if (methodData.IsSystemDateTime(elementType))
                {
                    if (nativeType == NativeType.Invalid ||
                        nativeType == NativeType.Struct)
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
                            case NativeType.Invalid:
                            case NativeType.Struct:
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
                        case NativeType.Invalid:
                            if (isAnsi)
                                return MarshallerKind.AnsiString;
                            else
                                return MarshallerKind.UnicodeString;
                        case NativeType.LPStr:
                            return MarshallerKind.AnsiString;
                        case NativeType.LPWStr:
                            return MarshallerKind.UnicodeString;
                        default:
                            return MarshallerKind.Invalid;
                    }
                }

                if (elementType.IsObject)
                {
                    if (nativeType == NativeType.Invalid)
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
            switch (PInvokeMethodData.Direction)
            {
                case MarshalDirection.Forward: EmitForwardArgumentMarshallingIL(); return;
                case MarshalDirection.Reverse: EmitReverseArgumentMarshallingIL(); return;
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
                _ilCodeStreams.MarshallingCodeStream.EmitLdArg(PInvokeParameterMetadata.Index - 1);

                EmitMarshalArgumentManagedToNative();

                Debug.Assert(NativeParameterType != null);

                IL.Stubs.ILLocalVariable vMarshalledTypeTemp = _ilCodeStreams.Emitter.NewLocal(NativeParameterType);
                _ilCodeStreams.MarshallingCodeStream.EmitStLoc(vMarshalledTypeTemp);
                _ilCodeStreams.CallsiteSetupCodeStream.EmitLdLoc(vMarshalledTypeTemp);
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
        protected virtual void EmitMarshalReturnValueManagedToNative()
        {
        }
        protected virtual void EmitMarshalArgumentManagedToNative()
        {
            //
            // marshal
            //
            if (In)
            {
                EmitSpaceAndContentsManagedToNative();
            }
            else
            {
                EmitSpaceManagedToNative();
            }

            //
            // unmarshal
            //
            if (Out)
            {
                if (In)
                {   
                    EmitClearManagedContents();
                }
                EmitContentsNativeToManaged();
            }
            EmitCleanupManagedToNative();
        }
        protected virtual void EmitSpaceAndContentsManagedToNative()
        {
            EmitSpaceManagedToNative();
            EmitContentsManagedToNative();
        }

        protected virtual void EmitSpaceManagedToNative()
        {
        }
        protected virtual void EmitContentsManagedToNative()
        {
        }

        protected virtual void EmitClearManagedContents()
        {
        }

        protected virtual void EmitContentsNativeToManaged()
        {
        }

        protected virtual void EmitCleanupManagedToNative()
        {
        }

        protected virtual void EmitMarshalReturnValueNativeToManaged()
        {
        }

        protected virtual void EmitMarshalArgumentNativeToManaged()
        {
        }
    }

    class VoidReturnMarshaller : Marshaller
    {
        protected override void EmitMarshalReturnValueManagedToNative()
        {
            NativeParameterType = ManagedParameterType;
        }
    }

    class BlittableValueMarshaller : BlittableByRefMarshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            if (Out)
            {
                EmitByRefManagedToNative();
            }
            else
            {
                NativeParameterType = ManagedParameterType.UnderlyingType;
            }
        }

        protected override void EmitMarshalReturnValueManagedToNative()
        {
            NativeParameterType = ManagedParameterType.UnderlyingType;
        }
    }

    class BlittableArrayMarshaller : Marshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var arrayType = (ArrayType)ManagedParameterType;
            Debug.Assert(arrayType.IsSzArray);

            IL.Stubs.ILLocalVariable vPinnedFirstElement = emitter.NewLocal(arrayType.ParameterType.MakeByRefType(), true);
            IL.Stubs.ILLocalVariable vArray = emitter.NewLocal(arrayType);
            ILCodeLabel lNullArray = emitter.NewCodeLabel();

            // Check for null array, or 0 element array.
            marshallingCodeStream.Emit(ILOpcode.dup);
            marshallingCodeStream.EmitStLoc(vArray);
            marshallingCodeStream.Emit(ILOpcode.brfalse, lNullArray);
            marshallingCodeStream.EmitLdLoc(vArray);
            marshallingCodeStream.Emit(ILOpcode.ldlen);
            marshallingCodeStream.Emit(ILOpcode.conv_i4);
            marshallingCodeStream.Emit(ILOpcode.brfalse, lNullArray);

            // Array has elements.
            marshallingCodeStream.EmitLdLoc(vArray);
            marshallingCodeStream.EmitLdc(0);
            marshallingCodeStream.Emit(ILOpcode.ldelema, emitter.NewToken(arrayType.ElementType));
            marshallingCodeStream.EmitStLoc(vPinnedFirstElement);

            // Fall through. If array didn't have elements, vPinnedFirstElement is zeroinit.
            marshallingCodeStream.EmitLabel(lNullArray);
            marshallingCodeStream.EmitLdLoc(vPinnedFirstElement);
            marshallingCodeStream.Emit(ILOpcode.conv_i);

            NativeParameterType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);
        }
    }

    abstract class BlittableByRefMarshaller : Marshaller
    {
        protected virtual void EmitByRefManagedToNative()
        {
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            ILEmitter emitter = _ilCodeStreams.Emitter;
            var byRefType = (ByRefType)ManagedParameterType;
            IL.Stubs.ILLocalVariable vPinnedByRef = emitter.NewLocal(byRefType, true);
            marshallingCodeStream.EmitStLoc(vPinnedByRef);
            marshallingCodeStream.EmitLdLoc(vPinnedByRef);
            marshallingCodeStream.Emit(ILOpcode.conv_i);

            NativeParameterType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);
        }
    }

    class BooleanMarshaller : Marshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            marshallingCodeStream.EmitLdc(0);
            marshallingCodeStream.Emit(ILOpcode.ceq);
            marshallingCodeStream.EmitLdc(0);
            marshallingCodeStream.Emit(ILOpcode.ceq);

            NativeParameterType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.Int32);
        }

        protected override void EmitMarshalReturnValueManagedToNative()
        {
            ILCodeStream returnValueMarshallingCodeStream = _ilCodeStreams.ReturnValueMarshallingCodeStream;
            returnValueMarshallingCodeStream.EmitLdc(0);
            returnValueMarshallingCodeStream.Emit(ILOpcode.ceq);
            returnValueMarshallingCodeStream.EmitLdc(0);
            returnValueMarshallingCodeStream.Emit(ILOpcode.ceq);

            NativeParameterType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.Int32);
        }
    }

    class UnicodeStringMarshaller : Marshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            TypeSystemContext context = PInvokeMethodData.Context;
            //
            // Unicode marshalling. Pin the string and push a pointer to the first character on the stack.
            //

            TypeDesc stringType = context.GetWellKnownType(WellKnownType.String);

            IL.Stubs.ILLocalVariable vPinnedString = emitter.NewLocal(stringType, true);
            ILCodeLabel lNullString = emitter.NewCodeLabel();

            marshallingCodeStream.EmitStLoc(vPinnedString);
            marshallingCodeStream.EmitLdLoc(vPinnedString);

            marshallingCodeStream.Emit(ILOpcode.conv_i);
            marshallingCodeStream.Emit(ILOpcode.dup);

            // Marshalling a null string?
            marshallingCodeStream.Emit(ILOpcode.brfalse, lNullString);

            marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                context.SystemModule.
                    GetKnownType("System.Runtime.CompilerServices", "RuntimeHelpers").
                        GetKnownMethod("get_OffsetToStringData", null)));

            marshallingCodeStream.Emit(ILOpcode.add);

            marshallingCodeStream.EmitLabel(lNullString);

            NativeParameterType = context.GetWellKnownType(WellKnownType.IntPtr);
        }
    }

    class AnsiStringMarshaller : BlittableArrayMarshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            TypeSystemContext context = PInvokeMethodData.Context;
            //
            // ANSI marshalling. Allocate a byte array, copy characters, pin first element.
            //

            var stringToAnsi = context.GetHelperEntryPoint("InteropHelpers", "StringToAnsi");

            marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(stringToAnsi));

            // Call the Array marshaller MarshalArgument
            ManagedParameterType = context.GetWellKnownType(WellKnownType.Byte).MakeArrayType();
            base.EmitMarshalArgumentManagedToNative();
        }
    }

    class SafeHandleMarshaller : Marshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            ILCodeStream unmarshallingCodeStream = _ilCodeStreams.UnmarshallingCodestream;
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

                Debug.Assert(ManagedParameterType is ByRefType);
                var vOutArg = emitter.NewLocal(ManagedParameterType);
                marshallingCodeStream.EmitStLoc(vOutArg);

                TypeDesc resolvedType = ((ByRefType)ManagedParameterType).ParameterType;

                var nativeType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr).MakeByRefType();
                var vOutValue = emitter.NewLocal(PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr));
                var vSafeHandle = emitter.NewLocal(resolvedType);
                marshallingCodeStream.Emit(ILOpcode.newobj, emitter.NewToken(resolvedType.GetDefaultConstructor()));
                marshallingCodeStream.EmitStLoc(vSafeHandle);
                marshallingCodeStream.EmitLdLoca(vOutValue);

                unmarshallingCodeStream.EmitLdLoc(vSafeHandle);
                unmarshallingCodeStream.EmitLdLoc(vOutValue);
                unmarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    PInvokeMethodData.SafeHandleType.GetKnownMethod("SetHandle", null)));

                unmarshallingCodeStream.EmitLdLoc(vOutArg);
                unmarshallingCodeStream.EmitLdLoc(vSafeHandle);
                unmarshallingCodeStream.Emit(ILOpcode.stind_ref);

                NativeParameterType = nativeType;
            }
            else
            {
                var vAddRefed = emitter.NewLocal(PInvokeMethodData.Context.GetWellKnownType(WellKnownType.Boolean));
                var vSafeHandle = emitter.NewLocal(ManagedParameterType);

                marshallingCodeStream.EmitStLoc(vSafeHandle);
                marshallingCodeStream.EmitLdLoc(vSafeHandle);
                marshallingCodeStream.EmitLdLoca(vAddRefed);
                marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousAddRef", null)));

                marshallingCodeStream.EmitLdLoc(vSafeHandle);
                marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousGetHandle", null)));

                // TODO: This should be inside finally block and only executed it the handle was addrefed
                unmarshallingCodeStream.EmitLdLoc(vSafeHandle);
                unmarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                    safeHandleType.GetKnownMethod("DangerousRelease", null)));

                NativeParameterType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);
            }
        }

        protected override void EmitMarshalReturnValueManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream returnValueMarshallingCodeStream = _ilCodeStreams.ReturnValueMarshallingCodeStream;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            NativeParameterType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);

            var vSafeHandle = emitter.NewLocal(ManagedParameterType);
            var vReturnValue = emitter.NewLocal(NativeParameterType);

            marshallingCodeStream.Emit(ILOpcode.newobj, emitter.NewToken(ManagedParameterType.GetDefaultConstructor()));
            marshallingCodeStream.EmitStLoc(vSafeHandle);

            returnValueMarshallingCodeStream.EmitStLoc(vReturnValue);

            returnValueMarshallingCodeStream.EmitLdLoc(vSafeHandle);
            returnValueMarshallingCodeStream.EmitLdLoc(vReturnValue);
            returnValueMarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
            PInvokeMethodData.SafeHandleType.GetKnownMethod("SetHandle", null)));

            returnValueMarshallingCodeStream.EmitLdLoc(vSafeHandle);
        }
    }

    class UnicodeStringBuilderMarshaller : BlittableArrayMarshaller
    {
        protected override void EmitMarshalArgumentManagedToNative()
        {
            ILEmitter emitter = _ilCodeStreams.Emitter;
            ILCodeStream marshallingCodeStream = _ilCodeStreams.MarshallingCodeStream;
            ILCodeStream unmarshallingCodeStream = _ilCodeStreams.UnmarshallingCodestream;
            TypeSystemContext context = PInvokeMethodData.Context;
            // TODO: Handles [out] marshalling only for now

            var stringBuilderType = context.SystemModule.GetKnownType("System.Text", "StringBuilder");
            var charArrayType = context.GetWellKnownType(WellKnownType.Char).MakeArrayType();

            IL.Stubs.ILLocalVariable vStringBuilder = emitter.NewLocal(stringBuilderType);
            IL.Stubs.ILLocalVariable vBuffer = emitter.NewLocal(charArrayType);

            marshallingCodeStream.EmitStLoc(vStringBuilder);

            marshallingCodeStream.EmitLdLoc(vStringBuilder);
            marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                context.GetHelperEntryPoint("InteropHelpers", "GetEmptyStringBuilderBuffer")));
            marshallingCodeStream.EmitStLoc(vBuffer);

            unmarshallingCodeStream.EmitLdLoc(vStringBuilder);
            unmarshallingCodeStream.EmitLdLoc(vBuffer);
            unmarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                context.GetHelperEntryPoint("InteropHelpers", "ReplaceStringBuilderBuffer")));

            marshallingCodeStream.EmitLdLoc(vBuffer);
            ManagedParameterType = charArrayType;
            base.EmitMarshalArgumentManagedToNative();
        }
    }
}