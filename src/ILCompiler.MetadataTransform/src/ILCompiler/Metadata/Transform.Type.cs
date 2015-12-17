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
        private Action<Cts.SignatureTypeVariable, TypeSpecification> _initTypeVar;
        private Action<Cts.SignatureMethodVariable, TypeSpecification> _initMethodVar;

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
            else if (type is Cts.SignatureTypeVariable)
            {
                var variable = (Cts.SignatureTypeVariable)type;
                rec = _types.GetOrCreate(variable, _initTypeVar ?? (_initTypeVar = InitializeTypeVariable));
            }
            else if (type is Cts.SignatureMethodVariable)
            {
                var variable = (Cts.SignatureMethodVariable)type;
                rec = _types.GetOrCreate(variable, _initMethodVar ?? (_initMethodVar = InitializeMethodVariable));
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
                // TODO: LowerBounds
                // TODO: Sizes
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

        private void InitializeTypeVariable(Cts.SignatureTypeVariable entity, TypeSpecification record)
        {
            record.Signature = new TypeVariableSignature
            {
                Number = entity.Index
            };
        }

        private void InitializeMethodVariable(Cts.SignatureMethodVariable entity, TypeSpecification record)
        {
            record.Signature = new MethodTypeVariableSignature
            {
                Number = entity.Index
            };
        }

        private void InitializeTypeInstance(Cts.InstantiatedType entity, TypeSpecification record)
        {
            var args = new List<MetadataRecord>(entity.Instantiation.Length);
            for (int i = 0; i < entity.Instantiation.Length; i++)
            {
                args.Add(HandleType(entity.Instantiation[i]));
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

                var namespaceDefinition =
                    HandleNamespaceDefinition(entity.ContainingType.Module, entity.ContainingType.Namespace);
                record.NamespaceDefinition = namespaceDefinition;
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
            {
                record.BaseType = HandleType(entity.BaseType);
            }

            if (entity.ExplicitlyImplementedInterfaces.Length > 0)
            {
                record.Interfaces = entity.ExplicitlyImplementedInterfaces
                    .Where(i => !IsBlocked(i))
                    .Select(i => HandleType(i)).ToList();
            }

            if (entity.HasInstantiation)
            {
                var genericParams = new List<GenericParameter>(entity.Instantiation.Length);
                foreach (var p in entity.Instantiation)
                    genericParams.Add(HandleGenericParameter((Cts.GenericParameterDesc)p));
                record.GenericParameters = genericParams;
            }

            var fields = new List<Field>();
            foreach (var field in entity.GetFields())
            {
                if (_policy.GeneratesMetadata(field))
                {
                    fields.Add(HandleFieldDefinition(field));
                }
            }
            record.Fields = fields;

            var methods = new List<Method>();
            foreach (var method in entity.GetMethods())
            {
                if (_policy.GeneratesMetadata(method))
                {
                    methods.Add(HandleMethodDefinition(method));
                }
            }
            record.Methods = methods;

            var ecmaEntity = entity as Cts.Ecma.EcmaType;
            if (ecmaEntity != null)
            {
                Ecma.TypeDefinition ecmaRecord = ecmaEntity.MetadataReader.GetTypeDefinition(ecmaEntity.Handle);
                foreach (var property in ecmaRecord.GetProperties())
                {
                    Property prop = HandleProperty(ecmaEntity.EcmaModule, property);
                    if (prop != null)
                        record.Properties.Add(prop);
                }

                // TODO: Events

                // TODO: CustomAttributes
            }
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

                // Not set: Abstract, Ansi/Unicode/Auto, HasSecurity, Import, visibility, Serializable,
                //          WindowsRuntime, HasSecurity, SpecialName, RTSpecialName
            }

            return result;
        }
    }
}
