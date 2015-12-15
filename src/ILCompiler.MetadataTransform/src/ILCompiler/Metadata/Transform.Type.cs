// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Internal.Metadata.NativeFormat.Writer;

using Ecma = System.Reflection.Metadata;
using Cts = Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using TypeAttributes = System.Reflection.TypeAttributes;

namespace ILCompiler.Metadata
{
    partial class Transform<TPolicy>
    {
        private EntityMap<Cts.TypeDesc, MetadataRecord> _types =
            new EntityMap<Cts.TypeDesc, MetadataRecord>(EqualityComparer<Cts.TypeDesc>.Default);

        private Action<Cts.MetadataType, TypeDefinition> _initTypeDef;
        private Action<Cts.MetadataType, TypeReference> _initTypeRef;
        private Action<Cts.ArrayType, TypeSpecification> _initSzArray;
        private Action<Cts.ArrayType, TypeSpecification> _initArray;
        private Action<Cts.ByRefType, TypeSpecification> _initByRef;
        private Action<Cts.PointerType, TypeSpecification> _initPointer;
        private Action<Cts.InstantiatedType, TypeSpecification> _initTypeInst;
        private Action<Cts.GenericParameterDesc, TypeSpecification> _initTypeVar;

        public override MetadataRecord HandleType(Cts.TypeDesc type)
        {
            MetadataRecord rec;

            if (type.IsSzArray)
            {
                var arrayType = (Cts.ArrayType)type;
                rec = _types.GetOrCreate(arrayType, _initSzArray ?? (_initSzArray = InitializeSzArray));
            }
            else if (type.IsArray)
            {
                var arrayType = (Cts.ArrayType)type;
                rec = _types.GetOrCreate(arrayType, _initArray ?? (_initArray = InitializeArray));
            }
            else if (type.IsByRef)
            {
                var byRefType = (Cts.ByRefType)type;
                rec = _types.GetOrCreate(byRefType, _initByRef ?? (_initByRef = InitializeByRef));
            }
            else if (type.IsPointer)
            {
                var pointerType = (Cts.PointerType)type;
                rec = _types.GetOrCreate(pointerType, _initPointer ?? (_initPointer = InitializePointer));
            }
            else if (type is Cts.GenericParameterDesc)
            {
                var genericParameter = (Cts.GenericParameterDesc)type;
                rec = _types.GetOrCreate(genericParameter, _initTypeVar ?? (_initTypeVar = InitializeTypeVariable));
            }
            else if (type is Cts.InstantiatedType)
            {
                var instType = (Cts.InstantiatedType)type;
                rec = _types.GetOrCreate(instType, _initTypeInst ?? (_initTypeInst = InitializeTypeInstance));
            }
            else
            {
                var metadataType = (Cts.MetadataType)type;
                if (_policy.GeneratesMetadata(metadataType))
                {
                    rec = _types.GetOrCreate(metadataType, _initTypeDef ?? (_initTypeDef = InitializeTypeDef));
                }
                else
                {
                    rec = _types.GetOrCreate(metadataType, _initTypeRef ?? (_initTypeRef = InitializeTypeRef));
                }
            }

            Debug.Assert(rec is TypeDefinition || rec is TypeReference || rec is TypeSpecification);

            return rec;
        }

        private void InitializeSzArray(Cts.ArrayType entity, TypeSpecification record)
        {
            record.Signature = new SZArraySignature
            {
                ElementType = HandleType(entity.ElementType),
            };
        }

        private void InitializeArray(Cts.ArrayType entity, TypeSpecification record)
        {
            record.Signature = new ArraySignature
            {
                ElementType = HandleType(entity.ElementType),
                Rank = entity.Rank,
                // TODO: sizes and lower bounds
            };
        }

        private void InitializeByRef(Cts.ByRefType entity, TypeSpecification record)
        {
            record.Signature = new ByReferenceSignature
            {
                Type = HandleType(entity.ParameterType)
            };
        }

        private void InitializePointer(Cts.PointerType entity, TypeSpecification record)
        {
            record.Signature = new PointerSignature
            {
                Type = HandleType(entity.ParameterType)
            };
        }

        private void InitializeTypeVariable(Cts.GenericParameterDesc entity, TypeSpecification record)
        {
            MetadataRecord sig;
            if (entity.Kind == Cts.GenericParameterKind.Type)
            {
                sig = new TypeVariableSignature
                {
                    Number = entity.Index
                };
            }
            else
            {
                Debug.Assert(entity.Kind == Cts.GenericParameterKind.Method);
                sig = new MethodTypeVariableSignature
                {
                    Number = entity.Index
                };
            }

            record.Signature = sig;
        }

        private void InitializeTypeInstance(Cts.InstantiatedType entity, TypeSpecification record)
        {
            var args = new List<MetadataRecord>(entity.Instantiation.Length);
            for (int i = 0; i < entity.Instantiation.Length; i++)
            {
                args[i] = HandleType(entity.Instantiation[i]);
            }

            record.Signature = new TypeInstantiationSignature
            {
                GenericType = HandleType(entity.GetTypeDefinition()),
                GenericTypeArguments = args
            };
        }

        private void InitializeTypeRef(Cts.MetadataType entity, TypeReference record)
        {
            if (entity.ContainingType != null)
            {
                record.ParentNamespaceOrType = HandleType(entity.ContainingType);
            }
            else
            {
                record.ParentNamespaceOrType = HandleNamespaceDefinition(entity.Module, entity.Namespace);
            }

            record.TypeName = HandleString(entity.Name);
        }

        private void InitializeTypeDef(Cts.MetadataType entity, TypeDefinition record)
        {
            if (entity.ContainingType != null)
            {
                var enclosingType = (TypeDefinition)HandleType(entity.ContainingType);
                record.EnclosingType = enclosingType;
                enclosingType.NestedTypes.Add(record);

                // TODO: NamespaceDefinition?
            }
            else
            {
                var namespaceDefinition = HandleNamespaceDefinition(entity.Module, entity.Namespace);
                record.NamespaceDefinition = namespaceDefinition;
                namespaceDefinition.TypeDefinitions.Add(record);
            }

            record.Name = HandleString(entity.Name);

            Cts.ClassLayoutMetadata layoutMetadata = entity.GetClassLayout();
            record.Size = checked((uint)layoutMetadata.Size);
            record.PackingSize = checked((uint)layoutMetadata.PackingSize);
            record.Flags = GetTypeAttributes(entity);

            if (entity.HasBaseType)
                record.BaseType = HandleType(entity.BaseType);

            record.Interfaces = entity.ExplicitlyImplementedInterfaces
                .Where(i => !IsBlocked(i))
                .Select(i => HandleType(i)).ToList();

            // TODO: GenericParameters
            // TODO: CustomAttributes
        }

        private TypeAttributes GetTypeAttributes(Cts.MetadataType type)
        {
            TypeAttributes result;

            var ecmaType = type as Cts.Ecma.EcmaType;
            if (ecmaType != null)
            {
                Ecma.TypeDefinition ecmaRecord = ecmaType.MetadataReader.GetTypeDefinition(ecmaType.Handle);
                result = ecmaRecord.Attributes;
            }
            else
            {
                result = 0;

                if (type.IsExplicitLayout)
                    result |= TypeAttributes.ExplicitLayout;
                if (type.IsSequentialLayout)
                    result |= TypeAttributes.SequentialLayout;
                if (type.IsInterface)
                    result |= TypeAttributes.Interface;
                if (type.IsSealed)
                    result |= TypeAttributes.Sealed;
                if (type.IsBeforeFieldInit)
                    result |= TypeAttributes.BeforeFieldInit;
            }

            return result;
        }
    }
}
