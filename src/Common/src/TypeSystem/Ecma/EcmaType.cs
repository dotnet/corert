// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    /// <summary>
    /// Override of MetadataType that uses actual Ecma335 metadata.
    /// </summary>
    public sealed partial class EcmaType : MetadataType
    {
        EcmaModule _module;
        TypeDefinitionHandle _handle;

        TypeDefinition _typeDefinition;

        // Cached values
        string _name;
        TypeDesc[] _genericParameters;
        MetadataType _baseType;

        internal EcmaType(EcmaModule module, TypeDefinitionHandle handle)
        {
            _module = module;
            _handle = handle;

            _typeDefinition = module.MetadataReader.GetTypeDefinition(handle);

            _baseType = this; // Not yet initialized flag

#if DEBUG
            // Initialize name eagerly in debug builds for convenience
            this.ToString();
#endif
        }

        // TODO: Use stable hashcode based on the type name?
        // public override int GetHashCode()
        // {
        // }

        public override TypeSystemContext Context
        {
            get
            {
                return _module.Context;
            }
        }

        void ComputeGenericParameters()
        {
            var genericParameterHandles = _typeDefinition.GetGenericParameters();
            int count = genericParameterHandles.Count;
            if (count > 0)
            {
                TypeDesc[] genericParameters = new TypeDesc[count];
                int i = 0;
                foreach (var genericParameterHandle in genericParameterHandles)
                {
                    genericParameters[i++] = new EcmaGenericParameter(this.Module, genericParameterHandle);
                }
                Interlocked.CompareExchange(ref _genericParameters, genericParameters, null);
            }
            else
            {
                _genericParameters = TypeDesc.EmptyTypes;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                if (_genericParameters == null)
                    ComputeGenericParameters();
                return new Instantiation(_genericParameters);
            }
        }

        public EcmaModule Module
        {
            get
            {
                return _module;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _module.MetadataReader;
            }
        }

        public TypeDefinitionHandle Handle
        {
            get
            {
                return _handle;
            }
        }

        MetadataType InitializeBaseType()
        {
            var baseTypeHandle = _typeDefinition.BaseType;
            if (baseTypeHandle.IsNil)
            {
                _baseType = null;
                return null;
            }

            var type = _module.GetType(baseTypeHandle) as MetadataType;
            if (type == null)
            {
                throw new BadImageFormatException();
            }
            _baseType = type;
            return type;
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
        
        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = 0;

            if ((mask & TypeFlags.ContainsGenericVariablesComputed) != 0)
            {
                flags |= TypeFlags.ContainsGenericVariablesComputed;

                // TODO: Do we really want to get the instantiation to figure out whether the type is generic?
                if (this.HasInstantiation)
                    flags |= TypeFlags.ContainsGenericVariables;
            }

            if ((mask & TypeFlags.CategoryMask) != 0)
            {
                TypeDesc baseType = this.BaseType;

                if (_module.Context.IsWellKnownType(baseType, WellKnownType.ValueType))
                {
                    flags |= TypeFlags.ValueType;
                }
                else
                if (_module.Context.IsWellKnownType(baseType, WellKnownType.Enum))
                {
                    flags |= TypeFlags.Enum;
                }
                else
                {
                    if ((_typeDefinition.Attributes & TypeAttributes.Interface) != 0)
                        flags |= TypeFlags.Interface;
                    else
                        flags |= TypeFlags.Class;
                }

                // All other cases are handled during TypeSystemContext intitialization
            }

            return flags;
        }

        private string InitializeName()
        {
            var metadataReader = this.MetadataReader;
            string typeName = metadataReader.GetString(_typeDefinition.Name);
            string typeNamespace = metadataReader.GetString(_typeDefinition.Namespace);
            string name = (typeNamespace.Length > 0) ? (typeNamespace + "." + typeName) : typeName;
            return (_name = name);
        }

        public override string Name
        {
            get
            {
                if (_name == null)
                    return InitializeName();
                return _name;
            }
        }

        public override IEnumerable<MethodDesc> GetMethods()
        {
            foreach (var handle in _typeDefinition.GetMethods())
            {
                yield return (MethodDesc)this.Module.GetObject(handle);
            }
        }

        public override MethodDesc GetMethod(string name, MethodSignature signature)
        {
            var metadataReader = this.MetadataReader;
            var stringComparer = metadataReader.StringComparer;

            foreach (var handle in _typeDefinition.GetMethods())
            {
                if (stringComparer.Equals(metadataReader.GetMethodDefinition(handle).Name, name))
                {
                    MethodDesc method = (MethodDesc)this.Module.GetObject(handle);
                    if (signature == null || signature.Equals(method.Signature))
                        return method;
                }
            }

            return null;
        }

        public override MethodDesc GetStaticConstructor()
        {
            var metadataReader = this.MetadataReader;
            var stringComparer = metadataReader.StringComparer;

            foreach (var handle in _typeDefinition.GetMethods())
            {
                var methodDefinition = metadataReader.GetMethodDefinition(handle);
                if ((methodDefinition.Attributes & MethodAttributes.SpecialName) != 0 &&
                    stringComparer.Equals(methodDefinition.Name, ".cctor"))
                {
                    MethodDesc method = (MethodDesc)this.Module.GetObject(handle);
                    return method;
                }
            }

            return null;
        }

        public override IEnumerable<FieldDesc> GetFields()
        {
            foreach (var handle in _typeDefinition.GetFields())
            {
                var field = (EcmaField)this.Module.GetObject(handle);

                // Literal fields are not interesting for codegen purposes
                if (!field.IsLiteral)
                    yield return field;
            }
        }

        public override FieldDesc GetField(string name)
        {
            var metadataReader = this.MetadataReader;
            var stringComparer = metadataReader.StringComparer;

            foreach (var handle in _typeDefinition.GetFields())
            {
                if (stringComparer.Equals(metadataReader.GetFieldDefinition(handle).Name, name))
                {
                    var field = (EcmaField)this.Module.GetObject(handle);

                    // Literal fields are not interesting for codegen purposes
                    if (!field.IsLiteral)
                        return field;
                }
            }

            return null;
        }

        public IEnumerable<TypeDesc> GetNestedTypes()
        {
            foreach (var handle in _typeDefinition.GetNestedTypes())
            {
                yield return (TypeDesc)this.Module.GetObject(handle);
            }
        }

        public TypeDesc GetNestedType(string name)
        {
            var metadataReader = this.MetadataReader;
            var stringComparer = metadataReader.StringComparer;

            foreach (var handle in _typeDefinition.GetNestedTypes())
            {
                if (stringComparer.Equals(metadataReader.GetTypeDefinition(handle).Name, name))
                    return (TypeDesc)this.Module.GetObject(handle);
            }

            return null;
        }

        public TypeAttributes Attributes
        {
            get
            {
                return _typeDefinition.Attributes;
            }
        }

        //
        // ContainingType of nested type
        //
        public TypeDesc ContainingType
        {
            get
            {
                var handle = _typeDefinition.GetDeclaringType();
                if (handle.IsNil)
                    return null;
                return _module.GetType(handle);
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return MetadataReader.HasCustomAttribute(_typeDefinition.GetCustomAttributes(),
                attributeNamespace, attributeName);
        }

        public override string ToString()
        {
            return "[" + Module.GetName().Name + "]" + this.Name;
        }

        public override ClassLayoutMetadata GetClassLayout()
        {
            TypeLayout layout = _typeDefinition.GetLayout();

            ClassLayoutMetadata result;
            result.PackingSize = layout.PackingSize;
            result.Size = layout.Size;

            // Skip reading field offsets if this is not explicit layout
            if (IsExplicitLayout)
            {
                var fieldDefinitionHandles = _typeDefinition.GetFields();
                var numInstanceFields = 0;

                foreach (var handle in fieldDefinitionHandles)
                {
                    var fieldDefinition = MetadataReader.GetFieldDefinition(handle);
                    if ((fieldDefinition.Attributes & FieldAttributes.Static) != 0)
                        continue;

                    numInstanceFields++;
                }

                result.Offsets = new FieldAndOffset[numInstanceFields];

                int index = 0;
                foreach (var handle in fieldDefinitionHandles)
                {
                    var fieldDefinition = MetadataReader.GetFieldDefinition(handle);
                    if ((fieldDefinition.Attributes & FieldAttributes.Static) != 0)
                        continue;

                    // Note: GetOffset() returns -1 when offset was not set in the metadata which maps nicely
                    //       to FieldAndOffset.InvalidOffset.
                    Debug.Assert(FieldAndOffset.InvalidOffset == -1);
                    result.Offsets[index] =
                        new FieldAndOffset((FieldDesc)this.Module.GetObject(handle), fieldDefinition.GetOffset());

                    index++;
                }
            }
            else
                result.Offsets = null;

            return result;
        }

        public override bool IsExplicitLayout
        {
            get
            {
                return (_typeDefinition.Attributes & TypeAttributes.ExplicitLayout) != 0;
            }
        }

        public override bool IsSequentialLayout
        {
            get
            {
                return (_typeDefinition.Attributes & TypeAttributes.SequentialLayout) != 0;
            }
        }

        public override bool IsBeforeFieldInit
        {
            get
            {
                return (_typeDefinition.Attributes & TypeAttributes.BeforeFieldInit) != 0;
            }
        }

        public override bool IsModuleType
        {
            get
            {
                return Module.GetGlobalModuleType() == this;
            }
        }

        public override bool IsSealed
        {
            get
            {
                return (_typeDefinition.Attributes & TypeAttributes.Sealed) != 0;
            }
        }
    }
}
