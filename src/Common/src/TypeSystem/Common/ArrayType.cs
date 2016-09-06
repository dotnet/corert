// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents an array type - either a multidimensional array, or a vector
    /// (a one-dimensional array with a zero lower bound).
    /// </summary>
    public sealed partial class ArrayType : ParameterizedType
    {
        private int _rank; // -1 for regular single dimensional arrays, > 0 for multidimensional arrays

        internal ArrayType(TypeDesc elementType, int rank)
            : base(elementType)
        {
            _rank = rank;
        }

        public override int GetHashCode()
        {
            // ComputeArrayTypeHashCode expects -1 for an SzArray
            return Internal.NativeFormat.TypeHashingAlgorithms.ComputeArrayTypeHashCode(this.ElementType.GetHashCode(), _rank);
        }

        public override DefType BaseType
        {
            get
            {
                return this.Context.GetWellKnownType(WellKnownType.Array);
            }
        }

        /// <summary>
        /// Gets the type of the element of this array.
        /// </summary>
        public TypeDesc ElementType
        {
            get
            {
                return this.ParameterType;
            }
        }

        internal MethodDesc[] _methods;

        /// <summary>
        /// Gets a value indicating whether this type is a vector.
        /// </summary>
        public new bool IsSzArray
        {
            get
            {
                return _rank < 0;
            }
        }

        /// <summary>
        /// Gets the rank of this array. Note this returns "1" for both vectors, and
        /// for general arrays of rank 1. Use <see cref="IsSzArray"/> to disambiguate.
        /// </summary>
        public int Rank
        {
            get
            {
                return (_rank < 0) ? 1 : _rank;
            }
        }

        private void InitializeMethods()
        {
            int numCtors;

            if (IsSzArray)
            {
                numCtors = 1;

                // Jagged arrays have constructor for each possible depth
                var t = this.ElementType;
                while (t.IsSzArray)
                {
                    t = ((ArrayType)t).ElementType;
                    numCtors++;
                }
            }
            else
            {
                // Multidimensional arrays have two ctors, one with and one without lower bounds
                numCtors = 2;
            }

            MethodDesc[] methods = new MethodDesc[(int)ArrayMethodKind.Ctor + numCtors];

            for (int i = 0; i < methods.Length; i++)
                methods[i] = new ArrayMethod(this, (ArrayMethodKind)i);

            Interlocked.CompareExchange(ref _methods, methods, null);
        }

        public override IEnumerable<MethodDesc> GetMethods()
        {
            if (_methods == null)
                InitializeMethods();
            return _methods;
        }

        public MethodDesc GetArrayMethod(ArrayMethodKind kind)
        {
            if (_methods == null)
                InitializeMethods();
            return _methods[(int)kind];
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc instantiatedElementType = this.ElementType.InstantiateSignature(typeInstantiation, methodInstantiation);
            return instantiatedElementType.Context.GetArrayType(instantiatedElementType, _rank);
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = _rank == -1 ? TypeFlags.SzArray : TypeFlags.Array;

            if ((mask & TypeFlags.ContainsGenericVariablesComputed) != 0)
            {
                flags |= TypeFlags.ContainsGenericVariablesComputed;
                if (this.ParameterType.ContainsGenericVariables)
                    flags |= TypeFlags.ContainsGenericVariables;
            }

            flags |= TypeFlags.HasGenericVarianceComputed;

            return flags;
        }

        public override string ToString()
        {
            return this.ElementType.ToString() + "[" + new String(',', Rank - 1) + "]";
        }
    }

    public enum ArrayMethodKind
    {
        Get,
        Set,
        Address,
        Ctor
    }

    /// <summary>
    /// Represents one of the methods on array types. While array types are not typical
    /// classes backed by metadata, they do have methods that can be referenced from the IL
    /// and the type system needs to provide a way to represent them.
    /// </summary>
    public partial class ArrayMethod : MethodDesc
    {
        private ArrayType _owningType;
        private ArrayMethodKind _kind;

        internal ArrayMethod(ArrayType owningType, ArrayMethodKind kind)
        {
            _owningType = owningType;
            _kind = kind;
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

        public ArrayMethodKind Kind
        {
            get
            {
                return _kind;
            }
        }

        private MethodSignature _signature;

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    switch (_kind)
                    {
                        case ArrayMethodKind.Get:
                            {
                                var parameters = new TypeDesc[_owningType.Rank];
                                for (int i = 0; i < _owningType.Rank; i++)
                                    parameters[i] = _owningType.Context.GetWellKnownType(WellKnownType.Int32);
                                _signature = new MethodSignature(0, 0, _owningType.ElementType, parameters);
                                break;
                            }
                        case ArrayMethodKind.Set:
                            {
                                var parameters = new TypeDesc[_owningType.Rank + 1];
                                for (int i = 0; i < _owningType.Rank; i++)
                                    parameters[i] = _owningType.Context.GetWellKnownType(WellKnownType.Int32);
                                parameters[_owningType.Rank] = _owningType.ElementType;
                                _signature = new MethodSignature(0, 0, this.Context.GetWellKnownType(WellKnownType.Void), parameters);
                                break;
                            }
                        case ArrayMethodKind.Address:
                            {
                                var parameters = new TypeDesc[_owningType.Rank];
                                for (int i = 0; i < _owningType.Rank; i++)
                                    parameters[i] = _owningType.Context.GetWellKnownType(WellKnownType.Int32);
                                _signature = new MethodSignature(0, 0, _owningType.ElementType.MakeByRefType(), parameters);
                            }
                            break;
                        default:
                            {
                                int numArgs;
                                if (_owningType.IsSzArray)
                                {
                                    numArgs = 1 + (int)_kind - (int)ArrayMethodKind.Ctor;
                                }
                                else
                                {
                                    numArgs = (_kind == ArrayMethodKind.Ctor) ? _owningType.Rank : 2 * _owningType.Rank;
                                }

                                var argTypes = new TypeDesc[numArgs];
                                for (int i = 0; i < argTypes.Length; i++)
                                    argTypes[i] = _owningType.Context.GetWellKnownType(WellKnownType.Int32);
                                _signature = new MethodSignature(0, 0, this.Context.GetWellKnownType(WellKnownType.Void), argTypes);
                            }
                            break;
                    }
                }
                return _signature;
            }
        }

        public override string Name
        {
            get
            {
                switch (_kind)
                {
                    case ArrayMethodKind.Get:
                        return "Get";
                    case ArrayMethodKind.Set:
                        return "Set";
                    case ArrayMethodKind.Address:
                        return "Address";
                    default:
                        return ".ctor";
                }
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return false;
        }

        public override MethodDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc owningType = this.OwningType;
            TypeDesc instantiatedOwningType = owningType.InstantiateSignature(typeInstantiation, methodInstantiation);

            if (owningType != instantiatedOwningType)
                return ((ArrayType)instantiatedOwningType).GetArrayMethod(_kind);
            else
                return this;
        }

        public override string ToString()
        {
            return _owningType.ToString() + "." + Name;
        }
    }
}
