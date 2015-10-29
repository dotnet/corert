﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents an array of elementType (either szArray or array [multidimensional]) 
    /// </summary>
    public sealed class ArrayType : ParameterizedType
    {
        int _rank; // -1 for regular single dimensional arrays, > 0 for multidimensional arrays

        /// <summary>
        /// Constructs an array type
        /// </summary>
        /// <param name="rank">-1 for regular single dimensional arrays, &gt; 0 for multidimensional arrays</param>
        internal ArrayType(TypeDesc elementType, int rank)
            : base(elementType)
        {
            _rank = rank;
        }

        public override int GetHashCode()
        {
            return Internal.NativeFormat.TypeHashingAlgorithms.ComputeArrayTypeHashCode(this.ElementType.GetHashCode(), _rank);
        }

        public override DefType BaseType
        {
            get
            {
                return this.Context.GetWellKnownType(WellKnownType.Array);
            }
        }

        // TODO: Implement
        // public override TypeDesc[] ImplementedInterfaces
        // {
        //     get
        //     {
        //         ...
        //     }
        // }

        /// <summary>
        /// Returns the type of the array's elements
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
        /// True for single-dimensional (not 1-d multidimensional) arrays
        /// </summary>
        public new bool IsSzArray
        {
            get
            {
                return _rank < 0;
            }
        }

        /// <summary>
        /// Returns the array's rank. This can be 1 for either szArrays or for 1-d multidimensional arrays
        /// </summary>
        public int Rank
        {
            get
            {
                return (_rank < 0) ? 1 : _rank;
            }
        }

        public override IEnumerable<MethodDesc> GetMethods()
        {
            if (_methods == null)
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
            return _methods;
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc instantiatedElementType = this.ElementType.InstantiateSignature(typeInstantiation, methodInstantiation);
            return instantiatedElementType.Context.GetArrayType(instantiatedElementType);
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
            TypeFlags flags = TypeFlags.Array;

            if ((mask & TypeFlags.ContainsGenericVariablesComputed) != 0)
            {
                flags |= TypeFlags.ContainsGenericVariablesComputed;
                if (this.ParameterType.ContainsGenericVariables)
                    flags |= TypeFlags.ContainsGenericVariables;
            }

            return flags;
        }

        public override string ToString()
        {
            return this.ElementType.ToString() + "[" + new String(',', Rank - 1) + "]";
        }
    }

    /// <summary>
    /// Represents categories of the special methods the type system represents as being on arrays
    /// </summary>
    public enum ArrayMethodKind
    {
        Get,
        Set,
        Address,
        Ctor
    }

    /// <summary>
    /// Represents the Ctor, Get, Set, and Address methods on arrays that are not represented in metadata
    /// </summary>
    public class ArrayMethod : MethodDesc
    {
        ArrayType _owningType;
        ArrayMethodKind _kind;

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

        /// <summary>
        /// Returns the category of array method this method corresponds to
        /// </summary>
        public ArrayMethodKind Kind
        {
            get
            {
                return _kind;
            }
        }

        MethodSignature _signature;

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

        /// <summary>
        /// Strips method instantiation. E.g C&lt;int&gt;.m&lt;string&gt; returns C&lt;int&gt;.m&lt;U&gt;
        /// </summary>
        /// <returns></returns>
        public override MethodDesc GetMethodDefinition()
        {
            return this;
        }
        
        /// <summary>
        /// Strips both type and method instantiation.E.g C&lt;int&gt;.m&lt;string&gt; returns C&lt;T&gt;.m&lt;U&gt;
        /// </summary>
        /// <returns></returns>
        public override MethodDesc GetTypicalMethodDefinition()
        {
            return this;
        }

        public override MethodDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc owningType = this.OwningType;
            TypeDesc instantiatedOwningType = owningType.InstantiateSignature(typeInstantiation, methodInstantiation);

            if (owningType != instantiatedOwningType)
                return ((ArrayMethod[])instantiatedOwningType.GetMethods())[(int)this._kind];
            else
                return this;
        }

        public override string ToString()
        {
            return _owningType.ToString() + "." + Name;
        }
    }
}
