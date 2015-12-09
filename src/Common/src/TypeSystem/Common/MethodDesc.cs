﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Internal.NativeFormat;

namespace Internal.TypeSystem
{
    [Flags]
    public enum MethodSignatureFlags
    {
        None = 0x0000,
        Static = 0x0001,
        // TODO: Generic, etc.
    }

    public sealed class MethodSignature
    {
        internal MethodSignatureFlags _flags;
        internal int _genericParameterCount;
        internal TypeDesc _returnType;
        internal TypeDesc[] _parameters;

        public MethodSignature(MethodSignatureFlags flags, int genericParameterCount, TypeDesc returnType, TypeDesc[] parameters)
        {
            _flags = flags;
            _genericParameterCount = genericParameterCount;
            _returnType = returnType;
            _parameters = parameters;

            Debug.Assert(parameters != null, "Parameters must not be null");
        }

        public MethodSignatureFlags Flags
        {
            get
            {
                return _flags;
            }
        }

        public bool IsStatic
        {
            get
            {
                return (_flags & MethodSignatureFlags.Static) != 0;
            }
        }

        public int GenericParameterCount
        {
            get
            {
                return _genericParameterCount;
            }
        }

        public TypeDesc ReturnType
        {
            get
            {
                return _returnType;
            }
        }

        [System.Runtime.CompilerServices.IndexerName("Parameter")]
        public TypeDesc this[int index]
        {
            get
            {
                return _parameters[index];
            }
        }

        public int Length
        {
            get
            {
                return _parameters.Length;
            }
        }

        public bool Equals(MethodSignature otherSignature)
        {
            // TODO: Generics, etc.
            if (this._flags != otherSignature._flags)
                return false;

            if (this._genericParameterCount != otherSignature._genericParameterCount)
                return false;

            if (this._returnType != otherSignature._returnType)
                return false;

            if (this._parameters.Length != otherSignature._parameters.Length)
                return false;

            for (int i = 0; i < this._parameters.Length; i++)
            {
                if (this._parameters[i] != otherSignature._parameters[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Adapt current signature to the instantation contexts.
        /// </summary>
        /// <param name="typeInstantiation">Context used for adapting the signature if it involves generic type parameter.</param>
        /// <param name="methodInstantiation">Context used for adapting the signature if it involves generic method type parameter.</param>
        /// <returns></returns>
        public MethodSignature InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            // Instantiate parameters first. If instantiation has no effects, avoid the allocation of a new array.
            TypeDesc[] parameters = null;
            for (int i = 0; i < _parameters.Length; i++)
            {
                TypeDesc oldType = _parameters[i];
                TypeDesc newType = oldType.InstantiateSignature(typeInstantiation, methodInstantiation);
                if (newType != oldType)
                {
                    if (parameters == null)
                    {
                        // Copy all entries of_parameters to parameters.
                        parameters = new TypeDesc[_parameters.Length];
                        Array.Copy(_parameters, 0, parameters, 0, _parameters.Length);
                    }
                    parameters[i] = newType;
                }
            }
            return new MethodSignature(_flags, _genericParameterCount, _returnType.InstantiateSignature(typeInstantiation, methodInstantiation), parameters??_parameters);
        }
    }

    public struct MethodSignatureBuilder
    {
        private MethodSignature _template;
        private MethodSignatureFlags _flags;
        private int _genericParameterCount;
        private TypeDesc _returnType;
        private TypeDesc[] _parameters;

        public MethodSignatureBuilder(MethodSignature template)
        {
            _template = template;

            _flags = template._flags;
            _genericParameterCount = template._genericParameterCount;
            _returnType = template._returnType;
            _parameters = template._parameters;
        }

        public MethodSignatureFlags Flags
        {
            set
            {
                _flags = value;
            }
        }

        public TypeDesc ReturnType
        {
            set
            {
                _returnType = value;
            }
        }

        [System.Runtime.CompilerServices.IndexerName("Parameter")]
        public TypeDesc this[int index]
        {
            set
            {
                if (_parameters[index] == value)
                    return;

                if (_template != null && _parameters == _template._parameters)
                {
                    TypeDesc[] parameters = new TypeDesc[_parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                        parameters[i] = _parameters[i];
                    _parameters = parameters;
                }
                _parameters[index] = value;
            }
        }

        public int Length
        {
            set
            {
                _parameters = new TypeDesc[value];
                _template = null;
            }
        }

        public MethodSignature ToSignature()
        {
            if (_template == null ||
                _flags != _template._flags ||
                _genericParameterCount != _template._genericParameterCount ||
                _returnType != _template._returnType ||
                _parameters != _template._parameters)
            {
                _template = new MethodSignature(_flags, _genericParameterCount, _returnType, _parameters);
            }

            return _template;
        }
    }

    public abstract partial class MethodDesc
    {
        public readonly static MethodDesc[] EmptyMethods = new MethodDesc[0];

        private int _hashcode;

        /// <summary>
        /// Use to allow objects to have their hashcode computed
        /// independently of the allocation of a MethodDesc object
        /// For instance, compute the hashcode when looking up the object,
        /// then when creating the object, pass in the hashcode directly.
        /// The hashcode specified MUST exactly match the algorithm implemented
        /// on this type normally.
        /// </summary>
        public void SetHashCode(int hashcode)
        {
            _hashcode = hashcode;
            Debug.Assert(hashcode == ComputeHashCode());
        }

        public sealed override int GetHashCode()
        {
            if (_hashcode != 0)
                return _hashcode;

            return AcquireHashCode();
        }

        private int AcquireHashCode()
        {
            _hashcode = ComputeHashCode();
            return _hashcode;
        }

        /// <summary>
        /// Compute HashCode. Should only be overriden by a MethodDesc that represents an instantiated method.
        /// </summary>
        protected virtual int ComputeHashCode()
        {
            return TypeHashingAlgorithms.ComputeMethodHashcode(OwningType.GetHashCode(), TypeHashingAlgorithms.ComputeNameHashCode(Name));
        }

        public override bool Equals(Object o)
        {
            return Object.ReferenceEquals(this, o);
        }

        public abstract TypeSystemContext Context
        {
            get;
        }

        public abstract TypeDesc OwningType
        {
            get;
        }

        public abstract MethodSignature Signature
        {
            get;
        }

        public virtual Instantiation Instantiation
        {
            get
            {
                return Instantiation.Empty;
            }
        }

        public bool HasInstantiation
        {
            get
            {
                return this.Instantiation.Length != 0;
            }
        }

        public bool ContainsGenericVariables
        {
            get
            {
                // TODO: Cache?

                Instantiation instantiation = this.Instantiation;
                for (int i = 0; i < instantiation.Length; i++)
                {
                    if (instantiation[i].ContainsGenericVariables)
                        return true;
                }
                return false;
            }
        }

        public bool IsConstructor
        {
            get
            {
                // TODO: Precise check
                // TODO: Cache?
                return this.Name == ".ctor";
            }
        }

        public bool IsStaticConstructor
        {
            get
            {
                return this == this.OwningType.GetStaticConstructor();
            }
        }

        public virtual string Name
        {
            get
            {
                return null;
            }
        }

        public virtual bool IsVirtual
        {
            get
            {
                return false;
            }
        }

        public virtual bool IsNewSlot
        {
            get
            {
                return false;
            }
        }

        public virtual bool IsAbstract
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating that this method cannot be overriden.
        /// </summary>
        public virtual bool IsFinal
        {
            get
            {
                return false;
            }
        }

        public abstract bool HasCustomAttribute(string attributeNamespace, string attributeName);

        // Strips method instantiation. E.g C<int>.m<string> -> C<int>.m<U>
        public virtual MethodDesc GetMethodDefinition()
        {
            return this;
        }

        public bool IsMethodDefinition
        {
            get
            {
                return GetMethodDefinition() == this;
            }
        }

        // Strips both type and method instantiation. E.g C<int>.m<string> -> C<T>.m<U>
        public virtual MethodDesc GetTypicalMethodDefinition()
        {
            return this;
        }

        public bool IsTypicalMethodDefinition
        {
            get
            {
                return GetTypicalMethodDefinition() == this;
            }
        }

        public virtual MethodDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            MethodDesc method = this;

            TypeDesc owningType = method.OwningType;
            TypeDesc instantiatedOwningType = owningType.InstantiateSignature(typeInstantiation, methodInstantiation);
            if (owningType != instantiatedOwningType)
                method = instantiatedOwningType.Context.GetMethodForInstantiatedType(method.GetTypicalMethodDefinition(), (InstantiatedType)instantiatedOwningType);

            Instantiation instantiation = method.Instantiation;
            TypeDesc[] clone = null;

            for (int i = 0; i < instantiation.Length; i++)
            {
                TypeDesc uninst = instantiation[i];
                TypeDesc inst = uninst.InstantiateSignature(typeInstantiation, methodInstantiation);
                if (inst != uninst)
                {
                    if (clone == null)
                    {
                        clone = new TypeDesc[instantiation.Length];
                        for (int j = 0; j < clone.Length; j++)
                        {
                            clone[j] = instantiation[j];
                        }
                    }
                    clone[i] = inst;
                }
            }

            return (clone == null) ? method : method.Context.GetInstantiatedMethod(method.GetMethodDefinition(), new Instantiation(clone));
        }
    }
}
