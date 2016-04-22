// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
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
            return Internal.NativeFormat.TypeHashingAlgorithms.ComputeArrayTypeHashCode(this.ElementType.GetHashCode(), _rank == -1 ? 1 : _rank);
        }

        public override DefType BaseType
        {
            get
            {
                return this.Context.GetWellKnownType(WellKnownType.Array);
            }
        }

        public TypeDesc ElementType
        {
            get
            {
                return this.ParameterType;
            }
        }

        internal MethodDesc[] _methods;

        public new bool IsSzArray
        {
            get
            {
                return _rank < 0;
            }
        }

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

                var t = this.ElementType;
                while (t.IsSzArray)
                {
                    t = ((ArrayType)t).ElementType;
                    numCtors++;
                }
            }
            else
            {
                // ELEMENT_TYPE_ARRAY has two ctor functions, one with and one without lower bounds
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

        public override TypeDesc GetTypeDefinition()
        {
            TypeDesc result = this;

            TypeDesc elementDef = this.ElementType.GetTypeDefinition();
            if (elementDef != this.ElementType)
                result = elementDef.Context.GetArrayType(elementDef);

            return result;
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

        // Strips method instantiation. E.g C<int>.m<string> -> C<int>.m<U>
        public override MethodDesc GetMethodDefinition()
        {
            return this;
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return false;
        }

        // Strips both type and method instantiation. E.g C<int>.m<string> -> C<T>.m<U>
        public override MethodDesc GetTypicalMethodDefinition()
        {
            return this;
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
