﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    public sealed partial class InstantiatedType : MetadataType
    {
        MetadataType _typeDef;
        Instantiation _instantiation;

        internal InstantiatedType(MetadataType typeDef, Instantiation instantiation)
        {
            Debug.Assert(!(typeDef is InstantiatedType));
            _typeDef = typeDef;

            Debug.Assert(instantiation.Length > 0);
            _instantiation = instantiation;

            _baseType = this; // Not yet initialized flag
        }

        int _hashCode;

        public override int GetHashCode()
        {
            if (_hashCode == 0)
                _hashCode = Internal.NativeFormat.TypeHashingAlgorithms.ComputeGenericInstanceHashCode(_typeDef.GetHashCode(), _instantiation);
            return _hashCode;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _typeDef.Context;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                return _instantiation;
            }
        }

        MetadataType _baseType /* = this */;

        MetadataType InitializeBaseType()
        {
            var uninst = _typeDef.MetadataBaseType;

            return (_baseType = (uninst != null) ? (MetadataType)uninst.InstantiateSignature(_instantiation, new Instantiation()) : null);
        }

        public override DefType BaseType
        {
            get
            {
                if (_baseType == this)
                    return InitializeBaseType();
                return _baseType;
            }
        }

        public override MetadataType MetadataBaseType
        {
            get
            {
                if (_baseType == this)
                    return InitializeBaseType();
                return _baseType;
            }
        }

        TypeDesc[] _implementedInterfaces = null;

        TypeDesc[] InitializeImplementedInterfaces()
        {
            TypeDesc[] uninstInterfaces = _typeDef.ImplementedInterfaces;
            TypeDesc[] instInterfaces = null;

            for (int i = 0; i<uninstInterfaces.Length; i++)
            {
                TypeDesc uninst = uninstInterfaces[i];
                TypeDesc inst = uninst.InstantiateSignature(_instantiation, new Instantiation());
                if (inst != uninst)
                {
                    if (instInterfaces == null)
                    {
                        instInterfaces = new TypeDesc[uninstInterfaces.Length];
                        for (int j = 0; j<uninstInterfaces.Length; j++)
                        {
                            instInterfaces[j] = uninstInterfaces[j];
                        }
                    }
                    instInterfaces[i] = inst;
                }
            }

            return (_implementedInterfaces = (instInterfaces != null) ? instInterfaces : uninstInterfaces);
        }

        public override TypeDesc[] ImplementedInterfaces
        {
            get
            {
                if (_implementedInterfaces == null)
                    return InitializeImplementedInterfaces();
                return _implementedInterfaces;
            }
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = 0;

            if ((mask & TypeFlags.ContainsGenericVariablesComputed) != 0)
            {
                flags |= TypeFlags.ContainsGenericVariablesComputed;

                for (int i = 0; i < _instantiation.Length; i++)
                {
                    if (_instantiation[i].ContainsGenericVariables)
                    {
                        flags |= TypeFlags.ContainsGenericVariables;
                        break;
                    }
                }
            }

            if ((mask & TypeFlags.CategoryMask) != 0)
            {
                flags |= _typeDef.Category;
            }

            return flags;
        }

        public override string Name
        {
            get
            {
                return _typeDef.Name;
            }
        }

        public override IEnumerable<MethodDesc> GetMethods()
        {
            foreach (var typicalMethodDef in _typeDef.GetMethods())
            {
                yield return _typeDef.Context.GetMethodForInstantiatedType(typicalMethodDef, this);
            }
        }

        // TODO: Substitutions, generics, modopts, ...
        public override MethodDesc GetMethod(string name, MethodSignature signature)
        {
            foreach (var meth in _typeDef.GetMethods())
            {
                if (meth.Name == name)
                {
                    MethodDesc result = meth.InstantiateSignature(Instantiation, new Instantiation());
                    if (result.Signature.Equals(signature.InstantiateSignature(Instantiation, new Instantiation())))
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        public override MethodDesc GetStaticConstructor()
        {
            MethodDesc typicalCctor = _typeDef.GetStaticConstructor();
            if (typicalCctor == null)
                return null;
            return _typeDef.Context.GetMethodForInstantiatedType(typicalCctor, this);
        }

        public override IEnumerable<FieldDesc> GetFields()
        {
            foreach (var fieldDef in _typeDef.GetFields())
            {
                yield return _typeDef.Context.GetFieldForInstantiatedType(fieldDef, this);
            }
        }

        // TODO: Substitutions, generics, modopts, ...
        public override FieldDesc GetField(string name)
        {
            FieldDesc fieldDef = _typeDef.GetField(name);
            if (fieldDef == null)
                return null;
            return _typeDef.Context.GetFieldForInstantiatedType(fieldDef, this);
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc[] clone = null;

            for (int i = 0; i < _instantiation.Length; i++)
            {
                TypeDesc uninst = _instantiation[i];
                TypeDesc inst = uninst.InstantiateSignature(typeInstantiation, methodInstantiation);
                if (inst != uninst)
                {
                    if (clone == null)
                    {
                        clone = new TypeDesc[_instantiation.Length];
                        for (int j = 0; j < clone.Length; j++)
                        {
                            clone[j] = _instantiation[j];
                        }
                    }
                    clone[i] = inst;
                }
            }

            return (clone == null) ? this : _typeDef.Context.GetInstantiatedType(_typeDef, new Instantiation(clone));
        }

        // Strips instantiation. E.g C<int> -> C<T>
        public override TypeDesc GetTypeDefinition()
        {
            return _typeDef;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(_typeDef.ToString());
            sb.Append('<');
            for (int i = 0; i < _instantiation.Length; i++)
                sb.Append(_instantiation[i].ToString());
            sb.Append('>');
            return sb.ToString();
        }

        // Properties that are passed through from the type definition
        public override ClassLayoutMetadata GetClassLayout()
        {
            return _typeDef.GetClassLayout();
        }

        public override bool IsExplicitLayout
        {
            get
            {
                return _typeDef.IsExplicitLayout;
            }
        }

        public override bool IsSequentialLayout
        {
            get
            {
                return _typeDef.IsSequentialLayout;
            }
        }

        public override bool IsBeforeFieldInit
        {
            get
            {
                return _typeDef.IsBeforeFieldInit;
            }
        }

        public override bool IsSealed
        {
            get
            {
                return _typeDef.IsSealed;
            }
        }

        public override bool IsModuleType
        {
            get
            {
                return false;
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return _typeDef.HasCustomAttribute(attributeNamespace, attributeName);
        }
    }
}
