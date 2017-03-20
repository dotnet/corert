// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;
using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    internal enum StructMarshallingThunkType : byte
    {
        ManagedToNative = 1,
        NativeToManage = 2,
        Cleanup = 4
    }

    internal class StructMarshallingThunk : ILStubMethod
    {
        internal readonly MetadataType ManagedType;
        internal readonly NativeStructType NativeType;
        internal readonly StructMarshallingThunkType ThunkType;
        private  InteropStateManager _interopStateManager;
        private TypeDesc _owningType;

        public StructMarshallingThunk(TypeDesc owningType, MetadataType managedType, StructMarshallingThunkType thunkType, InteropStateManager interopStateManager)
        {
            _owningType = owningType;
            ManagedType = managedType;
            _interopStateManager = interopStateManager;
            NativeType = _interopStateManager.GetStructMarshallingNativeType(managedType);
            ThunkType = thunkType;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return ManagedType.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _owningType;
            }
        }

        private MethodSignature _signature;
        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    TypeDesc[] parameters;
                    if (ThunkType == StructMarshallingThunkType.Cleanup)
                    {
                        parameters = new TypeDesc[] {
                            NativeType.MakeByRefType()
                        };
                    }
                    else
                    {
                        parameters = new TypeDesc[] {
                            ManagedType.MakeByRefType(),
                            NativeType.MakeByRefType()
                        };
                    }
                    _signature = new MethodSignature(MethodSignatureFlags.Static, 0, Context.GetWellKnownType(WellKnownType.Void), parameters);
                }
                return _signature;
            }
        }

        public override string Name
        {
            get
            {
                if (ThunkType == StructMarshallingThunkType.ManagedToNative)
                {
                    return "ManagedToNative__" + ((MetadataType)ManagedType).Name;
                }
                else if (ThunkType == StructMarshallingThunkType.NativeToManage)
                {
                    return "NativeToManaged__" + ((MetadataType)ManagedType).Name;
                }
                else
                {
                    return "Cleanup__" + ((MetadataType)ManagedType).Name;
                }
            }
        }

        private int GetNumberOfInstanceFields()
        {
            int numFields = 0;
            foreach (var field in ManagedType.GetFields())
            {
                if (field.IsStatic)
                {
                    continue;
                }
                numFields++;
            }
            return numFields;
        }

        private MethodIL EmitMarshallingIL(PInvokeILCodeStreams pInvokeILCodeStreams, Marshaller[] marshallers)
        {
            ILEmitter emitter = pInvokeILCodeStreams.Emitter;

            IEnumerator<FieldDesc> nativeEnumerator = NativeType.GetFields().GetEnumerator();

            int index = 0;
            foreach (var managedField in ManagedType.GetFields())
            {
                if (managedField.IsStatic)
                {
                    continue;
                }

                bool notEmpty = nativeEnumerator.MoveNext();
                Debug.Assert(notEmpty == true);

                var nativeField = nativeEnumerator.Current;
                Debug.Assert(nativeField != null);

                //
                // Field marshallers expects the value of the fields to be 
                // loaded on the stack. We load the value on the stack
                // before calling the marshallers
                //

                if (ThunkType == StructMarshallingThunkType.ManagedToNative)
                {
                    LoadFieldValueFromArg(0, managedField, pInvokeILCodeStreams);
                }
                else if (ThunkType == StructMarshallingThunkType.NativeToManage)
                {
                    LoadFieldValueFromArg(1, nativeField, pInvokeILCodeStreams);
                }

                marshallers[index++].EmitMarshallingIL(pInvokeILCodeStreams);

                if (ThunkType == StructMarshallingThunkType.ManagedToNative)
                {
                    StoreFieldValueFromArg(1, nativeField, pInvokeILCodeStreams);
                }
                else if (ThunkType == StructMarshallingThunkType.NativeToManage)
                {
                    StoreFieldValueFromArg(0, managedField, pInvokeILCodeStreams);
                }
            }

            Debug.Assert(!nativeEnumerator.MoveNext());

            pInvokeILCodeStreams.UnmarshallingCodestream.Emit(ILOpcode.ret);
            return emitter.Link(this);
        }

        private MethodIL EmitCleanupIL(PInvokeILCodeStreams pInvokeILCodeStreams, Marshaller[] marshallers)
        {
            ILEmitter emitter = pInvokeILCodeStreams.Emitter;
            ILCodeStream codeStream = pInvokeILCodeStreams.MarshallingCodeStream;
            IEnumerator<FieldDesc> nativeEnumerator = NativeType.GetFields().GetEnumerator();
            for (int i = 0; i < marshallers.Length; i++)
            {
                bool valid = nativeEnumerator.MoveNext();

                Debug.Assert(valid);

                if (marshallers[i].CleanupRequired)
                {
                    LoadFieldValueFromArg(0, nativeEnumerator.Current, pInvokeILCodeStreams);
                    marshallers[i].EmitElementCleanup(codeStream, emitter);
                }
            }

            pInvokeILCodeStreams.UnmarshallingCodestream.Emit(ILOpcode.ret);
            return emitter.Link(this);
        }

        public override MethodIL EmitIL()
        {
            try
            {
                Debug.Assert(_interopStateManager != null);
                Marshaller[] marshallers = new Marshaller[GetNumberOfInstanceFields()];
                MarshalAsDescriptor[] marshalAsDescriptors = ((MetadataType)ManagedType).GetFieldMarshalAsDescriptors();
                
                PInvokeFlags flags = new PInvokeFlags();
                if (ManagedType.PInvokeStringFormat == PInvokeStringFormat.UnicodeClass || ManagedType.PInvokeStringFormat == PInvokeStringFormat.AutoClass)
                {
                    flags.CharSet = CharSet.Unicode;
                }
                else
                {
                    flags.CharSet = CharSet.Ansi;
                }

                
                int index = 0;

                foreach (FieldDesc field in ManagedType.GetFields())
                {
                    if (field.IsStatic)
                    {
                        continue;
                    }

                    marshallers[index] = Marshaller.CreateMarshaller(field.FieldType,
                                                                        MarshallerType.Field,
                                                                        marshalAsDescriptors[index],
                                                                        (ThunkType == StructMarshallingThunkType.NativeToManage) ? MarshalDirection.Reverse : MarshalDirection.Forward,
                                                                        marshallers,
                                                                        _interopStateManager,
                                                                        index,
                                                                        flags,
                                                                        isIn: true,     /* Struct fields are considered as IN within the helper*/
                                                                        isOut: false,
                                                                        isReturn: false);
                    index++;
                }

                PInvokeILCodeStreams pInvokeILCodeStreams = new PInvokeILCodeStreams();

                if (ThunkType == StructMarshallingThunkType.Cleanup)
                {
                    return EmitCleanupIL(pInvokeILCodeStreams, marshallers);
                }
                else
                {
                    return EmitMarshallingIL(pInvokeILCodeStreams, marshallers);
                }
            }
            catch (NotSupportedException)
            {
                string message = "Struct '" + ((MetadataType)ManagedType).Name +
                    "' requires non-trivial marshalling that is not yet supported by this compiler.";
                return MarshalHelpers.EmitExceptionBody(message, this);
            }
            catch (InvalidProgramException ex)
            {
                Debug.Assert(!String.IsNullOrEmpty(ex.Message));
                return MarshalHelpers.EmitExceptionBody(ex.Message, this);
            }

        }
        /// <summary>
        /// Loads the value of field of a struct at argument index argIndex to stack
        /// </summary>
        private void LoadFieldValueFromArg(int argIndex, FieldDesc field, PInvokeILCodeStreams pInvokeILCodeStreams)
        {
            ILCodeStream stream = pInvokeILCodeStreams.MarshallingCodeStream;
            ILEmitter emitter = pInvokeILCodeStreams.Emitter;

            stream.EmitLdArg(argIndex);
            stream.Emit(ILOpcode.ldfld, emitter.NewToken(field));
        }

        private void StoreFieldValueFromArg(int argIndex, FieldDesc field, PInvokeILCodeStreams pInvokeILCodeStreams)
        {
            ILCodeStream stream = pInvokeILCodeStreams.MarshallingCodeStream;
            ILEmitter emitter = pInvokeILCodeStreams.Emitter;
            Internal.IL.Stubs.ILLocalVariable var = emitter.NewLocal(field.FieldType);

            stream.EmitStLoc(var);

            stream.EmitLdArg(argIndex);
            stream.EmitLdLoc(var);
            stream.Emit(ILOpcode.stfld, emitter.NewToken(field));
        }
    }
}
