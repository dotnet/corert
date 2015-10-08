// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public abstract class TypeSystemContext
    {
        public TypeSystemContext()
        {
            Target = new TargetDetails(TargetArchitecture.Unknown);
        }

        public TypeSystemContext(TargetDetails target)
        {
            Target = target;
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

        ImmutableDictionary<TypeDesc, ArrayType> _arrayTypes = ImmutableDictionary<TypeDesc, ArrayType>.Empty;

        public TypeDesc GetArrayType(TypeDesc elementType)
        {
            ArrayType existingArrayType;
            if (_arrayTypes.TryGetValue(elementType, out existingArrayType))
                return existingArrayType;

            return CreateArrayType(elementType);
        }

        TypeDesc CreateArrayType(TypeDesc elementType)
        {
            ArrayType arrayType = new ArrayType(elementType, -1);

            lock (this)
            {
                ArrayType existingArrayType;
                if (_arrayTypes.TryGetValue(elementType, out existingArrayType))
                    return existingArrayType;
                _arrayTypes = _arrayTypes.Add(elementType, arrayType);
            }

            return arrayType;
        }

        //
        // MDArray types
        //

        struct ArrayTypeKey : IEquatable<ArrayTypeKey>
        {
            TypeDesc _elementType;
            int _rank;

            public ArrayTypeKey(TypeDesc elementType, int rank)
            {
                _elementType = elementType;
                _rank = rank;
            }

            public override int GetHashCode()
            {
                return Internal.NativeFormat.TypeHashingAlgorithms.ComputeArrayTypeHashCode(_elementType, _rank);
            }

            public bool Equals(ArrayTypeKey other)
            {
                if (_elementType != other._elementType)
                    return false;

                if (_rank != other._rank)
                    return false;

                return true;
            }
        }

        ImmutableDictionary<ArrayTypeKey, ArrayType> _ArrayTypes = ImmutableDictionary<ArrayTypeKey, ArrayType>.Empty;

        public TypeDesc GetArrayType(TypeDesc elementType, int rank)
        {
            ArrayType existingArrayType;
            if (_ArrayTypes.TryGetValue(new ArrayTypeKey(elementType, rank), out existingArrayType))
                return existingArrayType;

            return CreateArrayType(elementType, rank);
        }

        TypeDesc CreateArrayType(TypeDesc elementType, int rank)
        {
            ArrayType arrayType = new ArrayType(elementType, rank);

            lock (this)
            {
                ArrayType existingArrayType;
                if (_ArrayTypes.TryGetValue(new ArrayTypeKey(elementType, rank), out existingArrayType))
                    return existingArrayType;
                _ArrayTypes = _ArrayTypes.Add(new ArrayTypeKey(elementType, rank), arrayType);
            }

            return arrayType;
        }

        //
        // ByRef types
        //

        ImmutableDictionary<TypeDesc, ByRefType> _byRefTypes = ImmutableDictionary<TypeDesc, ByRefType>.Empty;

        public TypeDesc GetByRefType(TypeDesc parameterType)
        {
            ByRefType existingByRefType;
            if (_byRefTypes.TryGetValue(parameterType, out existingByRefType))
                return existingByRefType;

            return CreateByRefType(parameterType);
        }

        TypeDesc CreateByRefType(TypeDesc parameterType)
        {
            ByRefType byRefType = new ByRefType(parameterType);
            
            lock (this)
            {
                ByRefType existingByRefType;
                if (_byRefTypes.TryGetValue(parameterType, out existingByRefType))
                    return existingByRefType;
                _byRefTypes = _byRefTypes.Add(parameterType, byRefType);
            }

            return byRefType;
        }

        //
        // Pointer types
        //

        ImmutableDictionary<TypeDesc, PointerType> _pointerTypes = ImmutableDictionary<TypeDesc, PointerType>.Empty;

        public TypeDesc GetPointerType(TypeDesc parameterType)
        {
            PointerType existingPointerType;
            if (_pointerTypes.TryGetValue(parameterType, out existingPointerType))
                return existingPointerType;

            return CreatePointerType(parameterType);
        }

        TypeDesc CreatePointerType(TypeDesc parameterType)
        {
            PointerType pointerType = new PointerType(parameterType);
            
            lock (this)
            {
                PointerType existingPointerType;
                if (_pointerTypes.TryGetValue(parameterType, out existingPointerType))
                    return existingPointerType;
                _pointerTypes = _pointerTypes.Add(parameterType, pointerType);
            }

            return pointerType;
        }

        //
        // Instantiated types
        //

        struct InstantiatedTypeKey : IEquatable<InstantiatedTypeKey>
        {
            TypeDesc _typeDef;
            Instantiation _instantiation;

            public InstantiatedTypeKey(TypeDesc typeDef, Instantiation instantiation)
            {
                _typeDef = typeDef;
                _instantiation = instantiation;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int _rotl(int value, int shift)
            {
                return (int)(((uint)value << shift) | ((uint)value >> (32 - shift)));
            }

            public override int GetHashCode()
            {
                return Internal.NativeFormat.TypeHashingAlgorithms.ComputeGenericInstanceHashCode(_typeDef.GetHashCode(), _instantiation);
            }

            public bool Equals(InstantiatedTypeKey other)
            {
                if (_typeDef != other._typeDef)
                    return false;

                if (_instantiation.Length != other._instantiation.Length)
                    return false;

                for (int i = 0; i < _instantiation.Length; i++)
                {
                    if (_instantiation[i] != other._instantiation[i])
                        return false;
                }

                return true;
            }
        }

        ImmutableDictionary<InstantiatedTypeKey, InstantiatedType> _instantiatedTypes = ImmutableDictionary<InstantiatedTypeKey, InstantiatedType>.Empty;

        public InstantiatedType GetInstantiatedType(MetadataType typeDef, Instantiation instantiation)
        {
            InstantiatedType existingInstantiatedType;
            if (_instantiatedTypes.TryGetValue(new InstantiatedTypeKey(typeDef, instantiation), out existingInstantiatedType))
                return existingInstantiatedType;

            return CreateInstantiatedType(typeDef, instantiation);
        }

        InstantiatedType CreateInstantiatedType(MetadataType typeDef, Instantiation instantiation)
        {
            InstantiatedType instantiatedType = new InstantiatedType(typeDef, instantiation);

            lock (this)
            {
                InstantiatedType existingInstantiatedType;
                if (_instantiatedTypes.TryGetValue(new InstantiatedTypeKey(typeDef, instantiation), out existingInstantiatedType))
                    return existingInstantiatedType;
                _instantiatedTypes = _instantiatedTypes.Add(new InstantiatedTypeKey(typeDef, instantiation), instantiatedType);
            }

            return instantiatedType;
        }

        //
        // Instantiated methods
        //

        struct InstantiatedMethodKey : IEquatable<InstantiatedMethodKey>
        {
            MethodDesc _methodDef;
            Instantiation _instantiation;

            public InstantiatedMethodKey(MethodDesc methodDef, Instantiation instantiation)
            {
                _methodDef = methodDef;
                _instantiation = instantiation;
            }

            public override int GetHashCode()
            {
                return Internal.NativeFormat.TypeHashingAlgorithms.ComputeGenericInstanceHashCode(_methodDef.GetHashCode(), _instantiation);
            }

            public bool Equals(InstantiatedMethodKey other)
            {
                if (_methodDef != other._methodDef)
                    return false;

                if (_instantiation.Length != other._instantiation.Length)
                    return false;

                for (int i = 0; i < _instantiation.Length; i++)
                {
                    if (_instantiation[i] != other._instantiation[i])
                        return false;
                }

                return true;
            }
        }

        ImmutableDictionary<InstantiatedMethodKey, InstantiatedMethod> _instantiatedMethods = ImmutableDictionary<InstantiatedMethodKey, InstantiatedMethod>.Empty;

        public InstantiatedMethod GetInstantiatedMethod(MethodDesc methodDef, Instantiation instantiation)
        {
            Debug.Assert(!(methodDef is InstantiatedMethod));

            InstantiatedMethod existingInstantiatedMethod;
            if (_instantiatedMethods.TryGetValue(new InstantiatedMethodKey(methodDef, instantiation), out existingInstantiatedMethod))
                return existingInstantiatedMethod;

            return CreateInstantiatedMethod(methodDef, instantiation);
        }

        InstantiatedMethod CreateInstantiatedMethod(MethodDesc methodDef, Instantiation instantiation)
        {
            InstantiatedMethod instantiatedMethod = new InstantiatedMethod(methodDef, instantiation);

            lock (this)
            {
                InstantiatedMethod existingInstantiatedMethod;
                if (_instantiatedMethods.TryGetValue(new InstantiatedMethodKey(methodDef, instantiation), out existingInstantiatedMethod))
                    return existingInstantiatedMethod;
                _instantiatedMethods = _instantiatedMethods.Add(new InstantiatedMethodKey(methodDef, instantiation), instantiatedMethod);
            }

            return instantiatedMethod;
        }

        //
        // Methods for instantiated type
        //

        struct MethodForInstantiatedTypeKey : IEquatable<MethodForInstantiatedTypeKey>
        {
            MethodDesc _typicalMethodDef;
            InstantiatedType _instantiatedType;

            public MethodForInstantiatedTypeKey(MethodDesc typicalMethodDef, InstantiatedType instantiatedType)
            {
                _typicalMethodDef = typicalMethodDef;
                _instantiatedType = instantiatedType;
            }

            public override int GetHashCode()
            {
                return _typicalMethodDef.GetHashCode() ^ _instantiatedType.GetHashCode();
            }

            public bool Equals(MethodForInstantiatedTypeKey other)
            {
                if (_typicalMethodDef != other._typicalMethodDef)
                    return false;

                if (_instantiatedType != other._instantiatedType)
                    return false;

                return true;
            }
        }

        ImmutableDictionary<MethodForInstantiatedTypeKey, MethodForInstantiatedType> _methodForInstantiatedTypes = ImmutableDictionary<MethodForInstantiatedTypeKey, MethodForInstantiatedType>.Empty;

        public MethodDesc GetMethodForInstantiatedType(MethodDesc typicalMethodDef, InstantiatedType instantiatedType)
        {
            Debug.Assert(!(typicalMethodDef is MethodForInstantiatedType));
            Debug.Assert(!(typicalMethodDef is InstantiatedMethod));

            MethodForInstantiatedType existingMethodForInstantiatedType;
            if (_methodForInstantiatedTypes.TryGetValue(new MethodForInstantiatedTypeKey(typicalMethodDef, instantiatedType), out existingMethodForInstantiatedType))
                return existingMethodForInstantiatedType;

            return CreateMethodForInstantiatedType(typicalMethodDef, instantiatedType);
        }

        MethodDesc CreateMethodForInstantiatedType(MethodDesc typicalMethodDef, InstantiatedType instantiatedType)
        {
            MethodForInstantiatedType methodForInstantiatedType = new MethodForInstantiatedType(typicalMethodDef, instantiatedType);

            lock (this)
            {
                MethodForInstantiatedType existingMethodForInstantiatedType;
                if (_methodForInstantiatedTypes.TryGetValue(new MethodForInstantiatedTypeKey(typicalMethodDef, instantiatedType), out existingMethodForInstantiatedType))
                    return existingMethodForInstantiatedType;
                _methodForInstantiatedTypes = _methodForInstantiatedTypes.Add(new MethodForInstantiatedTypeKey(typicalMethodDef, instantiatedType), methodForInstantiatedType);
            }

            return methodForInstantiatedType;
        }

        //
        // Fields for instantiated type
        //

        struct FieldForInstantiatedTypeKey : IEquatable<FieldForInstantiatedTypeKey>
        {
            FieldDesc _fieldDef;
            InstantiatedType _instantiatedType;

            public FieldForInstantiatedTypeKey(FieldDesc fieldDef, InstantiatedType instantiatedType)
            {
                _fieldDef = fieldDef;
                _instantiatedType = instantiatedType;
            }

            public override int GetHashCode()
            {
                return _fieldDef.GetHashCode() ^ _instantiatedType.GetHashCode();
            }

            public bool Equals(FieldForInstantiatedTypeKey other)
            {
                if (_fieldDef != other._fieldDef)
                    return false;

                if (_instantiatedType != other._instantiatedType)
                    return false;

                return true;
            }
        }

        ImmutableDictionary<FieldForInstantiatedTypeKey, FieldForInstantiatedType> _fieldForInstantiatedTypes = ImmutableDictionary<FieldForInstantiatedTypeKey, FieldForInstantiatedType>.Empty;

        public FieldDesc GetFieldForInstantiatedType(FieldDesc fieldDef, InstantiatedType instantiatedType)
        {
            FieldForInstantiatedType existingFieldForInstantiatedType;
            if (_fieldForInstantiatedTypes.TryGetValue(new FieldForInstantiatedTypeKey(fieldDef, instantiatedType), out existingFieldForInstantiatedType))
                return existingFieldForInstantiatedType;

            return CreateFieldForInstantiatedType(fieldDef, instantiatedType);
        }

        FieldDesc CreateFieldForInstantiatedType(FieldDesc fieldDef, InstantiatedType instantiatedType)
        {
            FieldForInstantiatedType fieldForInstantiatedType = new FieldForInstantiatedType(fieldDef, instantiatedType);

            lock (this)
            {
                FieldForInstantiatedType existingFieldForInstantiatedType;
                if (_fieldForInstantiatedTypes.TryGetValue(new FieldForInstantiatedTypeKey(fieldDef, instantiatedType), out existingFieldForInstantiatedType))
                    return existingFieldForInstantiatedType;
                _fieldForInstantiatedTypes = _fieldForInstantiatedTypes.Add(new FieldForInstantiatedTypeKey(fieldDef, instantiatedType), fieldForInstantiatedType);
            }

            return fieldForInstantiatedType;
        }

        //
        // Signature variables
        //
        ImmutableDictionary<uint, TypeDesc> _signatureVariables = ImmutableDictionary<uint, TypeDesc>.Empty;

        public TypeDesc GetSignatureVariable(int index, bool method)
        {
            if (index < 0)
                throw new BadImageFormatException();

            uint combinedIndex = method ? ((uint)index | 0x80000000) : (uint)index;

            TypeDesc existingSignatureVariable;
            if (_signatureVariables.TryGetValue(combinedIndex, out existingSignatureVariable))
                return existingSignatureVariable;

            return CreateSignatureVariable(index, method);
        }

        TypeDesc CreateSignatureVariable(int index, bool method)
        {
            TypeDesc signatureVariable;
            
            if (method)
                signatureVariable = new SignatureMethodVariable(this, index);
            else
                signatureVariable = new SignatureTypeVariable(this, index);

            uint combinedIndex = method ? ((uint)index | 0x80000000) : (uint)index;
            
            lock (this)
            {
                TypeDesc existingSignatureVariable;
                if (_signatureVariables.TryGetValue(combinedIndex, out existingSignatureVariable))
                    return existingSignatureVariable;
                _signatureVariables = _signatureVariables.Add(combinedIndex, signatureVariable);
            }

            return signatureVariable;
        }
    }
}
