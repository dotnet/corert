﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using Internal.TypeSystem;
using Internal.IL;
using Internal.IL.Stubs;

using Debug = System.Diagnostics.Debug;

//
// Functionality related to instantiating unboxing thunks
//
// To support calling canonical interface methods on generic valuetypes,
// the compiler needs to generate unboxing+instantiating thunks that bridge
// the difference between the two calling conventions.
//
// As a refresher:
// * Instance methods on shared generic valuetypes expect two arguments
//   (aside from the arguments declared in the signature): a ByRef to the
//   first byte of the value of the valuetype (this), and a generic context
//   argument (EEType)
// * Interface calls expect 'this' to be a reference type (with the generic
//   context to be inferred from 'this' by the callee).
//
// Instantiating and unboxing stubs bridge this by extracting a managed
// pointer out of a boxed valuetype, along with the EEType of the boxed
// valuetype (to provide the generic context) before dispatching to the
// instance method with the different calling convention.
//
// We compile them by:
// * Pretending the unboxing stub is an instance method on a reference type
//   with the same layout as a boxed valuetype (this matches the calling
//   convention expected by the caller).
// * Having the unboxing stub load the m_pEEType field (to get generic
//   context) and a byref to the actual value (to get a 'this' expected by
//   valuetype methods)
// * Generating a call to a fake instance method on the valuetype that has
//   the hidden (generic context) argument explicitly present in the
//   signature. We need a fake method to be able to refer to the hidden parameter
//   from IL.
//
// At a later stage (once codegen is done), we replace the references to the
// fake instance method with the real instance method. Their signatures after
// compilation is identical.
//

namespace ILCompiler
{
    // Contains functionality related to pseudotypes representing boxed instances of value types
    partial class CompilerTypeSystemContext
    {
        /// <summary>
        /// For a shared (canonical) instance method on a generic valuetype, gets a method that can be used to call the
        /// method given a boxed version of the generic valuetype as 'this' pointer.
        /// </summary>
        public MethodDesc GetSpecialUnboxingThunk(MethodDesc targetMethod, ModuleDesc ownerModuleOfThunk)
        {
            Debug.Assert(targetMethod.IsSharedByGenericInstantiations);
            Debug.Assert(!targetMethod.Signature.IsStatic);

            TypeDesc owningType = targetMethod.OwningType;
            Debug.Assert(owningType.IsValueType);

            var owningTypeDefinition = (MetadataType)owningType.GetTypeDefinition();

            // Get a reference type that has the same layout as the boxed valuetype.
            var typeKey = new BoxedValuetypeHashtableKey(owningTypeDefinition, ownerModuleOfThunk);
            BoxedValueType boxedTypeDefinition = _boxedValuetypeHashtable.GetOrCreateValue(typeKey);

            // Get a method on the reference type with the same signature as the target method (but different
            // calling convention, since 'this' will be a reference type).
            var targetMethodDefinition = targetMethod.GetTypicalMethodDefinition();
            var methodKey = new UnboxingThunkHashtableKey(targetMethodDefinition, boxedTypeDefinition);
            GenericUnboxingThunk thunkDefinition = _unboxingThunkHashtable.GetOrCreateValue(methodKey);

            // Find the thunk on the instantiated version of the reference type.
            Debug.Assert(owningType != owningTypeDefinition);
            InstantiatedType boxedType = boxedTypeDefinition.MakeInstantiatedType(owningType.Instantiation);

            MethodDesc thunk = GetMethodForInstantiatedType(thunkDefinition, boxedType);
            Debug.Assert(!thunk.HasInstantiation);

            return thunk;
        }

        /// <summary>
        /// Returns true of <paramref name="method"/> is a standin method for unboxing thunk target.
        /// </summary>
        public bool IsSpecialUnboxingThunkTargetMethod(MethodDesc method)
        {
            return method.GetTypicalMethodDefinition().GetType() == typeof(ValueTypeInstanceMethodWithHiddenParameter);
        }

        /// <summary>
        /// Returns the real target method of an unboxing stub.
        /// </summary>
        public MethodDesc GetRealSpecialUnboxingThunkTargetMethod(MethodDesc method)
        {
            MethodDesc typicalMethod = method.GetTypicalMethodDefinition();
            MethodDesc methodDefinitionRepresented = ((ValueTypeInstanceMethodWithHiddenParameter)typicalMethod).MethodRepresented;
            return GetMethodForInstantiatedType(methodDefinitionRepresented, (InstantiatedType)method.OwningType);
        }

        private struct BoxedValuetypeHashtableKey
        {
            public readonly MetadataType ValueType;
            public readonly ModuleDesc OwningModule;

            public BoxedValuetypeHashtableKey(MetadataType valueType, ModuleDesc owningModule)
            {
                ValueType = valueType;
                OwningModule = owningModule;
            }
        }

        private class BoxedValuetypeHashtable : LockFreeReaderHashtable<BoxedValuetypeHashtableKey, BoxedValueType>
        {
            protected override int GetKeyHashCode(BoxedValuetypeHashtableKey key)
            {
                return key.ValueType.GetHashCode();
            }
            protected override int GetValueHashCode(BoxedValueType value)
            {
                return value.ValueTypeRepresented.GetHashCode();
            }
            protected override bool CompareKeyToValue(BoxedValuetypeHashtableKey key, BoxedValueType value)
            {
                return Object.ReferenceEquals(key.ValueType, value.ValueTypeRepresented) &&
                    Object.ReferenceEquals(key.OwningModule, value.Module);
            }
            protected override bool CompareValueToValue(BoxedValueType value1, BoxedValueType value2)
            {
                return Object.ReferenceEquals(value1.ValueTypeRepresented, value2.ValueTypeRepresented) &&
                    Object.ReferenceEquals(value1.Module, value2.Module);
            }
            protected override BoxedValueType CreateValueFromKey(BoxedValuetypeHashtableKey key)
            {
                return new BoxedValueType(key.OwningModule, key.ValueType);
            }
        }
        private BoxedValuetypeHashtable _boxedValuetypeHashtable = new BoxedValuetypeHashtable();

        private struct UnboxingThunkHashtableKey
        {
            public readonly MethodDesc TargetMethod;
            public readonly BoxedValueType OwningType;

            public UnboxingThunkHashtableKey(MethodDesc targetMethod, BoxedValueType owningType)
            {
                TargetMethod = targetMethod;
                OwningType = owningType;
            }
        }

        private class UnboxingThunkHashtable : LockFreeReaderHashtable<UnboxingThunkHashtableKey, GenericUnboxingThunk>
        {
            protected override int GetKeyHashCode(UnboxingThunkHashtableKey key)
            {
                return key.TargetMethod.GetHashCode();
            }
            protected override int GetValueHashCode(GenericUnboxingThunk value)
            {
                return value.TargetMethod.GetHashCode();
            }
            protected override bool CompareKeyToValue(UnboxingThunkHashtableKey key, GenericUnboxingThunk value)
            {
                return Object.ReferenceEquals(key.TargetMethod, value.TargetMethod) &&
                    Object.ReferenceEquals(key.OwningType, value.OwningType);
            }
            protected override bool CompareValueToValue(GenericUnboxingThunk value1, GenericUnboxingThunk value2)
            {
                return Object.ReferenceEquals(value1.TargetMethod, value2.TargetMethod) &&
                    Object.ReferenceEquals(value1.OwningType, value2.OwningType);
            }
            protected override GenericUnboxingThunk CreateValueFromKey(UnboxingThunkHashtableKey key)
            {
                return new GenericUnboxingThunk(key.OwningType, key.TargetMethod);
            }
        }
        private UnboxingThunkHashtable _unboxingThunkHashtable = new UnboxingThunkHashtable();
        

        /// <summary>
        /// A type with an identical layout to the layout of a boxed value type.
        /// The type has a single field of the type of the valuetype it represents.
        /// </summary>
        private class BoxedValueType : MetadataType
        {
            private const string BoxedValueFieldName = "BoxedValue";

            public FieldDesc BoxedValue { get; }

            public MetadataType ValueTypeRepresented { get; }

            public override ModuleDesc Module { get; }

            public override string Name => "Boxed_" + ValueTypeRepresented.Name;

            public override string Namespace
            {
                get
                {
                    // Mangle the namespace in the hopes that it won't conflict with anything else.

                    StringBuilder sb = new StringBuilder();

                    ArrayBuilder<string> prefixes = new ArrayBuilder<string>();

                    DefType currentType = ValueTypeRepresented;
                    if (currentType.ContainingType != null)
                    {
                        while (currentType.ContainingType != null)
                        {
                            prefixes.Add(currentType.Name);
                            currentType = currentType.ContainingType;
                        }

                        prefixes.Add(currentType.Name);
                    }

                    sb.Append(((IAssemblyDesc)((MetadataType)currentType).Module).GetName().Name);
                    sb.Append('_');
                    sb.Append(currentType.Namespace);

                    for (int i = prefixes.Count - 1; i >= 0; i--)
                    {
                        sb.Append(prefixes[i]);

                        if (i > 0)
                            sb.Append('+');
                    }

                    return sb.ToString();
                }
            }

            public override Instantiation Instantiation => ValueTypeRepresented.Instantiation;
            public override PInvokeStringFormat PInvokeStringFormat => PInvokeStringFormat.AutoClass;
            public override bool IsExplicitLayout => false;
            public override bool IsSequentialLayout => true;
            public override bool IsBeforeFieldInit => false;
            public override MetadataType MetadataBaseType => (MetadataType)Context.GetWellKnownType(WellKnownType.Object);
            public override bool IsSealed => true;
            public override DefType ContainingType => null;
            public override DefType[] ExplicitlyImplementedInterfaces => Array.Empty<DefType>();
            public override TypeSystemContext Context => ValueTypeRepresented.Context;

            public BoxedValueType(ModuleDesc owningModule, MetadataType valuetype)
            {
                // BoxedValueType has the same genericness as the valuetype it's wrapping.
                // Making BoxedValueType wrap the genericness (and be itself nongeneric) would
                // require a crazy name mangling scheme to allow generating stable and unique names
                // for the wrappers.
                Debug.Assert(valuetype.IsTypeDefinition);

                Debug.Assert(valuetype.IsValueType);

                Module = owningModule;
                ValueTypeRepresented = valuetype;
                BoxedValue = new BoxedValueField(this);
            }

            public override ClassLayoutMetadata GetClassLayout() => default(ClassLayoutMetadata);
            public override bool HasCustomAttribute(string attributeNamespace, string attributeName) => false;
            public override IEnumerable<MetadataType> GetNestedTypes() => Array.Empty<MetadataType>();
            public override MetadataType GetNestedType(string name) => null;
            protected override MethodImplRecord[] ComputeVirtualMethodImplsForType() => Array.Empty<MethodImplRecord>();
            public override MethodImplRecord[] FindMethodsImplWithMatchingDeclName(string name) => Array.Empty<MethodImplRecord>();

            public override int GetHashCode()
            {
                string ns = Namespace;
                var hashCodeBuilder = new Internal.NativeFormat.TypeHashingAlgorithms.HashCodeBuilder(ns);
                if (ns.Length > 0)
                    hashCodeBuilder.Append(".");
                hashCodeBuilder.Append(Name);
                return hashCodeBuilder.ToHashCode();
            }

            protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
            {
                TypeFlags flags = 0;

                if ((mask & TypeFlags.ContainsGenericVariablesComputed) != 0)
                {
                    flags |= TypeFlags.ContainsGenericVariablesComputed;
                }

                if ((mask & TypeFlags.HasGenericVarianceComputed) != 0)
                {
                    flags |= TypeFlags.HasGenericVarianceComputed;
                }

                if ((mask & TypeFlags.CategoryMask) != 0)
                {
                    flags |= TypeFlags.Class;
                }

                return flags;
            }

            public override FieldDesc GetField(string name)
            {
                if (name == BoxedValueFieldName)
                    return BoxedValue;

                return null;
            }

            public override IEnumerable<FieldDesc> GetFields()
            {
                yield return BoxedValue;
            }

            /// <summary>
            /// Synthetic field on <see cref="BoxedValueType"/>.
            /// </summary>
            private class BoxedValueField : FieldDesc
            {
                private BoxedValueType _owningType;

                public override TypeSystemContext Context => _owningType.Context;
                public override TypeDesc FieldType => _owningType.ValueTypeRepresented.InstantiateAsOpen();
                public override bool HasRva => false;
                public override bool IsInitOnly => false;
                public override bool IsLiteral => false;
                public override bool IsStatic => false;
                public override bool IsThreadStatic => false;
                public override DefType OwningType => _owningType;
                public override bool HasCustomAttribute(string attributeNamespace, string attributeName) => false;
                public override string Name => BoxedValueFieldName;

                public BoxedValueField(BoxedValueType owningType)
                {
                    _owningType = owningType;
                }
            }
        }

        /// <summary>
        /// Represents a thunk to call shared instance method on boxed valuetypes.
        /// </summary>
        private class GenericUnboxingThunk : ILStubMethod
        {
            private MethodDesc _targetMethod;
            private ValueTypeInstanceMethodWithHiddenParameter _nakedTargetMethod;
            private BoxedValueType _owningType;

            public GenericUnboxingThunk(BoxedValueType owningType, MethodDesc targetMethod)
            {
                Debug.Assert(targetMethod.OwningType.IsValueType);
                Debug.Assert(!targetMethod.Signature.IsStatic);
                
                _owningType = owningType;
                _targetMethod = targetMethod;
                _nakedTargetMethod = new ValueTypeInstanceMethodWithHiddenParameter(targetMethod);
            }

            public override TypeSystemContext Context => _targetMethod.Context;

            public override TypeDesc OwningType => _owningType;

            public override MethodSignature Signature => _targetMethod.Signature;

            public MethodDesc TargetMethod => _targetMethod;

            public override string Name
            {
                get
                {
                    return _targetMethod.Name + "_Unbox";
                }
            }

            public override MethodIL EmitIL()
            {
                ILEmitter emit = new ILEmitter();
                ILCodeStream codeStream = emit.NewCodeStream();

                FieldDesc eeTypeField = Context.GetWellKnownType(WellKnownType.Object).GetKnownField("m_pEEType");
                FieldDesc boxedValueField = _owningType.BoxedValue.InstantiateAsOpen();

                // Load ByRef to the field with the value of the boxed valuetype
                codeStream.EmitLdArg(0);
                codeStream.Emit(ILOpcode.ldflda, emit.NewToken(boxedValueField));

                // Load the EEType of the boxed valuetype (this is the hidden generic context parameter expected
                // by the (canonical) instance method, but normally not part of the signature in IL).
                codeStream.EmitLdArg(0);
                codeStream.Emit(ILOpcode.ldfld, emit.NewToken(eeTypeField));

                // Load rest of the arguments
                for (int i = 0; i < _targetMethod.Signature.Length; i++)
                {
                    codeStream.EmitLdArg(i + 1);
                }

                // Call an instance method on the target valuetype that has a fake instantiation parameter
                // in it's signature. This will be swapped by the actual instance method after codegen is done.
                InstantiatedType targetType = (InstantiatedType)_nakedTargetMethod.OwningType.InstantiateAsOpen();
                MethodDesc targetMethod = Context.GetMethodForInstantiatedType(_nakedTargetMethod, targetType);

                codeStream.Emit(ILOpcode.call, emit.NewToken(targetMethod));
                codeStream.Emit(ILOpcode.ret);

                return emit.Link(this);
            }
        }

        /// <summary>
        /// Represents an instance method on a generic valuetype with an explicit instantiation parameter in the
        /// signature. This is so that we can refer to the parameter from IL. References to this method will
        /// be replaced by the actual instance method after codegen is done.
        /// </summary>
        internal class ValueTypeInstanceMethodWithHiddenParameter : MethodDesc
        {
            private MethodDesc _methodRepresented;
            private MethodSignature _signature;

            public ValueTypeInstanceMethodWithHiddenParameter(MethodDesc methodRepresented)
            {
                Debug.Assert(methodRepresented.OwningType.IsValueType);
                Debug.Assert(!methodRepresented.Signature.IsStatic);
                
                _methodRepresented = methodRepresented;
            }

            public MethodDesc MethodRepresented => _methodRepresented;

            // We really don't want this method to be inlined.
            public override bool IsNoInlining => true;

            public override TypeSystemContext Context => _methodRepresented.Context;
            public override TypeDesc OwningType => _methodRepresented.OwningType;

            public override string Name => _methodRepresented.Name;

            public override MethodSignature Signature
            {
                get
                {
                    if (_signature == null)
                    {
                        TypeDesc[] parameters = new TypeDesc[_methodRepresented.Signature.Length + 1];

                        // Shared instance methods on generic valuetypes have a hidden parameter with the generic context.
                        // We add it to the signature so that we can refer to it from IL.
                        parameters[0] = Context.GetWellKnownType(WellKnownType.Object).GetKnownField("m_pEEType").FieldType;
                        for (int i = 0; i < _methodRepresented.Signature.Length; i++)
                            parameters[i + 1] = _methodRepresented.Signature[i];

                        _signature = new MethodSignature(_methodRepresented.Signature.Flags,
                            _methodRepresented.Signature.GenericParameterCount,
                            _methodRepresented.Signature.ReturnType,
                            parameters);
                    }

                    return _signature;
                }
            }

            public override bool HasCustomAttribute(string attributeNamespace, string attributeName) => false;
        }
    }
}
