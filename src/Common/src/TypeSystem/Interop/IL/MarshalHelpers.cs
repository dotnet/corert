// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.IL;
using Debug = System.Diagnostics.Debug;
using Internal.IL.Stubs;

namespace Internal.TypeSystem.Interop
{
    public static class MarshalHelpers
    {
        /// <summary>
        /// Returns true if this is a type that doesn't require marshalling.
        /// </summary>
        public static bool IsBlittableType(TypeDesc type)
        {
            type = type.UnderlyingType;

            if (type.IsValueType)
            {
                if (type.IsPrimitive)
                {
                    // All primitive types except char and bool are blittable
                    TypeFlags category = type.Category;
                    if (category == TypeFlags.Boolean || category == TypeFlags.Char)
                        return false;

                    return true;
                }

                foreach (FieldDesc field in type.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    TypeDesc fieldType = field.FieldType;

                    // TODO: we should also reject fields that specify custom marshalling
                    if (!MarshalHelpers.IsBlittableType(fieldType))
                    {
                        // This field can still be blittable if it's a Char and marshals as Unicode
                        var owningType = field.OwningType as MetadataType;
                        if (owningType == null)
                            return false;

                        if (fieldType.Category != TypeFlags.Char ||
                            owningType.PInvokeStringFormat == PInvokeStringFormat.AnsiClass)
                            return false;
                    }
                }
                return true;
            }

            if (type.IsPointer || type.IsFunctionPointer)
                return true;

            return false;
        }

        public static bool IsStructMarshallingRequired(TypeDesc typeDesc)
        {
            if (typeDesc is ByRefType)
            {
                typeDesc = typeDesc.GetParameterType();
            }

            if (typeDesc.Category != TypeFlags.ValueType)
                return false;

            MetadataType type = typeDesc as MetadataType;
            if (type == null)
            {
                return false;
            }

            //
            // For struct marshalling it is required to have either Sequential
            // or Explicit layout. For Auto layout the P/Invoke marshalling code
            // will throw appropriate error message.
            //
            if (!type.IsSequentialLayout && !type.IsExplicitLayout)
                return false;

            // If it is not blittable we will need struct marshalling
            return !IsBlittableType(type);
        }

        /// <summary>
        /// Returns true if the PInvoke target should be resolved lazily.
        /// </summary>
        public static bool UseLazyResolution(MethodDesc method, string importModule, PInvokeILEmitterConfiguration configuration)
        {
            bool? forceLazyResolution = configuration.ForceLazyResolution;
            if (forceLazyResolution.HasValue)
                return forceLazyResolution.Value;

            // In multi-module library mode, the WinRT p/invokes in System.Private.Interop cause linker failures
            // since we don't link against the OS libraries containing those APIs. Force them to be lazy.
            // See https://github.com/dotnet/corert/issues/2601
            string assemblySimpleName = ((IAssemblyDesc)((MetadataType)method.OwningType).Module).GetName().Name;
            if (assemblySimpleName == "System.Private.Interop")
            {
                return true;
            }

            // Determine whether this call should be made through a lazy resolution or a static reference
            // Eventually, this should be controlled by a custom attribute (or an extension to the metadata format).
            if (importModule == "[MRT]" || importModule == "*")
                return false;

            if (method.Context.Target.IsWindows)
            {
                return !importModule.StartsWith("api-ms-win-");
            }
            else 
            {
                // Account for System.Private.CoreLib.Native / System.Globalization.Native / System.Native / etc
                return !importModule.StartsWith("System.");
            }
        }

        internal static TypeDesc GetNativeMethodParameterType(TypeDesc type, MarshalAsDescriptor marshalAs, InteropStateManager interopStateManager, bool isReturn, bool isAnsi)
        {
            MarshallerKind elementMarshallerKind;
            MarshallerKind marshallerKind = MarshalHelpers.GetMarshallerKind(type,
                                                marshalAs,
                                                isReturn,
                                                isAnsi,
                                                MarshallerType.Argument,
                                                out elementMarshallerKind);

            return GetNativeTypeFromMarshallerKind(type,
                marshallerKind,
                elementMarshallerKind,
                interopStateManager,
                marshalAs);
        }

        internal static TypeDesc GetNativeStructFieldType(TypeDesc type, MarshalAsDescriptor marshalAs, InteropStateManager interopStateManager, bool isAnsi)
        {
            MarshallerKind elementMarshallerKind;
            MarshallerKind marshallerKind = MarshalHelpers.GetMarshallerKind(type,
                                                marshalAs,
                                                false,  /*  isReturn */
                                                isAnsi, /*    isAnsi */
                                                MarshallerType.Field,
                                                out elementMarshallerKind);

            return GetNativeTypeFromMarshallerKind(type,
                marshallerKind,
                elementMarshallerKind,
                interopStateManager,
                marshalAs);
        }

        internal static TypeDesc GetNativeTypeFromMarshallerKind(TypeDesc type, 
                MarshallerKind kind, 
                MarshallerKind elementMarshallerKind,
                InteropStateManager interopStateManager,
                MarshalAsDescriptor marshalAs,
                bool isArrayElement = false)
        {
            TypeSystemContext context = type.Context;
            NativeTypeKind nativeType = NativeTypeKind.Invalid;
            if (marshalAs != null)
            {
                nativeType = isArrayElement ? marshalAs.ArraySubType : marshalAs.Type;
            }

            switch (kind)
            {
                case MarshallerKind.BlittableValue:
                    {
                        switch (nativeType)
                        {
                            case NativeTypeKind.I1:
                                return context.GetWellKnownType(WellKnownType.SByte);
                            case NativeTypeKind.U1:
                                return context.GetWellKnownType(WellKnownType.Byte);
                            case NativeTypeKind.I2:
                                return context.GetWellKnownType(WellKnownType.Int16);
                            case NativeTypeKind.U2:
                                return context.GetWellKnownType(WellKnownType.UInt16);
                            case NativeTypeKind.I4:
                                return context.GetWellKnownType(WellKnownType.Int32);
                            case NativeTypeKind.U4:
                                return context.GetWellKnownType(WellKnownType.UInt32);
                            case NativeTypeKind.I8:
                                return context.GetWellKnownType(WellKnownType.Int64);
                            case NativeTypeKind.U8:
                                return context.GetWellKnownType(WellKnownType.UInt64);
                            case NativeTypeKind.R4:
                                return context.GetWellKnownType(WellKnownType.Single);
                            case NativeTypeKind.R8:
                                return context.GetWellKnownType(WellKnownType.Double);
                            default:
                                return type.UnderlyingType;
                        }
                    }

                case MarshallerKind.Bool:
                    return context.GetWellKnownType(WellKnownType.Int32);

                case MarshallerKind.CBool:
                        return context.GetWellKnownType(WellKnownType.Byte);

                case MarshallerKind.Enum:
                case MarshallerKind.BlittableStruct:
                case MarshallerKind.Decimal:
                case MarshallerKind.VoidReturn:
                    return type;

                case MarshallerKind.Struct:
                    return interopStateManager.GetStructMarshallingNativeType((MetadataType)type);

                case MarshallerKind.BlittableStructPtr:
                    return type.MakePointerType();

                case MarshallerKind.HandleRef:
                    return context.GetWellKnownType(WellKnownType.IntPtr);

                case MarshallerKind.UnicodeChar:
                    if (nativeType == NativeTypeKind.U2)
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
                            elementMarshallerKind,
                            MarshallerKind.Unknown,
                            interopStateManager,
                            marshalAs, 
                            isArrayElement: true);

                        return elementNativeType.MakePointerType();
                    }

                case MarshallerKind.AnsiChar:
                    return context.GetWellKnownType(WellKnownType.Byte);

                case MarshallerKind.FunctionPointer:
                    return context.GetWellKnownType(WellKnownType.IntPtr);

                case MarshallerKind.ByValUnicodeString:
                case MarshallerKind.ByValAnsiString:
                    {
                        var inlineArrayCandidate = GetInlineArrayCandidate(context.GetWellKnownType(WellKnownType.Char), elementMarshallerKind, interopStateManager, marshalAs);
                        return interopStateManager.GetInlineArrayType(inlineArrayCandidate);
                    }

                case MarshallerKind.ByValAnsiCharArray:
                case MarshallerKind.ByValArray:
                    {
                        ArrayType arrayType = type as ArrayType;
                        Debug.Assert(arrayType != null, "Expecting array");

                        var inlineArrayCandidate = GetInlineArrayCandidate(arrayType.ElementType, elementMarshallerKind, interopStateManager, marshalAs);

                        return interopStateManager.GetInlineArrayType(inlineArrayCandidate);
                    }

                case MarshallerKind.Unknown:
                default:
                    throw new NotSupportedException();
            }
        }

        internal static InlineArrayCandidate GetInlineArrayCandidate(TypeDesc managedElementType, MarshallerKind elementMarshallerKind, InteropStateManager interopStateManager, MarshalAsDescriptor marshalAs)
        {
            TypeDesc nativeType = GetNativeTypeFromMarshallerKind(
                                                managedElementType,
                                                elementMarshallerKind,
                                                MarshallerKind.Unknown,
                                                interopStateManager,
                                                null);

            var elementNativeType = nativeType as MetadataType;
            if (elementNativeType == null)
            {
                Debug.Assert(nativeType is PointerType);

                // If it is a pointer type we will create InlineArray for IntPtr
                elementNativeType = (MetadataType)managedElementType.Context.GetWellKnownType(WellKnownType.IntPtr);
            }
            Debug.Assert(marshalAs != null && marshalAs.SizeConst.HasValue);

            // if SizeConst is not specified, we will default to 1. 
            // the marshaller will throw appropriate exception
            uint size = 1;
            if (marshalAs.SizeConst.HasValue)
            {
                size = marshalAs.SizeConst.Value;
            }
            return new InlineArrayCandidate(elementNativeType, size);

        }

        internal static MarshallerKind GetMarshallerKind(
             TypeDesc type,
             MarshalAsDescriptor marshalAs,
             bool isReturn,
             bool isAnsi,
             MarshallerType marshallerType,
             out MarshallerKind elementMarshallerKind)
        {
            if (type.IsByRef)
            {
                type = type.GetParameterType();
            }
            TypeSystemContext context = type.Context;
            NativeTypeKind nativeType = NativeTypeKind.Invalid;
            bool isField = marshallerType == MarshallerType.Field;

            if (marshalAs != null)
                nativeType = (NativeTypeKind)marshalAs.Type;


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
                            case NativeTypeKind.Invalid:
                            case NativeTypeKind.Boolean:
                                return MarshallerKind.Bool;

                            case NativeTypeKind.U1:
                            case NativeTypeKind.I1:
                                return MarshallerKind.CBool;

                            default:
                                return MarshallerKind.Invalid;
                        }

                    case TypeFlags.Char:
                        switch (nativeType)
                        {
                            case NativeTypeKind.I1:
                            case NativeTypeKind.U1:
                                return MarshallerKind.AnsiChar;

                            case NativeTypeKind.I2:
                            case NativeTypeKind.U2:
                                return MarshallerKind.UnicodeChar;

                            case NativeTypeKind.Invalid:
                                if (isAnsi)
                                    return MarshallerKind.AnsiChar;
                                else
                                    return MarshallerKind.UnicodeChar;
                            default:
                                return MarshallerKind.Invalid;
                        }

                    case TypeFlags.SByte:
                    case TypeFlags.Byte:
                        if (nativeType == NativeTypeKind.I1 || nativeType == NativeTypeKind.U1 || nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int16:
                    case TypeFlags.UInt16:
                        if (nativeType == NativeTypeKind.I2 || nativeType == NativeTypeKind.U2 || nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int32:
                    case TypeFlags.UInt32:
                        if (nativeType == NativeTypeKind.I4 || nativeType == NativeTypeKind.U4 || nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Int64:
                    case TypeFlags.UInt64:
                        if (nativeType == NativeTypeKind.I8 || nativeType == NativeTypeKind.U8 || nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.IntPtr:
                    case TypeFlags.UIntPtr:
                        if (nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Single:
                        if (nativeType == NativeTypeKind.R4 || nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.BlittableValue;
                        else
                            return MarshallerKind.Invalid;

                    case TypeFlags.Double:
                        if (nativeType == NativeTypeKind.R8 || nativeType == NativeTypeKind.Invalid)
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

                if (InteropTypes.IsSystemDateTime(context, type))
                {
                    if (nativeType == NativeTypeKind.Invalid ||
                        nativeType == NativeTypeKind.Struct)
                        return MarshallerKind.OleDateTime;
                    else
                        return MarshallerKind.Invalid;
                }
                /*              
                                TODO: Bring HandleRef to CoreLib
                                https://github.com/dotnet/corert/issues/2570

                                else if (context.IsHandleRef(type))
                                {
                                    if (nativeType == NativeType.Invalid)
                                        return MarshallerKind.HandleRef;
                                    else
                                        return MarshallerKind.Invalid;
                                }
                */

                switch (nativeType)
                {
                    case NativeTypeKind.Invalid:
                    case NativeTypeKind.Struct:
                        if (InteropTypes.IsSystemDecimal(context, type))
                            return MarshallerKind.Decimal;
                        break;

                    case NativeTypeKind.LPStruct:
                        if (InteropTypes.IsSystemGuid(context, type) ||
                            InteropTypes.IsSystemDecimal(context, type))
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

                if (type is MetadataType)
                {
                    MetadataType metadataType = (MetadataType)type;
                    // the struct type need to be either sequential or explicit. If it is
                    // auto layout we will throw exception.
                    if (!metadataType.IsSequentialLayout && !metadataType.IsExplicitLayout)
                    {
                        throw new InvalidProgramException("The specified structure " + metadataType.Name + " has invalid StructLayout information. It must be either Sequential or Explicit.");
                    }
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
                            case NativeTypeKind.LPWStr:
                                return MarshallerKind.UnicodeString;

                            case NativeTypeKind.LPStr:
                                return MarshallerKind.AnsiString;

                            case NativeTypeKind.LPTStr:
                                return MarshallerKind.UnicodeString;

                            case NativeTypeKind.ByValTStr:
                                if (isAnsi)
                                {
                                    elementMarshallerKind = MarshallerKind.AnsiChar;
                                    return MarshallerKind.ByValAnsiString;
                                }
                                else
                                {
                                    elementMarshallerKind = MarshallerKind.UnicodeChar;
                                    return MarshallerKind.ByValUnicodeString;
                                }

                            case NativeTypeKind.Invalid:
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
                        if (nativeType == NativeTypeKind.Invalid || nativeType == NativeTypeKind.Func)
                            return MarshallerKind.FunctionPointer;
                        else
                            return MarshallerKind.Invalid;
                    }
                    else if (type.IsObject)
                    {
                        if (nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.Variant;
                        else
                            return MarshallerKind.Invalid;
                    }
                    else if (InteropTypes.IsStringBuilder(context, type))
                    {
                        switch (nativeType)
                        {
                            case NativeTypeKind.Invalid:
                                if (isAnsi)
                                {
                                    return MarshallerKind.AnsiStringBuilder;
                                }
                                else
                                {
                                    return MarshallerKind.UnicodeStringBuilder;
                                }

                            case NativeTypeKind.LPStr:
                                return MarshallerKind.AnsiStringBuilder;

                            case NativeTypeKind.LPWStr:
                                return MarshallerKind.UnicodeStringBuilder;
                            default:
                                return MarshallerKind.Invalid;
                        }
                    }
                    else if (InteropTypes.IsSafeHandle(context, type))
                    {
                        if (nativeType == NativeTypeKind.Invalid)
                            return MarshallerKind.SafeHandle;
                        else
                            return MarshallerKind.Invalid;
                    }
                    /*
                                        TODO: Bring CriticalHandle to CoreLib
                                        https://github.com/dotnet/corert/issues/2570

                                        else if (InteropTypes.IsCriticalHandle(context, type))
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
                else if (InteropTypes.IsSystemArray(context, type))
                {
                    return MarshallerKind.Invalid;
                }
                else if (type.IsSzArray)
                {
                    if (nativeType == NativeTypeKind.Invalid)
                        nativeType = NativeTypeKind.Array;

                    switch (nativeType)
                    {
                        case NativeTypeKind.Array:
                            {
                                if (isField || isReturn)
                                    return MarshallerKind.Invalid;

                                var arrayType = (ArrayType)type;

                                elementMarshallerKind = GetArrayElementMarshallerKind(
                                    arrayType,
                                    marshalAs,
                                    isAnsi);

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

                        case NativeTypeKind.ByValArray:         // fix sized array
                            {
                                var arrayType = (ArrayType)type;
                                elementMarshallerKind = GetArrayElementMarshallerKind(
                                    arrayType,
                                    marshalAs,
                                    isAnsi);

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
                    if (nativeType == NativeTypeKind.Invalid)
                        return MarshallerKind.BlittableValue;
                    else
                        return MarshallerKind.Invalid;
                }
            }

            return MarshallerKind.Invalid;
        }

        private static MarshallerKind GetArrayElementMarshallerKind(
                   ArrayType arrayType,
                   MarshalAsDescriptor marshalAs,
                   bool isAnsi)
        {
            TypeDesc elementType = arrayType.ElementType;
            NativeTypeKind nativeType = NativeTypeKind.Invalid;
            TypeSystemContext context = arrayType.Context;

            if (marshalAs != null)
                nativeType = (NativeTypeKind)marshalAs.ArraySubType;

            if (elementType.IsPrimitive)
            {
                switch (elementType.Category)
                {
                    case TypeFlags.Char:
                        switch (nativeType)
                        {
                            case NativeTypeKind.I1:
                            case NativeTypeKind.U1:
                                return MarshallerKind.AnsiChar;
                            case NativeTypeKind.I2:
                            case NativeTypeKind.U2:
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
                            case NativeTypeKind.Boolean:
                                return MarshallerKind.Bool;
                            case NativeTypeKind.I1:
                            case NativeTypeKind.U1:
                                return MarshallerKind.CBool;
                            case NativeTypeKind.Invalid:
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

                if (InteropTypes.IsSystemDecimal(context, elementType))
                {
                    switch (nativeType)
                    {
                        case NativeTypeKind.Invalid:
                        case NativeTypeKind.Struct:
                            return MarshallerKind.Decimal;

                        case NativeTypeKind.LPStruct:
                            return MarshallerKind.BlittableStructPtr;

                        default:
                            return MarshallerKind.Invalid;
                    }
                }
                else if (InteropTypes.IsSystemGuid(context, elementType))
                {
                    switch (nativeType)
                    {
                        case NativeTypeKind.Invalid:
                        case NativeTypeKind.Struct:
                            return MarshallerKind.BlittableValue;

                        case NativeTypeKind.LPStruct:
                            return MarshallerKind.BlittableStructPtr;

                        default:
                            return MarshallerKind.Invalid;
                    }
                }
                else if (InteropTypes.IsSystemDateTime(context, elementType))
                {
                    if (nativeType == NativeTypeKind.Invalid ||
                        nativeType == NativeTypeKind.Struct)
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

                                else if (InteropTypes.IsHandleRef(context, elementType))
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
                            case NativeTypeKind.Invalid:
                            case NativeTypeKind.Struct:
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
                        case NativeTypeKind.Invalid:
                            if (isAnsi)
                                return MarshallerKind.AnsiString;
                            else
                                return MarshallerKind.UnicodeString;
                        case NativeTypeKind.LPStr:
                            return MarshallerKind.AnsiString;
                        case NativeTypeKind.LPWStr:
                            return MarshallerKind.UnicodeString;
                        default:
                            return MarshallerKind.Invalid;
                    }
                }

                if (elementType.IsObject)
                {
                    if (nativeType == NativeTypeKind.Invalid)
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

                if (InteropTypes.IsSafeHandle(context, elementType))
                {
                    return MarshallerKind.Invalid;
                }
                /*          
                                TODO: Bring CriticalHandle to CoreLib
                                https://github.com/dotnet/corert/issues/2570

                                if (pInvokeData.IsCriticalHandle(elementType))
                                {
                                    return MarshallerKind.Invalid;
                                }
                */
            }

            return MarshallerKind.Invalid;
        }

        //TODO: https://github.com/dotnet/corert/issues/2675
        // This exception messages need to localized
        // TODO: Log as warning
        public static MethodIL EmitExceptionBody(string message, MethodDesc method)
        {
            ILEmitter emitter = new ILEmitter();

            TypeSystemContext context = method.Context;
            MethodSignature ctorSignature = new MethodSignature(0, 0, context.GetWellKnownType(WellKnownType.Void),
                new TypeDesc[] { context.GetWellKnownType(WellKnownType.String) });
            MethodDesc exceptionCtor = method.Context.GetWellKnownType(WellKnownType.Exception).GetKnownMethod(".ctor", ctorSignature);

            ILCodeStream codeStream = emitter.NewCodeStream();
            codeStream.Emit(ILOpcode.ldstr, emitter.NewToken(message));
            codeStream.Emit(ILOpcode.newobj, emitter.NewToken(exceptionCtor));
            codeStream.Emit(ILOpcode.throw_);
            codeStream.Emit(ILOpcode.ret);

            return new PInvokeILStubMethodIL((ILStubMethodIL)emitter.Link(method), true);
        }

    }
}