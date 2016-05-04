// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private TypeSystemContext _context;
        private PInvokeMetadata _importMetadata;

        private ILEmitter _emitter;
        private ILCodeStream _marshallingCodeStream;
        private ILCodeStream _returnValueMarshallingCodeStream;
        private ILCodeStream _unmarshallingCodestream;

        private PInvokeMarshallingILEmitter(MethodDesc targetMethod)
        {
            Debug.Assert(targetMethod.IsPInvoke);

            _targetMethod = targetMethod;
            _context = _targetMethod.Context;
            _importMetadata = targetMethod.GetPInvokeMethodMetadata();

            _emitter = null;
            _marshallingCodeStream = null;
            _returnValueMarshallingCodeStream = null;
            _unmarshallingCodestream = null;
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

            if (type.IsPointer)
                return true;

            return false;
        }

        /// <summary>
        /// Marshals an array. Expects the array reference on the stack. Pushes a pinned
        /// managed reference to the first element on the stack or null if the array was
        /// null or empty.
        /// </summary>
        /// <returns>Type the array was marshalled into.</returns>
        private TypeDesc EmitArrayMarshalling(ArrayType arrayType)
        {
            Debug.Assert(arrayType.IsSzArray);

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
            _marshallingCodeStream.Emit(ILOpcode.conv_i);

            return _context.GetWellKnownType(WellKnownType.IntPtr);
        }

        /// <summary>
        /// Marshals a ByRef. Expects the ByRef on the stack. Pushes a pinned
        /// unmanaged pointer to the stack.
        /// </summary>
        /// <returns>Type the ByRef was marshalled into.</returns>
        private TypeDesc EmitByRefMarshalling(ByRefType byRefType)
        {
            ILLocalVariable vPinnedByRef = _emitter.NewLocal(byRefType, true);
            _marshallingCodeStream.EmitStLoc(vPinnedByRef);
            _marshallingCodeStream.EmitLdLoc(vPinnedByRef);
            _marshallingCodeStream.Emit(ILOpcode.conv_i);

            return _context.GetWellKnownType(WellKnownType.IntPtr);
        }

        /// <summary>
        /// Charset for marshalling strings
        /// </summary>
        private PInvokeAttributes GetCharSet()
        {
            PInvokeAttributes charset = _importMetadata.Attributes & PInvokeAttributes.CharSetMask;

            if (charset == 0)
            {
                // ECMA-335 II.10.1.5 - Default value is Ansi.
                charset = PInvokeAttributes.CharSetAnsi;
            }

            if (charset == PInvokeAttributes.CharSetAuto)
            {
                charset = PInvokeAttributes.CharSetUnicode;
            }

            return charset;
        }

        private TypeDesc EmitStringMarshalling()
        {
            if (GetCharSet() == PInvokeAttributes.CharSetUnicode)
            {
                //
                // Unicode marshalling. Pin the string and push a pointer to the first character on the stack.
                //

                TypeDesc stringType = _context.GetWellKnownType(WellKnownType.String);

                ILLocalVariable vPinnedString = _emitter.NewLocal(stringType, true);
                ILCodeLabel lNullString = _emitter.NewCodeLabel();

                _marshallingCodeStream.EmitStLoc(vPinnedString);
                _marshallingCodeStream.EmitLdLoc(vPinnedString);

                _marshallingCodeStream.Emit(ILOpcode.conv_i);
                _marshallingCodeStream.Emit(ILOpcode.dup);

                // Marshalling a null string?
                _marshallingCodeStream.Emit(ILOpcode.brfalse, lNullString);

                _marshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(
                    _context.SystemModule.
                        GetKnownType("System.Runtime.CompilerServices", "RuntimeHelpers").
                            GetKnownMethod("get_OffsetToStringData", null)));

                _marshallingCodeStream.Emit(ILOpcode.add);

                _marshallingCodeStream.EmitLabel(lNullString);

                return _context.GetWellKnownType(WellKnownType.IntPtr);
            }
            else
            {
                //
                // ANSI marshalling. Allocate a byte array, copy characters, pin first element.
                //

                var stringToAnsi = _context.GetHelperEntryPoint("InteropHelpers", "StringToAnsi");

                _marshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(stringToAnsi));

                return EmitArrayMarshalling(_context.GetWellKnownType(WellKnownType.Byte).MakeArrayType());
            }
        }

        private TypeDesc EmitStringBuilderMarshalling()
        {
            if (GetCharSet() == PInvokeAttributes.CharSetUnicode)
            {
                // TODO: Handles [out] marshalling only for now

                var stringBuilderType = _context.SystemModule.GetKnownType("System.Text", "StringBuilder");
                var charArrayType = _context.GetWellKnownType(WellKnownType.Char).MakeArrayType();

                ILLocalVariable vStringBuilder = _emitter.NewLocal(stringBuilderType);
                ILLocalVariable vBuffer = _emitter.NewLocal(charArrayType);

                _marshallingCodeStream.EmitStLoc(vStringBuilder);

                _marshallingCodeStream.EmitLdLoc(vStringBuilder);
                _marshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(
                    _context.GetHelperEntryPoint("InteropHelpers", "GetEmptyStringBuilderBuffer")));
                _marshallingCodeStream.EmitStLoc(vBuffer);

                _unmarshallingCodestream.EmitLdLoc(vStringBuilder);
                _unmarshallingCodestream.EmitLdLoc(vBuffer);
                _unmarshallingCodestream.Emit(ILOpcode.call, _emitter.NewToken(
                    _context.GetHelperEntryPoint("InteropHelpers", "ReplaceStringBuilderBuffer")));

                _marshallingCodeStream.EmitLdLoc(vBuffer);
                return EmitArrayMarshalling(charArrayType);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private TypeDesc EmitBooleanMarshalling()
        {
            _marshallingCodeStream.EmitLdc(0);
            _marshallingCodeStream.Emit(ILOpcode.ceq);
            _marshallingCodeStream.EmitLdc(0);
            _marshallingCodeStream.Emit(ILOpcode.ceq);

            return _context.GetWellKnownType(WellKnownType.Int32);
        }

        private TypeDesc EmitBooleanReturnValueMarshalling()
        {
            _returnValueMarshallingCodeStream.EmitLdc(0);
            _returnValueMarshallingCodeStream.Emit(ILOpcode.ceq);
            _returnValueMarshallingCodeStream.EmitLdc(0);
            _returnValueMarshallingCodeStream.Emit(ILOpcode.ceq);

            return _context.GetWellKnownType(WellKnownType.Int32);
        }

        private MetadataType SafeHandleType
        {
            get
            {
                return _context.SystemModule.GetKnownType("System.Runtime.InteropServices", "SafeHandle");
            }
        }

        private bool IsSafeHandle(TypeDesc type)
        {
            var safeHandleType = this.SafeHandleType;
            while (type != null)
            {
                if (type == safeHandleType)
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        private TypeDesc EmitSafeHandleMarshalling(TypeDesc type)
        {
            var safeHandleType = this.SafeHandleType;

            var vAddRefed = _emitter.NewLocal(_context.GetWellKnownType(WellKnownType.Boolean));
            var vSafeHandle = _emitter.NewLocal(type);

            _marshallingCodeStream.EmitStLoc(vSafeHandle);

            _marshallingCodeStream.EmitLdLoc(vSafeHandle);
            _marshallingCodeStream.EmitLdLoca(vAddRefed);
            _marshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(
                safeHandleType.GetKnownMethod("DangerousAddRef", null)));

            _marshallingCodeStream.EmitLdLoc(vSafeHandle);
            _marshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(
                safeHandleType.GetKnownMethod("DangerousGetHandle", null)));

            // TODO: This should be inside finally block and only executed it the handle was addrefed
            _unmarshallingCodestream.EmitLdLoc(vSafeHandle);
            _unmarshallingCodestream.Emit(ILOpcode.call, _emitter.NewToken(
                safeHandleType.GetKnownMethod("DangerousRelease", null)));

            return _context.GetWellKnownType(WellKnownType.IntPtr);
        }

        private TypeDesc EmitSafeHandleReturnValueMarshalling(TypeDesc type)
        {
            var nativeType = _context.GetWellKnownType(WellKnownType.IntPtr);

            var vSafeHandle = _emitter.NewLocal(type);
            var vReturnValue = _emitter.NewLocal(nativeType);

            _marshallingCodeStream.Emit(ILOpcode.newobj, _emitter.NewToken(type.GetDefaultConstructor()));
            _marshallingCodeStream.EmitStLoc(vSafeHandle);

            _returnValueMarshallingCodeStream.EmitStLoc(vReturnValue);

            _returnValueMarshallingCodeStream.EmitLdLoc(vSafeHandle);
            _returnValueMarshallingCodeStream.EmitLdLoc(vReturnValue);
            _returnValueMarshallingCodeStream.Emit(ILOpcode.call, _emitter.NewToken(
                this.SafeHandleType.GetKnownMethod("SetHandle", null)));

            _returnValueMarshallingCodeStream.EmitLdLoc(vSafeHandle);

            return nativeType;
        }

        private TypeDesc MarshalArgument(TypeDesc type)
        {
            if (IsBlittableType(type))
            {
                return type.UnderlyingType;
            }

            if (type.IsSzArray)
            {
                var arrayType = (ArrayType)type;
                if (IsBlittableType(arrayType.ParameterType))
                    return EmitArrayMarshalling(arrayType);

                if (arrayType.ParameterType == _context.GetWellKnownType(WellKnownType.Char))
                {
                    if (GetCharSet() == PInvokeAttributes.CharSetUnicode)
                    {
                        return EmitArrayMarshalling(arrayType);
                    }
                }
            }

            if (type.IsByRef)
            {
                var byRefType = (ByRefType)type;
                if (IsBlittableType(byRefType.ParameterType))
                    return EmitByRefMarshalling(byRefType);

                if (byRefType.ParameterType == _context.GetWellKnownType(WellKnownType.Char))
                {
                    if (GetCharSet() == PInvokeAttributes.CharSetUnicode)
                    {
                        return EmitByRefMarshalling(byRefType);
                    }
                }
            }

            if (type.IsString)
            {
                return EmitStringMarshalling();
            }

            if (type.Category == TypeFlags.Boolean)
            {
                return EmitBooleanMarshalling();
            }

            if (type is MetadataType)
            {
                var metadataType = (MetadataType)type;

                if (metadataType.Module == _context.SystemModule)
                {
                    var nameSpace = metadataType.Namespace;
                    var name = metadataType.Name;

                    if (name == "StringBuilder" && nameSpace == "System.Text")
                    {
                        return EmitStringBuilderMarshalling();
                    }
                }
            }

            if (IsSafeHandle(type))
            {
                return EmitSafeHandleMarshalling(type);
            }

            throw new NotSupportedException();
        }

        private TypeDesc MarshalReturnValue(TypeDesc type)
        {
            if (type.IsVoid)
            {
                return type;
            }

            if (IsBlittableType(type))
            {
                return type.UnderlyingType;
            }

            if (type.Category == TypeFlags.Boolean)
            {
                return EmitBooleanReturnValueMarshalling();
            }

            if (IsSafeHandle(type))
            {
                return EmitSafeHandleReturnValueMarshalling(type);
            }

            throw new NotSupportedException();
        }

        public MethodIL EmitIL()
        {
            MethodSignature targetMethodSignature = _targetMethod.Signature;

            // We have 4 code streams:
            // - _marshallingCodeStream is used to convert each argument into a native type and 
            // store that into the local
            // - callsiteSetupCodeStream is used to used to load each previously generated local
            // and call the actual target native method.
            // - _returnValueMarshallingCodeStream is used to convert the native return value 
            // to managed one.
            // - _unmarshallingCodestream is used to propagate [out] native arguments values to 
            // managed ones.
            _emitter = new ILEmitter();
            _marshallingCodeStream = _emitter.NewCodeStream();
            ILCodeStream callsiteSetupCodeStream = _emitter.NewCodeStream();
            _returnValueMarshallingCodeStream = _emitter.NewCodeStream();
            _unmarshallingCodestream = _emitter.NewCodeStream();

            TypeDesc[] nativeParameterTypes = new TypeDesc[targetMethodSignature.Length];

            //
            // Parameter marshalling
            //

            //
            // Convert each argument to something we can pass to native and store it in a local.
            // Then load the local in the second code stream.
            //
            for (int i = 0; i < targetMethodSignature.Length; i++)
            {
                // TODO: throw if there's custom marshalling

                _marshallingCodeStream.EmitLdArg(i);

                TypeDesc nativeType = MarshalArgument(targetMethodSignature[i]);

                nativeParameterTypes[i] = nativeType;

                ILLocalVariable vMarshalledTypeTemp = _emitter.NewLocal(nativeType);
                _marshallingCodeStream.EmitStLoc(vMarshalledTypeTemp);

                callsiteSetupCodeStream.EmitLdLoc(vMarshalledTypeTemp);
            }

            //
            // Return value marshalling
            //

            // TODO: throw if SetLastError is true
            // TODO: throw if there's custom marshalling

            TypeDesc nativeReturnType = MarshalReturnValue(targetMethodSignature.ReturnType);

            MethodSignature nativeSig = new MethodSignature(
                targetMethodSignature.Flags, 0, nativeReturnType, nativeParameterTypes);
            MethodDesc nativeMethod =
                new PInvokeTargetNativeMethod(_targetMethod.OwningType, nativeSig, _importMetadata);

            // Call the native method
            callsiteSetupCodeStream.Emit(ILOpcode.call, _emitter.NewToken(nativeMethod));

            _unmarshallingCodestream.Emit(ILOpcode.ret);

            return _emitter.Link();
        }

        public static MethodIL EmitIL(MethodDesc method)
        {
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
                MethodDesc exceptionCtor = method.Context.GetWellKnownType(WellKnownType.Exception).GetKnownMethod(".ctor", ctorSignature);

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
        private static int nativeMethodCounter;

        private TypeDesc _owningType;
        private MethodSignature _signature;
        private PInvokeMetadata _methodMetadata;
        private int sequenceNumber;

        public PInvokeTargetNativeMethod(TypeDesc owningType, MethodSignature signature, PInvokeMetadata methodMetadata)
        {
#if DEBUG
            Debug.Assert(signature.ReturnType.IsBlittable());
            for (int i = 0; i < signature.Length; i++)
                Debug.Assert(signature[i].IsBlittable());
#endif

            _owningType = owningType;
            _signature = signature;
            _methodMetadata = methodMetadata;

            sequenceNumber = System.Threading.Interlocked.Increment(ref nativeMethodCounter);
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
                return "__pInvokeImpl" + _methodMetadata.Name + sequenceNumber;
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
