// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;
using Internal.NativeFormat;

namespace Internal.TypeSystem.Ecma
{
    /// <summary>
    /// Override of MetadataType that uses actual Ecma335 metadata.
    /// </summary>
    public sealed partial class EcmaType : MetadataType, EcmaModule.IEntityHandleObject
    {
        private EcmaModule _module;
        private TypeDefinitionHandle _handle;

        private TypeDefinition _typeDefinition;

        // Cached values
        private string _typeName;
        private string _typeNamespace;
        private TypeDesc[] _genericParameters;
        private MetadataType _baseType;
        private int _hashcode;

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

        public override int GetHashCode()
        {
            if (_hashcode != 0)
            {
                return _hashcode;
            }
            int nameHash = TypeHashingAlgorithms.ComputeNameHashCode(this.GetFullName());
            TypeDesc containingType = ContainingType;
            if (containingType == null)
            {
                _hashcode = nameHash;
            }
            else
            {
                _hashcode = TypeHashingAlgorithms.ComputeNestedTypeHashCode(containingType.GetHashCode(), nameHash);
            }

            return _hashcode;
        }

        EntityHandle EcmaModule.IEntityHandleObject.Handle
        {
            get
            {
                return _handle;
            }
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

        private void ComputeGenericParameters()
        {
            var genericParameterHandles = _typeDefinition.GetGenericParameters();
            int count = genericParameterHandles.Count;
            if (count > 0)
            {
                TypeDesc[] genericParameters = new TypeDesc[count];
                int i = 0;
                foreach (var genericParameterHandle in genericParameterHandles)
                {
                    genericParameters[i++] = new EcmaGenericParameter(_module, genericParameterHandle);
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

        public override ModuleDesc Module
        {
            get
            {
                return _module;
            }
        }

        public EcmaModule EcmaModule
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

        private MetadataType InitializeBaseType()
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

            Debug.Assert((flags & mask) != 0);
            return flags;
        }

        private string InitializeName()
        {
            var metadataReader = this.MetadataReader;
            _typeName = metadataReader.GetString(_typeDefinition.Name);
            return _typeName;
        }

        public override string Name
        {
            get
            {
                if (_typeName == null)
                    return InitializeName();
                return _typeName;
            }
        }

        private string InitializeNamespace()
        {
            var metadataReader = this.MetadataReader;
            _typeNamespace = metadataReader.GetString(_typeDefinition.Namespace);
            return _typeNamespace;
        }

        public override string Namespace
        {
            get
            {
                if (_typeNamespace == null)
                    return InitializeNamespace();
                return _typeNamespace;
            }
        }

        public override IEnumerable<MethodDesc> GetMethods()
        {
            foreach (var handle in _typeDefinition.GetMethods())
            {
                yield return (MethodDesc)_module.GetObject(handle);
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
                    MethodDesc method = (MethodDesc)_module.GetObject(handle);
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
                    MethodDesc method = (MethodDesc)_module.GetObject(handle);
                    return method;
                }
            }

            return null;
        }

        public override MethodDesc GetFinalizer()
        {
            // System.Object defines Finalize but doesn't use it, so we can determine that a type has a Finalizer
            // by checking for a virtual method override that lands anywhere other than Object in the inheritance
            // chain.
            if (!HasBaseType)
                return null;

            TypeDesc objectType = Context.GetWellKnownType(WellKnownType.Object);
            MethodDesc decl = objectType.GetMethod("Finalize", null);

            if (decl != null)
            {
                ResolvedVirtualMethod impl = this.FindVirtualFunctionTargetMethodOnObjectType(decl);
                if (impl.OwningType != objectType)
                {
                    return impl.Target;
                }

                return null;
            }

            // TODO: Better exception type. Should be: "CoreLib doesn't have a required thing in it".
            throw new NotImplementedException();
        }

        public override IEnumerable<FieldDesc> GetFields()
        {
            foreach (var handle in _typeDefinition.GetFields())
            {
                var field = (EcmaField)_module.GetObject(handle);
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
                    var field = (EcmaField)_module.GetObject(handle);
                    return field;
                }
            }

            return null;
        }

        public override IEnumerable<MetadataType> GetNestedTypes()
        {
            foreach (var handle in _typeDefinition.GetNestedTypes())
            {
                yield return (MetadataType)_module.GetObject(handle);
            }
        }

        public override MetadataType GetNestedType(string name)
        {
            var metadataReader = this.MetadataReader;
            var stringComparer = metadataReader.StringComparer;

            foreach (var handle in _typeDefinition.GetNestedTypes())
            {
                if (stringComparer.Equals(metadataReader.GetTypeDefinition(handle).Name, name))
                    return (MetadataType)_module.GetObject(handle);
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

        public override MetadataType ContainingType
        {
            get
            {
                if (!_typeDefinition.Attributes.IsNested())
                    return null;

                var handle = _typeDefinition.GetDeclaringType();
                return (MetadataType)_module.GetType(handle);
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return MetadataReader.HasCustomAttribute(_typeDefinition.GetCustomAttributes(),
                attributeNamespace, attributeName);
        }

        public override string ToString()
        {
            return "[" + _module.GetName().Name + "]" + this.GetFullName();
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
                        new FieldAndOffset((FieldDesc)_module.GetObject(handle), fieldDefinition.GetOffset());

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

        public override bool IsSealed
        {
            get
            {
                return (_typeDefinition.Attributes & TypeAttributes.Sealed) != 0;
            }
        }

        public override PInvokeStringFormat PInvokeStringFormat
        {
            get
            {
                return (PInvokeStringFormat)(_typeDefinition.Attributes & TypeAttributes.StringFormatMask);
            }
        }
    }
}
