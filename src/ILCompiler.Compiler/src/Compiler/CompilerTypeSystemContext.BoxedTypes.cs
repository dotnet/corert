// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.IL;
using Internal.IL.Stubs;

using Debug = System.Diagnostics.Debug;
using System.Text;

namespace ILCompiler

{
    // Contains functionality related to pseudotypes representing boxed instances of value types
    partial class CompilerTypeSystemContext
    {
        public MethodDesc GetSpecialUnboxingThunk(MethodDesc targetMethod, ModuleDesc ownerModuleOfThunk)
        {
            TypeDesc owningType = targetMethod.OwningType;
            var owningTypeDefinition = (MetadataType)owningType.GetTypeDefinition();

            var typeKey = new BoxedValuetypeHashtableKey(owningTypeDefinition, ownerModuleOfThunk);
            BoxedValueType boxedTypeDefinition = _boxedValuetypeHashtable.GetOrCreateValue(typeKey);

            var targetMethodDefinition = targetMethod.GetTypicalMethodDefinition();
            var methodKey = new UnboxingThunkHashtableKey(targetMethodDefinition, boxedTypeDefinition);
            GenericUnboxingThunk thunkDefinition = _unboxingThunkHashtable.GetOrCreateValue(methodKey);

            Debug.Assert(owningType != owningTypeDefinition);
            InstantiatedType boxedType = boxedTypeDefinition.MakeInstantiatedType(owningType.Instantiation);

            MethodDesc thunk = GetMethodForInstantiatedType(thunkDefinition, boxedType);
            Debug.Assert(!thunk.HasInstantiation);

            return thunk;
        }

        public bool IsSpecialUnboxingThunkTargetMethod(MethodDesc method)
        {
            return method.GetTypicalMethodDefinition().GetType() == typeof(ValueTypeInstanceMethodWithHiddenParameter);
        }

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

                codeStream.EmitLdArg(0);
                codeStream.Emit(ILOpcode.ldflda, emit.NewToken(boxedValueField));

                codeStream.EmitLdArg(0);
                codeStream.Emit(ILOpcode.ldfld, emit.NewToken(eeTypeField));

                for (int i = 0; i < _targetMethod.Signature.Length; i++)
                {
                    codeStream.EmitLdArg(i + 1);
                }

                TypeDesc[] targetTypeInstantiation = new TypeDesc[OwningType.Instantiation.Length];
                for (int i = 0; i < OwningType.Instantiation.Length; i++)
                    targetTypeInstantiation[i] = Context.GetSignatureVariable(i, false);

                InstantiatedType targetType = Context.GetInstantiatedType((MetadataType)_nakedTargetMethod.OwningType, new Instantiation(targetTypeInstantiation));

                MethodDesc targetMethod = Context.GetMethodForInstantiatedType(_nakedTargetMethod, targetType);

                codeStream.Emit(ILOpcode.call, emit.NewToken(targetMethod));
                codeStream.Emit(ILOpcode.ret);

                return emit.Link(this);
            }
        }

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

                        parameters[0] = Context.GetWellKnownType(WellKnownType.Object).GetKnownField("m_pEEType").FieldType;
                        for (int i = 0; i < _methodRepresented.Signature.Length; i++)
                            parameters[i + 1] = _methodRepresented.Signature[0];

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
