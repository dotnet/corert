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
        BlittableArray,
        BlittableByRef,
        String,
        Bool,
        StringBuilder,
        SafeHandle,
        VoidReturn
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
        public TypeDesc NativeType;
        public TypeDesc ManagedType;
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
                                                pInvokeParameterdata.MarshalAsDescriptor, 
                                                pInvokeMethodData, 
                                                pInvokeParameterdata);
            
            // Create the marshaller based on MarshallerKind
            Marshaller marshaller = Marshaller.CreateMarshallerInternal(marshallerKind);
            marshaller.PInvokeMethodData = pInvokeMethodData;
            marshaller.PInvokeParameterMetadata = pInvokeParameterdata;
            marshaller.MarshallerKind = marshallerKind;
            marshaller.NativeType = null;
            marshaller.ManagedType = parameterType;

            return marshaller;
        }

        private static Marshaller CreateMarshallerInternal(MarshallerKind kind)
        {
            switch (kind)
            {
                case MarshallerKind.BlittableValue:
                    return new BlittableValueMarshaller();
                case MarshallerKind.BlittableArray:
                    return new BlittableArrayMarshaller();
                case MarshallerKind.BlittableByRef:
                    return new BlittableByRefMarshaller();
                case MarshallerKind.Bool:
                    return new BooleanMarshaller();
                case MarshallerKind.String:
                    return new StringMarshaller();
                case MarshallerKind.SafeHandle:
                    return new SafeHandleMarshaller();
                case MarshallerKind.StringBuilder:
                    return new StringBuilderMarshaller();
                case MarshallerKind.VoidReturn:
                    return new VoidReturnMarshaller();
                 default:
                    throw new NotSupportedException();
            }
        }
        private static MarshallerKind GetMarshallerKind(TypeDesc type, MarshalAsDescriptor marshalAs, PInvokeMethodData PInvokeMethodData, ParameterMetadata paramMetadata)
        {
            if (paramMetadata.Return)
            {
                if (type.IsVoid)
                {
                    return MarshallerKind.VoidReturn;
                }

                if (MarshalHelpers.IsBlittableType(type))
                {
                    return MarshallerKind.BlittableValue;
                }

                if (type.Category == TypeFlags.Boolean)
                {
                    return MarshallerKind.Bool;
                }

                if (PInvokeMethodData.IsSafeHandle(type))
                {
                    return MarshallerKind.SafeHandle;
                }
                throw new NotSupportedException();
            }

            if (MarshalHelpers.IsBlittableType(type))
            {
                return MarshallerKind.BlittableValue;
            }
            TypeSystemContext context = PInvokeMethodData.Context;

            if (type.IsSzArray)
            {
                var arrayType = (ArrayType)type;
                if (MarshalHelpers.IsBlittableType(arrayType.ParameterType))
                    return MarshallerKind.BlittableArray;

                if (arrayType.ParameterType == context.GetWellKnownType(WellKnownType.Char))
                {
                    if (PInvokeMethodData.GetCharSet() == PInvokeAttributes.CharSetUnicode)
                    {
                        return MarshallerKind.BlittableArray;
                    }
                }
            }

            if (type.IsByRef)
            {
                var byRefType = (ByRefType)type;
                if (MarshalHelpers.IsBlittableType(byRefType.ParameterType))
                    return MarshallerKind.BlittableByRef;

                if (byRefType.ParameterType == context.GetWellKnownType(WellKnownType.Char))
                {
                    if (PInvokeMethodData.GetCharSet() == PInvokeAttributes.CharSetUnicode)
                    {
                        return MarshallerKind.BlittableByRef;
                    }
                }
            }

            if (type.IsString)
            {
                return MarshallerKind.String;
            }

            if (type.Category == TypeFlags.Boolean)
            {
                return MarshallerKind.Bool;
            }

            if (type is MetadataType)
            {
                var metadataType = (MetadataType)type;

                if (metadataType.Module == context.SystemModule)
                {
                    var nameSpace = metadataType.Namespace;
                    var name = metadataType.Name;

                    if (name == "StringBuilder" && nameSpace == "System.Text")
                    {
                        return MarshallerKind.StringBuilder;
                    }
                }
            }

            if (PInvokeMethodData.IsSafeHandle(type))
            {
                return MarshallerKind.SafeHandle;
            }

            // Temporary fix for out SafeHandle scenario
            // TODO: handle in,out,ref properly
            if (paramMetadata.Out && !paramMetadata.In)
            {
                ByRefType byRefType = type as ByRefType;
                if (byRefType != null)
                {
                    if (PInvokeMethodData.IsSafeHandle(byRefType.ParameterType))
                    {
                        return MarshallerKind.SafeHandle;
                    }
                }
            }

            throw new NotSupportedException();
        }
        #endregion

        public void EmitMarshallingIL(ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream callsiteSetupCodeStream, ILCodeStream unmarshallingCodeStream, ILCodeStream returnValueCodeStream)
        {
            if (!PInvokeParameterMetadata.Return)
            {
                marshallingCodeStream.EmitLdArg(PInvokeParameterMetadata.Index -1);
                NativeType = MarshalArgument(ManagedType, emitter, marshallingCodeStream, unmarshallingCodeStream);
                IL.Stubs.ILLocalVariable vMarshalledTypeTemp = emitter.NewLocal(NativeType);
                marshallingCodeStream.EmitStLoc(vMarshalledTypeTemp);
                callsiteSetupCodeStream.EmitLdLoc(vMarshalledTypeTemp);
            }
            else
            {
                NativeType = MarshalReturn(ManagedType, emitter, marshallingCodeStream, returnValueCodeStream);
            }
        }

        /// <summary>
        /// Marshals a managed type to native type
        /// </summary>
        /// <returns></returns>
        protected virtual TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            return null;
        }
        protected virtual TypeDesc MarshalReturn(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream returnValueMarshallingCodeStream)
        {
            return null;
        }
    }

    class VoidReturnMarshaller : Marshaller
    {
        protected override TypeDesc MarshalReturn(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream returnValueMarshallingCodeStream)
        {
            return managedType;
        }
    }

    class BlittableValueMarshaller : Marshaller
    {
        protected override TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            return managedType.UnderlyingType;
        }
        protected override TypeDesc MarshalReturn(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream returnValueMarshallingCodeStream)
        {
            return managedType.UnderlyingType;
        }
    }

    class BlittableArrayMarshaller : Marshaller
    {
        protected override TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            var arrayType = (ArrayType)managedType;
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

            return PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);
        }
    }

    class BlittableByRefMarshaller : Marshaller
    {
        protected override TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            var byRefType = (ByRefType)managedType;
            IL.Stubs.ILLocalVariable vPinnedByRef = emitter.NewLocal(byRefType, true);
            marshallingCodeStream.EmitStLoc(vPinnedByRef);
            marshallingCodeStream.EmitLdLoc(vPinnedByRef);
            marshallingCodeStream.Emit(ILOpcode.conv_i);

            return PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);
        }
    }

    class BooleanMarshaller : Marshaller
    {
        protected override TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            marshallingCodeStream.EmitLdc(0);
            marshallingCodeStream.Emit(ILOpcode.ceq);
            marshallingCodeStream.EmitLdc(0);
            marshallingCodeStream.Emit(ILOpcode.ceq);

            return PInvokeMethodData.Context.GetWellKnownType(WellKnownType.Int32);
        }

        protected override TypeDesc MarshalReturn(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream returnValueMarshallingCodeStream)
        {
            returnValueMarshallingCodeStream.EmitLdc(0);
            returnValueMarshallingCodeStream.Emit(ILOpcode.ceq);
            returnValueMarshallingCodeStream.EmitLdc(0);
            returnValueMarshallingCodeStream.Emit(ILOpcode.ceq);

            return PInvokeMethodData.Context.GetWellKnownType(WellKnownType.Int32);
        }
    }

    class StringMarshaller : BlittableArrayMarshaller
    {
        protected override TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            TypeSystemContext context = PInvokeMethodData.Context;
            if (PInvokeMethodData.GetCharSet() == PInvokeAttributes.CharSetUnicode)
            {
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

                return context.GetWellKnownType(WellKnownType.IntPtr);
            }
            else
            {
                //
                // ANSI marshalling. Allocate a byte array, copy characters, pin first element.
                //

                var stringToAnsi = context.GetHelperEntryPoint("InteropHelpers", "StringToAnsi");

                marshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(stringToAnsi));

                // Call the Array marshaller MarshalArgument
                return base.MarshalArgument(context.GetWellKnownType(WellKnownType.Byte).MakeArrayType(), emitter, marshallingCodeStream, unmarshallingCodeStream);
            }
        }
    }

    class SafeHandleMarshaller : Marshaller
    {
        protected override TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            // we don't support [IN,OUT] together yet, either IN or OUT
            Debug.Assert(!(PInvokeParameterMetadata.Out && PInvokeParameterMetadata.In));

            var safeHandleType = PInvokeMethodData.SafeHandleType;

            if (PInvokeParameterMetadata.Out)
            {
                // 1) If this is an output parameter we need to preallocate a SafeHandle to wrap the new native handle value. We
                //    must allocate this before the native call to avoid a failure point when we already have a native resource
                //    allocated. We must allocate a new SafeHandle even if we have one on input since both input and output native
                //    handles need to be tracked and released by a SafeHandle.
                // 2) Initialize a local IntPtr that will be passed to the native call. 
                // 3) After the native call, the new handle value is written into the output SafeHandle and that SafeHandle
                //    is propagated back to the caller.

                Debug.Assert(managedType is ByRefType);
                var vOutArg = emitter.NewLocal(managedType);
                marshallingCodeStream.EmitStLoc(vOutArg);

                TypeDesc resolvedType = ((ByRefType)managedType).ParameterType;

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
                unmarshallingCodeStream.Emit(ILOpcode.stind_i);

                return nativeType;
            }
            else
            {
                var vAddRefed = emitter.NewLocal(PInvokeMethodData.Context.GetWellKnownType(WellKnownType.Boolean));
                var vSafeHandle = emitter.NewLocal(managedType);

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

                return PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);
            }
        }

        protected override TypeDesc MarshalReturn(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream returnValueMarshallingCodeStream )
        {
            var nativeType = PInvokeMethodData.Context.GetWellKnownType(WellKnownType.IntPtr);

            var vSafeHandle = emitter.NewLocal(managedType);
            var vReturnValue = emitter.NewLocal(nativeType);

            marshallingCodeStream.Emit(ILOpcode.newobj, emitter.NewToken(managedType.GetDefaultConstructor()));
            marshallingCodeStream.EmitStLoc(vSafeHandle);

            returnValueMarshallingCodeStream.EmitStLoc(vReturnValue);

            returnValueMarshallingCodeStream.EmitLdLoc(vSafeHandle);
            returnValueMarshallingCodeStream.EmitLdLoc(vReturnValue);
            returnValueMarshallingCodeStream.Emit(ILOpcode.call, emitter.NewToken(
                PInvokeMethodData.SafeHandleType.GetKnownMethod("SetHandle", null)));

            returnValueMarshallingCodeStream.EmitLdLoc(vSafeHandle);

            return nativeType;
        }
    }

    class StringBuilderMarshaller : BlittableArrayMarshaller
    {
        protected override TypeDesc MarshalArgument(TypeDesc managedType, ILEmitter emitter, ILCodeStream marshallingCodeStream, ILCodeStream unmarshallingCodeStream)
        {
            TypeSystemContext context = PInvokeMethodData.Context;
            if (PInvokeMethodData.GetCharSet() == PInvokeAttributes.CharSetUnicode)
            {
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
                return base.MarshalArgument(charArrayType, emitter, marshallingCodeStream, unmarshallingCodeStream);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}