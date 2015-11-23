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
    public struct PInvokeMarshallingILEmitter
    {
        private MethodDesc _targetMethod;
        private PInvokeMetadata _importMetadata;

        private ILEmitter _emitter;
        private ILCodeStream _marshallingCodeStream;

        private PInvokeMarshallingILEmitter(MethodDesc targetMethod)
        {
            Debug.Assert(targetMethod.IsPInvoke);
            Debug.Assert(RequiresMarshalling(targetMethod));

            _targetMethod = targetMethod;
            _importMetadata = targetMethod.GetPInvokeMethodMetadata();

            _emitter = null;
            _marshallingCodeStream = null;
        }

        /// <summary>
        /// Returns true if <paramref name="method"/> requires a marshalling stub to be generated.
        /// </summary>
        public static bool RequiresMarshalling(MethodDesc method)
        {
            Debug.Assert(method.IsPInvoke);

            // TODO: true if there are any custom marshalling rules on the parameters
            // TODO: true if SetLastError is true

            TypeDesc returnType = method.Signature.ReturnType;
            if (!IsBlittableType(returnType) && !returnType.IsVoid)
                return true;

            for (int i = 0; i < method.Signature.Length; i++)
            {
                if (!IsBlittableType(method.Signature[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if this is a type that doesn't require marshalling.
        /// </summary>
        private static bool IsBlittableType(TypeDesc type)
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
                    if (!IsBlittableType(fieldType))
                        return false;
                }
                return true;
            }

            if (type.IsPointer)
                return true;

            return false;
        }

        /// <summary>
        /// Marshals an array. Expects the array referene on the stack. Pushes a pinned
        /// managed reference to the first element on the stack or null if the array was
        /// null or empty.
        /// </summary>
        /// <returns>Type the array was marshalled into.</returns>
        private TypeDesc EmitArrayMarshalling(ArrayType arrayType)
        {
            Debug.Assert(arrayType.IsSzArray);

            if (!IsBlittableType(arrayType.ParameterType))
                throw new NotSupportedException();

            ILLocalVariable vPinnedFirstElement = _emitter.NewLocal(arrayType.ParameterType.MakeByRefType(), true);
            ILLocalVariable vArray = _emitter.NewLocal(arrayType);
            ILCodeLabel lNullArray = _emitter.NewCodeLabel();

            // Check for null array, or 0 element array.
            _marshallingCodeStream.Emit(ILOpcode.dup);
            _marshallingCodeStream.EmitStLoc(vArray);
            _marshallingCodeStream.Emit(ILOpcode.brfalse, lNullArray);
            _marshallingCodeStream.EmitLdLoc(vArray);
            _marshallingCodeStream.Emit(ILOpcode.ldlen);
            _marshallingCodeStream.Emit(ILOpcode.conv_i4);
            _marshallingCodeStream.Emit(ILOpcode.brfalse, lNullArray);

            // Array has elements.
            _marshallingCodeStream.EmitLdLoc(vArray);
            _marshallingCodeStream.EmitLdc(0);
            _marshallingCodeStream.Emit(ILOpcode.ldelema, _emitter.NewToken(arrayType.ElementType));
            _marshallingCodeStream.EmitStLoc(vPinnedFirstElement);

            // Fall through. If array didn't have elements, vPinnedFirstElement is zeroinit.
            _marshallingCodeStream.EmitLabel(lNullArray);
            _marshallingCodeStream.EmitLdLoc(vPinnedFirstElement);

            return arrayType.Context.GetWellKnownType(WellKnownType.IntPtr);
        }

        /// <summary>
        /// Marshals a ByRef. Expects the ByRef on the stack. Pushes a pinned
        /// unmanaged pointer to the stack.
        /// </summary>
        /// <returns>Type the ByRef was marshalled into.</returns>
        private TypeDesc EmitByRefMarshalling(ByRefType byRefType)
        {
            if (!IsBlittableType(byRefType.ParameterType))
                throw new NotSupportedException();

            ILLocalVariable vPinnedByRef = _emitter.NewLocal(byRefType, true);
            _marshallingCodeStream.EmitStLoc(vPinnedByRef);
            _marshallingCodeStream.EmitLdLoc(vPinnedByRef);
            _marshallingCodeStream.Emit(ILOpcode.conv_i);

            return byRefType.Context.GetWellKnownType(WellKnownType.IntPtr);
        }

        /// <summary>
        /// Marshals a string. Expects the string referene on the stack. Pushes a pointer
        /// to the stack that is safe to pass out to native code.
        /// </summary>
        /// <returns>The type the string was marshalled into.</returns>
        private TypeDesc EmitStringMarshalling()
        {
            PInvokeAttributes charset = _importMetadata.Attributes & PInvokeAttributes.CharSetMask;

            TypeSystemContext context = _targetMethod.Context;

            if (charset == 0)
            {
                // ECMA-335 II.10.1.5 - Default value is Ansi.
                charset = PInvokeAttributes.CharSetAnsi;
            }

            if (charset == PInvokeAttributes.CharSetAuto)
            {
                charset = PInvokeAttributes.CharSetUnicode;
            }

            TypeDesc stringType = context.GetWellKnownType(WellKnownType.String);

            if (charset == PInvokeAttributes.CharSetUnicode)
            {
                //
                // Unicode marshalling. Pin the string and push a pointer to the first character on the stack.
                //

                ILLocalVariable vPinnedString = _emitter.NewLocal(stringType, true);
                ILCodeLabel lNullString = _emitter.NewCodeLabel();

                _marshallingCodeStream.EmitStLoc(vPinnedString);
                _marshallingCodeStream.EmitLdLoc(vPinnedString);

                _marshallingCodeStream.Emit(ILOpcode.conv_i);
                _marshallingCodeStream.Emit(ILOpcode.dup);

                // Marshalling a null string?
                _marshallingCodeStream.Emit(ILOpcode.brfalse, lNullString);

                // TODO: find a safe, non-awkward way to get to OffsetToStringData
                _marshallingCodeStream.EmitLdc(context.Target.PointerSize + 4);
                _marshallingCodeStream.Emit(ILOpcode.add);

                _marshallingCodeStream.EmitLabel(lNullString);
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

                ILCodeLabel lStart = _emitter.NewCodeLabel();
                ILCodeLabel lNext = _emitter.NewCodeLabel();
                ILCodeLabel lNullString = _emitter.NewCodeLabel();
                ILCodeLabel lDone = _emitter.NewCodeLabel();

                // Check for the simple case: string is null
                ILLocalVariable vStringToMarshal = _emitter.NewLocal(stringType);
                _marshallingCodeStream.EmitStLoc(vStringToMarshal);
                _marshallingCodeStream.EmitLdLoc(vStringToMarshal);
                _marshallingCodeStream.Emit(ILOpcode.brfalse, lNullString);

                // TODO: figure out how to reference a helper from here and call the helper instead...

                // byte[] byteArray = new byte[stringToMarshal.Length + 1];
                _marshallingCodeStream.EmitLdLoc(vStringToMarshal);
                _marshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(getLengthMethod));
                _marshallingCodeStream.EmitLdc(1);
                _marshallingCodeStream.Emit(ILOpcode.add);
                _marshallingCodeStream.Emit(ILOpcode.newarr, _emitter.NewToken(byteType));
                ILLocalVariable vByteArray = _emitter.NewLocal(byteArrayType);
                _marshallingCodeStream.EmitStLoc(vByteArray);

                // for (int i = 0; i < byteArray.Length - 1; i++)
                //     byteArray[i] = (byte)stringToMarshal[i];
                ILLocalVariable vIterator = _emitter.NewLocal(context.GetWellKnownType(WellKnownType.Int32));
                _marshallingCodeStream.Emit(ILOpcode.ldc_i4_0);
                _marshallingCodeStream.EmitStLoc(vIterator);
                _marshallingCodeStream.Emit(ILOpcode.br, lStart);
                _marshallingCodeStream.EmitLabel(lNext);
                _marshallingCodeStream.EmitLdLoc(vByteArray);
                _marshallingCodeStream.EmitLdLoc(vIterator);
                _marshallingCodeStream.EmitLdLoc(vStringToMarshal);
                _marshallingCodeStream.EmitLdLoc(vIterator);
                _marshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(getCharsMethod));
                _marshallingCodeStream.Emit(ILOpcode.conv_u1);
                _marshallingCodeStream.Emit(ILOpcode.stelem_i1);
                _marshallingCodeStream.EmitLdLoc(vIterator);
                _marshallingCodeStream.EmitLdc(1);
                _marshallingCodeStream.Emit(ILOpcode.add);
                _marshallingCodeStream.EmitStLoc(vIterator);
                _marshallingCodeStream.EmitLabel(lStart);
                _marshallingCodeStream.EmitLdLoc(vIterator);
                _marshallingCodeStream.EmitLdLoc(vByteArray);
                _marshallingCodeStream.Emit(ILOpcode.ldlen);
                _marshallingCodeStream.Emit(ILOpcode.conv_i4);
                _marshallingCodeStream.EmitLdc(1);
                _marshallingCodeStream.Emit(ILOpcode.sub);
                _marshallingCodeStream.Emit(ILOpcode.blt, lNext);
                _marshallingCodeStream.EmitLdLoc(vByteArray);

                // Pin first element and load the byref on the stack.
                _marshallingCodeStream.EmitLdc(0);
                _marshallingCodeStream.Emit(ILOpcode.ldelema, _emitter.NewToken(byteType));
                ILLocalVariable vPinnedFirstElement = _emitter.NewLocal(byteArrayType.MakeByRefType(), true);
                _marshallingCodeStream.EmitStLoc(vPinnedFirstElement);
                _marshallingCodeStream.EmitLdLoc(vPinnedFirstElement);
                _marshallingCodeStream.Emit(ILOpcode.conv_i);
                _marshallingCodeStream.Emit(ILOpcode.br, lDone);

                _marshallingCodeStream.EmitLabel(lNullString);
                _marshallingCodeStream.Emit(ILOpcode.ldnull);
                _marshallingCodeStream.Emit(ILOpcode.conv_i);

                _marshallingCodeStream.EmitLabel(lDone);
            }

            return _targetMethod.Context.GetWellKnownType(WellKnownType.IntPtr);
        }

        public MethodIL EmitIL()
        {
            // We have two code streams - one is used to convert each argument into a native type
            // and store that into the local. The other is used to load each previously generated local
            // and call the actual target native method.
            _emitter = new ILEmitter();
            _marshallingCodeStream = _emitter.NewCodeStream();
            ILCodeStream callsiteSetupCodeStream = _emitter.NewCodeStream();

            // TODO: throw if SetLastError is true
            // TODO: throw if there's custom marshalling
            TypeDesc nativeReturnType = _targetMethod.Signature.ReturnType;
            if (!IsBlittableType(nativeReturnType) && !nativeReturnType.IsVoid)
                throw new NotSupportedException();

            TypeDesc[] nativeParameterTypes = new TypeDesc[_targetMethod.Signature.Length];

            //
            // Convert each argument to something we can pass to native and store it in a local.
            // Then load the local in the second code stream.
            //
            for (int i = 0; i < _targetMethod.Signature.Length; i++)
            {
                // TODO: throw if there's custom marshalling
                TypeDesc parameterType = _targetMethod.Signature[i];

                _marshallingCodeStream.EmitLdArg(i);

                TypeDesc nativeType;
                if (parameterType.IsSzArray)
                {
                    nativeType = EmitArrayMarshalling((ArrayType)parameterType);
                }
                else if (parameterType.IsByRef)
                {
                    nativeType = EmitByRefMarshalling((ByRefType)parameterType);
                }
                else if (parameterType.IsString)
                {
                    nativeType = EmitStringMarshalling();
                }
                else
                {
                    if (!IsBlittableType(parameterType))
                        throw new NotSupportedException();

                    nativeType = parameterType.UnderlyingType;
                }

                nativeParameterTypes[i] = nativeType;

                ILLocalVariable vMarshalledTypeTemp = _emitter.NewLocal(nativeType);
                _marshallingCodeStream.EmitStLoc(vMarshalledTypeTemp);

                callsiteSetupCodeStream.EmitLdLoc(vMarshalledTypeTemp);
            }

            MethodSignature nativeSig = new MethodSignature(
                _targetMethod.Signature.Flags, 0, nativeReturnType, nativeParameterTypes);
            MethodDesc nativeMethod =
                new PInvokeTargetNativeMethod(_targetMethod.OwningType, nativeSig, _importMetadata);

            // Call the native method
            callsiteSetupCodeStream.Emit(ILOpcode.call, _emitter.NewToken(nativeMethod));
            callsiteSetupCodeStream.Emit(ILOpcode.ret);

            return _emitter.Link();
        }

        public static MethodIL EmitIL(MethodDesc method)
        {
            if (!RequiresMarshalling(method))
                return null;

            try
            {
                return new PInvokeMarshallingILEmitter(method).EmitIL();
            }
            catch (NotSupportedException)
            {
                ILEmitter emitter = new ILEmitter();
                string message = "Method '" + method.ToString() +
                    "' requires non-trivial marshalling that is not yet supported by this compiler.";

                TypeSystemContext context = method.Context;
                MethodSignature ctorSignature = new MethodSignature(0, 0, context.GetWellKnownType(WellKnownType.Void),
                    new TypeDesc[] { context.GetWellKnownType(WellKnownType.String) });
                MethodDesc exceptionCtor = method.Context.GetWellKnownType(WellKnownType.Exception).GetMethod(".ctor", ctorSignature);

                ILCodeStream codeStream = emitter.NewCodeStream();
                codeStream.Emit(ILOpcode.ldstr, emitter.NewToken(message));
                codeStream.Emit(ILOpcode.newobj, emitter.NewToken(exceptionCtor));
                codeStream.Emit(ILOpcode.throw_);
                codeStream.Emit(ILOpcode.ret);

                return emitter.Link();
            }
        }
    }

    /// <summary>
    /// Synthetic method that represents the actual PInvoke target method.
    /// All parameters are simple types. There will be no code
    /// generated for this method.
    /// </summary>
    internal sealed class PInvokeTargetNativeMethod : MethodDesc
    {
        private TypeDesc _owningType;
        private MethodSignature _signature;
        private PInvokeMetadata _methodMetadata;

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

        public override bool IsPInvoke
        {
            get
            {
                return true;
            }
        }

        public override PInvokeMetadata GetPInvokeMethodMetadata()
        {
            return _methodMetadata;
        }

        public override string ToString()
        {
            return "[EXTERNAL]" + Name;
        }
    }
}
