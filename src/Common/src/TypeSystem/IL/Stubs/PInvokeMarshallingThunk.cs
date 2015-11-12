// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for PInvoke methods that require marshalling.
    /// 
    /// This by no means intends to provide full PInvoke support. The intended use of this is to
    /// a) prevent calls getting generated to targets that require a full marshaller
    /// (this compiler doesn't provide that), and b) offer a hand in some very simple marshalling
    /// situations (but support for this part might go away as the product matures).
    /// </summary>
    public sealed class PInvokeMarshallingThunkEmitter : ILEmitter
    {
        MethodDesc _targetMethod;
        MethodDesc _nativeTargetMethod;
        PInvokeMetadata _importMetadata;

        public PInvokeMarshallingThunkEmitter(MethodDesc targetMethod)
        {
            Debug.Assert(targetMethod.IsPInvokeImpl);
            Debug.Assert(RequiresMarshalling(targetMethod));

            _targetMethod = targetMethod;
            _importMetadata = targetMethod.GetPInvokeMethodImportMetadata();
        }

        /// <summary>
        /// Gets the synthetic native PInvoke target method. This method represents the real PInvoke target
        /// that doesn't require any marshalling because all arguments are simple types.
        /// </summary>
        private MethodDesc NativeTargetMethod
        {
            get
            {
                if (_nativeTargetMethod == null)
                {
                    // Map all types in the target method signature to native types that don't require marshalling.
                    TypeDesc[] nativeParameters = new TypeDesc[_targetMethod.Signature.Length];
                    for (int i = 0; i < nativeParameters.Length; i++)
                    {
                        nativeParameters[i] = ManagedTypeToNativeType(_targetMethod.Signature[i]);
                        Debug.Assert(nativeParameters[i] != null);
                    }

                    TypeDesc nativeReturnType = _targetMethod.Signature.ReturnType.IsVoid ?
                        _targetMethod.Signature.ReturnType :
                        ManagedTypeToNativeType(_targetMethod.Signature.ReturnType);
                    Debug.Assert(nativeReturnType != null);

                    MethodSignature nativeSignature = new MethodSignature(_targetMethod.Signature.Flags, 0, nativeReturnType, nativeParameters);

                    // Create the synthetic pinvokeimpl method.
                    _nativeTargetMethod = new PInvokeTargetNativeMethod(_targetMethod.OwningType, nativeSignature, _importMetadata);

                    // The native target method better not require another marshalling...
                    Debug.Assert(!RequiresMarshalling(_nativeTargetMethod));
                }

                return _nativeTargetMethod;
            }
        }

        /// <summary>
        /// Returns true if <paramref name="method"/> requires a marshalling stub to be generated.
        /// </summary>
        public static bool RequiresMarshalling(MethodDesc method)
        {
            Debug.Assert(method.IsPInvokeImpl);

            // TODO: true if there are any custom marshalling rules on the parameters
            // TODO: true if SetLastError is true

            if (!IsSimpleType(method.Signature.ReturnType) && !method.Signature.ReturnType.IsVoid)
                return true;

            for (int i = 0; i < method.Signature.Length; i++)
            {
                if (!IsSimpleType(method.Signature[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if <paramref name="type"/> doesn't require marshalling and can be directly passed
        /// to native code.
        /// </summary>
        private static bool IsSimpleType(TypeDesc type)
        {
            type = type.UnderlyingType;

            switch (type.Category)
            {
                case TypeFlags.Byte:
                case TypeFlags.SByte:
                case TypeFlags.UInt16:
                case TypeFlags.Int16:
                case TypeFlags.UInt32:
                case TypeFlags.Int32:
                case TypeFlags.UInt64:
                case TypeFlags.Int64:
                case TypeFlags.Double:
                case TypeFlags.Single:
                case TypeFlags.UIntPtr:
                case TypeFlags.IntPtr:
                    return true;
            }

            if (type.IsPointer)
                return true;

            return false;
        }

        /// <summary>
        /// Returns true if struct doesn't have fields that require marshalling.
        /// </summary>
        private static bool IsBlittableStruct(TypeDesc type)
        {
            if (type.IsValueType)
            {
                foreach (FieldDesc field in type.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    // TODO: we should also reject fields that specify custom marshalling
                    if (!IsSimpleType(field.FieldType) && !IsBlittableStruct(field.FieldType))
                        return false;
                }
                return true;
            }

            return false;
        }

        private static TypeDesc ManagedTypeToNativeType(TypeDesc type)
        {
            if (IsSimpleType(type))
            {
                return type.UnderlyingType;
            }

            // Arrays and byrefs to simple and blittable types can be marshalled as pointers
            if (type.IsSzArray || type.IsByRef)
            {
                ParameterizedType parametrizedType = (ParameterizedType)type;
                if (IsSimpleType(parametrizedType.ParameterType) || IsBlittableStruct(parametrizedType.ParameterType))
                {
                    return parametrizedType.ParameterType.UnderlyingType.MakePointerType();
                }
                else
                {
                    return null;
                }
            }

            if (type.IsString)
            {
                return type.Context.GetWellKnownType(WellKnownType.Byte).MakePointerType();
            }

            return null;
        }

        /// <summary>
        /// Marshals an array. Expects the array referene on the stack. Pushes a pinned
        /// managed reference to the first element on the stack or null if the array was
        /// null or empty.
        /// </summary>
        private void EmitArrayMarshalling(ILCodeStream codeStream, ArrayType arrayType)
        {
            Debug.Assert(arrayType.IsSzArray);
            int vPinnedFirstElement = NewLocal(arrayType.ParameterType.MakeByRefType(), true);
            int vArray = NewLocal(arrayType);
            ILCodeLabel lNullArray = NewCodeLabel();
            
            // Check for null array, or 0 element array.
            codeStream.Emit(ILOpcode.dup);
            codeStream.EmitStLoc(vArray);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);
            codeStream.EmitLdLoc(vArray);
            codeStream.Emit(ILOpcode.ldlen);
            codeStream.Emit(ILOpcode.conv_i4);
            codeStream.Emit(ILOpcode.brfalse, lNullArray);

            // Array has elements.
            codeStream.EmitLdLoc(vArray);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.ldelema, NewToken(arrayType.ElementType));
            codeStream.EmitStLoc(vPinnedFirstElement);

            // Fall through. If array didn't have elements, vPinnedFirstElement is zeroinit.
            codeStream.EmitLabel(lNullArray);
            codeStream.EmitLdLoc(vPinnedFirstElement);
        }

        /// <summary>
        /// Marshals a ByRef. Expects the ByRef on the stack. Pushes a pinned
        /// unmanaged pointer to the stack.
        /// </summary>
        private void EmitByRefMarshalling(ILCodeStream codeStream, TypeDesc byRefType)
        {
            Debug.Assert(byRefType.IsByRef);
            int vPinnedByRef = NewLocal(byRefType, true);
            codeStream.EmitStLoc(vPinnedByRef);
            codeStream.EmitLdLoc(vPinnedByRef);
            codeStream.Emit(ILOpcode.conv_i);
        }

        /// <summary>
        /// Marshals a string. Expects the string referene on the stack. Pushes a pointer
        /// to the stack that is safe to pass out to native code.
        /// </summary>
        private void EmitStringMarshalling(ILCodeStream codeStream)
        {
            CharSet charset = _importMetadata.CharSet;

            TypeSystemContext context = _targetMethod.Context;

            if (charset == CharSet.Unknown)
            {
                // ECMA-335 II.10.1.5 - Default value is Ansi.
                charset = CharSet.Ansi;
            }

            if (charset == CharSet.Auto)
            {
                if (context.Target.OperatingSystem == TargetOS.Windows)
                    charset = CharSet.Unicode;
                else
                    charset = CharSet.Ansi;
            }

            TypeDesc stringType = context.GetWellKnownType(WellKnownType.String);

            if (charset == CharSet.Unicode)
            {
                //
                // Unicode marshalling. Pin the string and push a pointer to the first character on the stack.
                //

                int vPinnedString = NewLocal(stringType, true);
                ILCodeLabel lNullString = NewCodeLabel();

                codeStream.EmitStLoc(vPinnedString);
                codeStream.EmitLdLoc(vPinnedString);

                codeStream.Emit(ILOpcode.conv_i);
                codeStream.Emit(ILOpcode.dup);

                // Marshalling a null string?
                codeStream.Emit(ILOpcode.brfalse, lNullString);

                // TODO: find a safe, non-awkward way to get to OffsetToStringData
                codeStream.EmitLdc(context.Target.PointerSize + 4);
                codeStream.Emit(ILOpcode.add);

                codeStream.EmitLabel(lNullString);
            }
            else
            {
                //
                // ANSI marshalling. Allocate a byte array, copy characters, pin first element.
                //

                TypeDesc byteType = context.GetWellKnownType(WellKnownType.Byte);
                TypeDesc byteArrayType = byteType.MakeArrayType();

                MethodDesc getLengthMethod = stringType.GetMethod("get_Length", null);
                MethodDesc getCharsMethod = stringType.GetMethod("get_Chars", null);

                ILCodeLabel lStart = NewCodeLabel();
                ILCodeLabel lNext = NewCodeLabel();
                ILCodeLabel lNullString = NewCodeLabel();
                ILCodeLabel lDone = NewCodeLabel();

                // Check for the simple case: string is null
                int vStringToMarshal = NewLocal(stringType);
                codeStream.EmitStLoc(vStringToMarshal);
                codeStream.EmitLdLoc(vStringToMarshal);
                codeStream.Emit(ILOpcode.brfalse, lNullString);

                // TODO: figure out how to reference a helper from here and call the helper instead...

                // byte[] byteArray = new byte[stringToMarshal.Length + 1];
                codeStream.EmitLdLoc(vStringToMarshal);
                codeStream.Emit(ILOpcode.call, NewToken(getLengthMethod));
                codeStream.EmitLdc(1);
                codeStream.Emit(ILOpcode.add);
                codeStream.Emit(ILOpcode.newarr, NewToken(byteType));
                int vByteArray = NewLocal(byteArrayType);
                codeStream.EmitStLoc(vByteArray);

                // for (int i = 0; i < byteArray.Length - 1; i++)
                //     byteArray[i] = (byte)stringToMarshal[i];
                int vIterator = NewLocal(context.GetWellKnownType(WellKnownType.Int32));
                codeStream.Emit(ILOpcode.ldc_i4_0);
                codeStream.EmitStLoc(vIterator);
                codeStream.Emit(ILOpcode.br, lStart);
                codeStream.EmitLabel(lNext);
                codeStream.EmitLdLoc(vByteArray);
                codeStream.EmitLdLoc(vIterator);
                codeStream.EmitLdLoc(vStringToMarshal);
                codeStream.EmitLdLoc(vIterator);
                codeStream.Emit(ILOpcode.call, NewToken(getCharsMethod));
                codeStream.Emit(ILOpcode.conv_u1);
                codeStream.Emit(ILOpcode.stelem_i1);
                codeStream.EmitLdLoc(vIterator);
                codeStream.EmitLdc(1);
                codeStream.Emit(ILOpcode.add);
                codeStream.EmitStLoc(vIterator);
                codeStream.EmitLabel(lStart);
                codeStream.EmitLdLoc(vIterator);
                codeStream.EmitLdLoc(vByteArray);
                codeStream.Emit(ILOpcode.ldlen);
                codeStream.Emit(ILOpcode.conv_i4);
                codeStream.EmitLdc(1);
                codeStream.Emit(ILOpcode.sub);
                codeStream.Emit(ILOpcode.blt, lNext);
                codeStream.EmitLdLoc(vByteArray);

                // Pin first element and load the byref on the stack.
                codeStream.EmitLdc(0);
                codeStream.Emit(ILOpcode.ldelema, NewToken(byteType));
                int vPinnedFirstElement = NewLocal(byteArrayType.MakeByRefType(), true);
                codeStream.EmitStLoc(vPinnedFirstElement);
                codeStream.EmitLdLoc(vPinnedFirstElement);
                codeStream.Emit(ILOpcode.conv_i);
                codeStream.Emit(ILOpcode.br, lDone);

                codeStream.EmitLabel(lNullString);
                codeStream.Emit(ILOpcode.ldnull);
                codeStream.Emit(ILOpcode.conv_i);

                codeStream.EmitLabel(lDone);
            }
        }

        public MethodIL EmitIL()
        {
            var codeStream = NewCodeStream();

            // Quick check to see if the signature is something we can support. If the signature
            // is unsupported, the generated method body won't actually do the PInvoke.

            // TODO: need to reject parameters with custom marshalling
            bool isSupportedSignature = IsSimpleType(_targetMethod.Signature.ReturnType) ||
                _targetMethod.Signature.ReturnType.IsVoid;
            // TODO: we don't actually support signatures that set last error
            //isSupportedSignature &= !_importMetadata.SetLastError;
            if (isSupportedSignature)
            {
                for (int i = 0; i < _targetMethod.Signature.Length; i++)
                {
                    if (ManagedTypeToNativeType(_targetMethod.Signature[i]) == null)
                    {
                        isSupportedSignature = false;
                        break;
                    }
                }
            }

            if (!isSupportedSignature)
            {
                // Signature is not supported. Do not call the unmanaged method because that would
                // just result in potentially difficult to debug situations.
                
                // TODO: find a way to emit a warning
                codeStream.Emit(ILOpcode.ldnull);
                codeStream.Emit(ILOpcode.throw_);
                codeStream.Emit(ILOpcode.ret);
            }
            else
            {
                //
                // Convert each argument to something we can pass to native and push it on the stack.
                //

                for (int i = 0; i < _targetMethod.Signature.Length; i++)
                {
                    TypeDesc parameterType = _targetMethod.Signature[i];

                    codeStream.EmitLdArg(i);

                    if (parameterType.IsSzArray)
                    {
                        EmitArrayMarshalling(codeStream, (ArrayType)parameterType);
                    }
                    else if (parameterType.IsByRef)
                    {
                        EmitByRefMarshalling(codeStream, parameterType);
                    }
                    else if (parameterType.IsString)
                    {
                        EmitStringMarshalling(codeStream);
                    }
                    else
                    {
                        Debug.Assert(IsSimpleType(parameterType));
                    }
                }

                // Call the native method
                codeStream.Emit(ILOpcode.call, NewToken(NativeTargetMethod));

                codeStream.Emit(ILOpcode.ret);
            }

            return Link();
        }
    }

    /// <summary>
    /// Synthetic method that represents the actual PInvoke target method.
    /// All parameters are simple types. There will be no code
    /// generated for this method.
    /// </summary>
    sealed class PInvokeTargetNativeMethod : MethodDesc
    {
        TypeDesc _owningType;
        MethodSignature _signature;
        PInvokeMetadata _methodMetadata;

        public PInvokeTargetNativeMethod(TypeDesc owningType, MethodSignature signature, PInvokeMetadata methodMetadata)
        {
            _owningType = owningType;
            _signature = signature;
            _methodMetadata = methodMetadata;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _owningType.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _owningType;
            }
        }

        public override MethodSignature Signature
        {
            get
            {
                return _signature;
            }
        }

        public override string Name
        {
            get
            {
                return "__pInvokeImpl" + _methodMetadata.Name;
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return false;
        }

        public override bool IsPInvokeImpl
        {
            get
            {
                return true;
            }
        }

        public override PInvokeMetadata GetPInvokeMethodImportMetadata()
        {
            return _methodMetadata;
        }

        public override string ToString()
        {
            return "[EXTERNAL]" + Name;
        }
    }
}
