﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public abstract partial class TypeSystemContext
    {
        public TypeSystemContext() : this(new TargetDetails(TargetArchitecture.Unknown, TargetOS.Unknown))
        {
        }

        public TypeSystemContext(TargetDetails target)
        {
            Target = target;

            _instantiatedTypes = new InstantiatedTypeKey.InstantiatedTypeKeyHashtable();

            _arrayTypes = new ArrayTypeKey.ArrayTypeKeyHashtable();

            _byRefTypes = new ByRefHashtable();

            _pointerTypes = new PointerHashtable();

            _instantiatedMethods = new InstantiatedMethodKey.InstantiatedMethodKeyHashtable();

            _methodForInstantiatedTypes = new MethodForInstantiatedTypeKey.MethodForInstantiatedTypeKeyHashtable();

            _fieldForInstantiatedTypes = new FieldForInstantiatedTypeKey.FieldForInstantiatedTypeKeyHashtable();

            _signatureVariables = new SignatureVariableHashtable(this);
        }

        public TargetDetails Target
        {
            get; private set;
        }

        public abstract MetadataType GetWellKnownType(WellKnownType wellKnownType);

        // TODO: Optional interface instead? Return ModuleDesc instead?
        public virtual Object ResolveAssembly(AssemblyName name)
        {
            return null;
        }

        public virtual bool IsWellKnownType(TypeDesc type, WellKnownType wellKnownType)
        {
            return type == GetWellKnownType(wellKnownType);
        }

        //
        // Array types
        //

        public TypeDesc GetArrayType(TypeDesc elementType)
        {
            return GetArrayType(elementType, -1);
        }

        //
        // MDArray types
        //

        struct ArrayTypeKey
        {
            TypeDesc _elementType;
            int _rank;

            public ArrayTypeKey(TypeDesc elementType, int rank)
            {
                _elementType = elementType;
                _rank = rank;
            }

            public TypeDesc ElementType
            {
                get
                {
                    return _elementType;
                }
            }

            public int Rank
            {
                get
                {
                    return _rank;
                }
            }

            public class ArrayTypeKeyHashtable : LockFreeReaderHashtable<ArrayTypeKey, ArrayType>
            {
                protected override int GetKeyHashCode(ArrayTypeKey key)
                {
                    return Internal.NativeFormat.TypeHashingAlgorithms.ComputeArrayTypeHashCode(key._elementType, key._rank);
                }

                protected override int GetValueHashCode(ArrayType value)
                {
                    return Internal.NativeFormat.TypeHashingAlgorithms.ComputeArrayTypeHashCode(value.ElementType, value.IsSzArray ? -1 : value.Rank);
                }

                protected override bool CompareKeyToValue(ArrayTypeKey key, ArrayType value)
                {
                    if (key._elementType != value.ElementType)
                        return false;

                    if ((key._rank == -1) && value.IsSzArray)
                        return true;

                    return key._rank == value.Rank;
                }

                protected override bool CompareValueToValue(ArrayType value1, ArrayType value2)
                {
                    return (value1.ElementType == value2.ElementType) && (value1.Rank == value2.Rank) && value1.IsSzArray == value2.IsSzArray;
                }

                protected override ArrayType CreateValueFromKey(ArrayTypeKey key)
                {
                    return new ArrayType(key.ElementType, key.Rank);
                }
            }
        }

        ArrayTypeKey.ArrayTypeKeyHashtable _arrayTypes;

        public TypeDesc GetArrayType(TypeDesc elementType, int rank)
        {
            return _arrayTypes.GetOrCreateValue(new ArrayTypeKey(elementType, rank));
        }

        //
        // ByRef types
        //
        public class ByRefHashtable : LockFreeReaderHashtable<TypeDesc, ByRefType>
        {
            protected override int GetKeyHashCode(TypeDesc key)
            {
                return key.GetHashCode();
            }

            protected override int GetValueHashCode(ByRefType value)
            {
                return value.ParameterType.GetHashCode();
            }

            protected override bool CompareKeyToValue(TypeDesc key, ByRefType value)
            {
                return key == value.ParameterType;
            }

            protected override bool CompareValueToValue(ByRefType value1, ByRefType value2)
            {
                return value1.ParameterType == value2.ParameterType;
            }

            protected override ByRefType CreateValueFromKey(TypeDesc key)
            {
                return new ByRefType(key);
            }
        }

        ByRefHashtable _byRefTypes;

        public TypeDesc GetByRefType(TypeDesc parameterType)
        {
            return _byRefTypes.GetOrCreateValue(parameterType);
        }

        //
        // Pointer types
        //
        public class PointerHashtable : LockFreeReaderHashtable<TypeDesc, PointerType>
        {
            protected override int GetKeyHashCode(TypeDesc key)
            {
                return key.GetHashCode();
            }

            protected override int GetValueHashCode(PointerType value)
            {
                return value.ParameterType.GetHashCode();
            }

            protected override bool CompareKeyToValue(TypeDesc key, PointerType value)
            {
                return key == value.ParameterType;
            }

            protected override bool CompareValueToValue(PointerType value1, PointerType value2)
            {
                return value1.ParameterType == value2.ParameterType;
            }

            protected override PointerType CreateValueFromKey(TypeDesc key)
            {
                return new PointerType(key);
            }
        }

        PointerHashtable _pointerTypes;

        public TypeDesc GetPointerType(TypeDesc parameterType)
        {
            return _pointerTypes.GetOrCreateValue(parameterType);
        }

        //
        // Instantiated types
        //

        struct InstantiatedTypeKey
        {
            TypeDesc _typeDef;
            Instantiation _instantiation;

            public InstantiatedTypeKey(TypeDesc typeDef, Instantiation instantiation)
            {
                _typeDef = typeDef;
                _instantiation = instantiation;
            }

            public TypeDesc TypeDef
            {
                get
                {
                    return _typeDef;
                }
            }

            public Instantiation Instantiation
            {
                get
                {
                    return _instantiation;
                }
            }

            public class InstantiatedTypeKeyHashtable : LockFreeReaderHashtable<InstantiatedTypeKey, InstantiatedType>
            {
                protected override int GetKeyHashCode(InstantiatedTypeKey key)
                {
                    return key._instantiation.ComputeGenericInstanceHashCode(key._typeDef.GetHashCode());
                }

                protected override int GetValueHashCode(InstantiatedType value)
                {
                    return value.Instantiation.ComputeGenericInstanceHashCode(value.GetTypeDefinition().GetHashCode());
                }

                protected override bool CompareKeyToValue(InstantiatedTypeKey key, InstantiatedType value)
                {
                    if (key._typeDef != value.GetTypeDefinition())
                        return false;

                    Instantiation valueInstantiation = value.Instantiation;

                    if (key._instantiation.Length != valueInstantiation.Length)
                        return false;

                    for (int i = 0; i < key._instantiation.Length; i++)
                    {
                        if (key._instantiation[i] != valueInstantiation[i])
                            return false;
                    }

                    return true;
                }

                protected override bool CompareValueToValue(InstantiatedType value1, InstantiatedType value2)
                {
                    if (value1.GetTypeDefinition() != value2.GetTypeDefinition())
                        return false;

                    Instantiation value1Instantiation = value1.Instantiation;
                    Instantiation value2Instantiation = value2.Instantiation;

                    if (value1Instantiation.Length != value2Instantiation.Length)
                        return false;

                    for (int i = 0; i < value1Instantiation.Length; i++)
                    {
                        if (value1Instantiation[i] != value2Instantiation[i])
                            return false;
                    }

                    return true;
                }

                protected override InstantiatedType CreateValueFromKey(InstantiatedTypeKey key)
                {
                    return new InstantiatedType((MetadataType)key.TypeDef, key.Instantiation);
                }
            }
        }

        InstantiatedTypeKey.InstantiatedTypeKeyHashtable _instantiatedTypes;

        public InstantiatedType GetInstantiatedType(MetadataType typeDef, Instantiation instantiation)
        {
            return _instantiatedTypes.GetOrCreateValue(new InstantiatedTypeKey(typeDef, instantiation));
        }

        //
        // Instantiated methods
        //

        struct InstantiatedMethodKey
        {
            MethodDesc _methodDef;
            Instantiation _instantiation;

            public InstantiatedMethodKey(MethodDesc methodDef, Instantiation instantiation)
            {
                _methodDef = methodDef;
                _instantiation = instantiation;
            }

            public MethodDesc MethodDef
            {
                get
                {
                    return _methodDef;
                }
            }

            public Instantiation Instantiation
            {
                get
                {
                    return _instantiation;
                }
            }

            public class InstantiatedMethodKeyHashtable : LockFreeReaderHashtable<InstantiatedMethodKey, InstantiatedMethod>
            {
                protected override int GetKeyHashCode(InstantiatedMethodKey key)
                {
                    return key._instantiation.ComputeGenericInstanceHashCode(key._methodDef.GetHashCode());
                }

                protected override int GetValueHashCode(InstantiatedMethod value)
                {
                    return value.Instantiation.ComputeGenericInstanceHashCode(value.GetMethodDefinition().GetHashCode());
                }

                protected override bool CompareKeyToValue(InstantiatedMethodKey key, InstantiatedMethod value)
                {
                    if (key._methodDef != value.GetMethodDefinition())
                        return false;

                    Instantiation valueInstantiation = value.Instantiation;

                    if (key._instantiation.Length != valueInstantiation.Length)
                        return false;

                    for (int i = 0; i < key._instantiation.Length; i++)
                    {
                        if (key._instantiation[i] != valueInstantiation[i])
                            return false;
                    }

                    return true;
                }

                protected override bool CompareValueToValue(InstantiatedMethod value1, InstantiatedMethod value2)
                {
                    if (value1.GetMethodDefinition() != value2.GetMethodDefinition())
                        return false;

                    Instantiation value1Instantiation = value1.Instantiation;
                    Instantiation value2Instantiation = value2.Instantiation;

                    if (value1Instantiation.Length != value2Instantiation.Length)
                        return false;

                    for (int i = 0; i < value1Instantiation.Length; i++)
                    {
                        if (value1Instantiation[i] != value2Instantiation[i])
                            return false;
                    }

                    return true;
                }

                protected override InstantiatedMethod CreateValueFromKey(InstantiatedMethodKey key)
                {
                    return new InstantiatedMethod(key.MethodDef, key.Instantiation);
                }
            }
        }

        InstantiatedMethodKey.InstantiatedMethodKeyHashtable _instantiatedMethods;

        public InstantiatedMethod GetInstantiatedMethod(MethodDesc methodDef, Instantiation instantiation)
        {
            Debug.Assert(!(methodDef is InstantiatedMethod));
            return _instantiatedMethods.GetOrCreateValue(new InstantiatedMethodKey(methodDef, instantiation));
        }

        //
        // Methods for instantiated type
        //

        struct MethodForInstantiatedTypeKey
        {
            MethodDesc _typicalMethodDef;
            InstantiatedType _instantiatedType;

            public MethodForInstantiatedTypeKey(MethodDesc typicalMethodDef, InstantiatedType instantiatedType)
            {
                _typicalMethodDef = typicalMethodDef;
                _instantiatedType = instantiatedType;
            }

            public MethodDesc TypicalMethodDef
            {
                get
                {
                    return _typicalMethodDef;
                }
            }

            public InstantiatedType InstantiatedType
            {
                get
                {
                    return _instantiatedType;
                }
            }

            public class MethodForInstantiatedTypeKeyHashtable : LockFreeReaderHashtable<MethodForInstantiatedTypeKey, MethodForInstantiatedType>
            {
                protected override int GetKeyHashCode(MethodForInstantiatedTypeKey key)
                {
                    return key._typicalMethodDef.GetHashCode() ^ key._instantiatedType.GetHashCode();
                }

                protected override int GetValueHashCode(MethodForInstantiatedType value)
                {
                    return value.GetTypicalMethodDefinition().GetHashCode() ^ value.OwningType.GetHashCode();
                }

                protected override bool CompareKeyToValue(MethodForInstantiatedTypeKey key, MethodForInstantiatedType value)
                {
                    if (key._typicalMethodDef != value.GetTypicalMethodDefinition())
                        return false;

                    return key._instantiatedType == value.OwningType;
                }

                protected override bool CompareValueToValue(MethodForInstantiatedType value1, MethodForInstantiatedType value2)
                {
                    return (value1.GetTypicalMethodDefinition() == value2.GetTypicalMethodDefinition()) && (value1.OwningType == value2.OwningType);
                }

                protected override MethodForInstantiatedType CreateValueFromKey(MethodForInstantiatedTypeKey key)
                {
                    return new MethodForInstantiatedType(key.TypicalMethodDef, key.InstantiatedType);
                }
            }
        }

        MethodForInstantiatedTypeKey.MethodForInstantiatedTypeKeyHashtable _methodForInstantiatedTypes;

        public MethodDesc GetMethodForInstantiatedType(MethodDesc typicalMethodDef, InstantiatedType instantiatedType)
        {
            Debug.Assert(!(typicalMethodDef is MethodForInstantiatedType));
            Debug.Assert(!(typicalMethodDef is InstantiatedMethod));

            return _methodForInstantiatedTypes.GetOrCreateValue(new MethodForInstantiatedTypeKey(typicalMethodDef, instantiatedType));
        }

        //
        // Fields for instantiated type
        //

        struct FieldForInstantiatedTypeKey
        {
            FieldDesc _fieldDef;
            InstantiatedType _instantiatedType;

            public FieldForInstantiatedTypeKey(FieldDesc fieldDef, InstantiatedType instantiatedType)
            {
                _fieldDef = fieldDef;
                _instantiatedType = instantiatedType;
            }

            public FieldDesc TypicalFieldDef
            {
                get
                {
                    return _fieldDef;
                }
            }

            public InstantiatedType InstantiatedType
            {
                get
                {
                    return _instantiatedType;
                }
            }

            public class FieldForInstantiatedTypeKeyHashtable : LockFreeReaderHashtable<FieldForInstantiatedTypeKey, FieldForInstantiatedType>
            {
                protected override int GetKeyHashCode(FieldForInstantiatedTypeKey key)
                {
                    return key._fieldDef.GetHashCode() ^ key._instantiatedType.GetHashCode();
                }

                protected override int GetValueHashCode(FieldForInstantiatedType value)
                {
                    return value.GetTypicalFieldDefinition().GetHashCode() ^ value.OwningType.GetHashCode();
                }

                protected override bool CompareKeyToValue(FieldForInstantiatedTypeKey key, FieldForInstantiatedType value)
                {
                    if (key._fieldDef != value.GetTypicalFieldDefinition())
                        return false;

                    return key._instantiatedType == value.OwningType;
                }

                protected override bool CompareValueToValue(FieldForInstantiatedType value1, FieldForInstantiatedType value2)
                {
                    return (value1.GetTypicalFieldDefinition() == value2.GetTypicalFieldDefinition()) && (value1.OwningType == value2.OwningType);
                }

                protected override FieldForInstantiatedType CreateValueFromKey(FieldForInstantiatedTypeKey key)
                {
                    return new FieldForInstantiatedType(key.TypicalFieldDef, key.InstantiatedType);
                }
            }
        }

        FieldForInstantiatedTypeKey.FieldForInstantiatedTypeKeyHashtable _fieldForInstantiatedTypes;

        public FieldDesc GetFieldForInstantiatedType(FieldDesc fieldDef, InstantiatedType instantiatedType)
        {
            return _fieldForInstantiatedTypes.GetOrCreateValue(new FieldForInstantiatedTypeKey(fieldDef, instantiatedType));
        }

        //
        // Signature variables
        //
        private class SignatureVariableHashtable : LockFreeReaderHashtable<uint, SignatureVariable>
        {
            TypeSystemContext _context;
            public SignatureVariableHashtable(TypeSystemContext context)
            {
                _context = context;
            }

            protected override int GetKeyHashCode(uint key)
            {
                return (int)key;
            }

            protected override int GetValueHashCode(SignatureVariable value)
            {
                uint combinedIndex = value.IsMethodSignatureVariable ? ((uint)value.Index | 0x80000000) : (uint)value.Index;
                return (int)combinedIndex;
            }

            protected override bool CompareKeyToValue(uint key, SignatureVariable value)
            {
                uint combinedIndex = value.IsMethodSignatureVariable ? ((uint)value.Index | 0x80000000) : (uint)value.Index;
                return key == combinedIndex;
            }

            protected override bool CompareValueToValue(SignatureVariable value1, SignatureVariable value2)
            {
                uint combinedIndex1 = value1.IsMethodSignatureVariable ? ((uint)value1.Index | 0x80000000) : (uint)value1.Index;
                uint combinedIndex2 = value2.IsMethodSignatureVariable ? ((uint)value2.Index | 0x80000000) : (uint)value2.Index;

                return combinedIndex1 == combinedIndex2;
            }

            protected override SignatureVariable CreateValueFromKey(uint key)
            {
                bool method = ((key & 0x80000000) != 0);
                int index = (int)(key & 0x7FFFFFFF);
                if (method)
                    return new SignatureMethodVariable(_context, index);
                else
                    return new SignatureTypeVariable(_context, index);
            }
        }

        SignatureVariableHashtable _signatureVariables;

        public TypeDesc GetSignatureVariable(int index, bool method)
        {
            if (index < 0)
                throw new BadImageFormatException();

            uint combinedIndex = method ? ((uint)index | 0x80000000) : (uint)index;
            return _signatureVariables.GetOrCreateValue(combinedIndex);
        }

        /// <summary>
        /// Abstraction to allow the type system context to affect the field layout
        /// algorithm used by types to lay themselves out.
        /// </summary>
        public abstract FieldLayoutAlgorithm GetLayoutAlgorithmForType(DefType type);

        /// <summary>
        /// Abstraction to allow the type system context to control the interfaces
        /// algorithm used by types.
        /// </summary>
        public RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForType(TypeDesc type)
        {
            if (type is MetadataType)
            {
                return GetRuntimeInterfacesAlgorithmForMetadataType((MetadataType)type);
            }
            else if (type is ArrayType)
            {
                ArrayType arrType = (ArrayType)type;
                if (arrType.IsSzArray && !arrType.ElementType.IsPointer)
                {
                    return GetRuntimeInterfacesAlgorithmForNonPointerArrayType((ArrayType)type);
                }
                else
                {
                    return BaseTypeRuntimeInterfacesAlgorithm.Instance;
                }
            }

            return null;
        }

        /// <summary>
        /// Abstraction to allow the type system context to control the interfaces
        /// algorithm used by metadata types.
        /// </summary>
        public abstract RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForMetadataType(MetadataType type);

        /// <summary>
        /// Abstraction to allow the type system context to control the interfaces
        /// algorithm used by single dimensional array types.
        /// </summary>
        public abstract RuntimeInterfacesAlgorithm GetRuntimeInterfacesAlgorithmForNonPointerArrayType(ArrayType type);
    }
}
