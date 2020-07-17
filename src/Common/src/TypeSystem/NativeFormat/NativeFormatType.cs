// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Threading;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;
using Internal.NativeFormat;

namespace Internal.TypeSystem.NativeFormat
{
    /// <summary>
    /// Override of MetadataType that uses actual NativeFormat335 metadata.
    /// </summary>
    public sealed partial class NativeFormatType : MetadataType, NativeFormatMetadataUnit.IHandleObject
    {
        private static readonly LowLevelDictionary<string, TypeFlags> s_primitiveTypes = InitPrimitiveTypesDictionary();

        private static LowLevelDictionary<string, TypeFlags> InitPrimitiveTypesDictionary()
        {
            LowLevelDictionary<string, TypeFlags> result = new LowLevelDictionary<string, TypeFlags>();
            result.Add("Void", TypeFlags.Void);
            result.Add("Boolean", TypeFlags.Boolean);
            result.Add("Char", TypeFlags.Char);
            result.Add("SByte", TypeFlags.SByte);
            result.Add("Byte", TypeFlags.Byte);
            result.Add("Int16", TypeFlags.Int16);
            result.Add("UInt16", TypeFlags.UInt16);
            result.Add("Int32", TypeFlags.Int32);
            result.Add("UInt32", TypeFlags.UInt32);
            result.Add("Int64", TypeFlags.Int64);
            result.Add("UInt64", TypeFlags.UInt64);
            result.Add("IntPtr", TypeFlags.IntPtr);
            result.Add("UIntPtr", TypeFlags.UIntPtr);
            result.Add("Single", TypeFlags.Single);
            result.Add("Double", TypeFlags.Double);
            return result;
        }

        private NativeFormatModule _module;
        private NativeFormatMetadataUnit _metadataUnit;
        private TypeDefinitionHandle _handle;

        private TypeDefinition _typeDefinition;

        // Cached values
        private string _typeName;
        private string _typeNamespace;
        private TypeDesc[] _genericParameters;
        private MetadataType _baseType;
        private int _hashcode;

        internal NativeFormatType(NativeFormatMetadataUnit metadataUnit, TypeDefinitionHandle handle)
        {
            _handle = handle;
            _metadataUnit = metadataUnit;

            _typeDefinition = metadataUnit.MetadataReader.GetTypeDefinition(handle);
            _module = metadataUnit.GetModuleFromNamespaceDefinition(_typeDefinition.NamespaceDefinition);

            _baseType = this; // Not yet initialized flag

#if DEBUG
            // Initialize name eagerly in debug builds for convenience
            InitializeName();
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

        Handle NativeFormatMetadataUnit.IHandleObject.Handle
        {
            get
            {
                return _handle;
            }
        }

        NativeFormatType NativeFormatMetadataUnit.IHandleObject.Container
        {
            get
            {
                return null;
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
            var genericParameterHandles = _typeDefinition.GenericParameters;
            int count = genericParameterHandles.Count;
            if (count > 0)
            {
                TypeDesc[] genericParameters = new TypeDesc[count];
                int i = 0;
                foreach (var genericParameterHandle in genericParameterHandles)
                {
                    genericParameters[i++] = new NativeFormatGenericParameter(_metadataUnit, genericParameterHandle);
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

        public NativeFormatModule NativeFormatModule
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
                return _metadataUnit.MetadataReader;
            }
        }

        public NativeFormatMetadataUnit MetadataUnit
        {
            get
            {
                return _metadataUnit;
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
            if (baseTypeHandle.IsNull(MetadataReader))
            {
                _baseType = null;
                return null;
            }

            var type = _metadataUnit.GetType(baseTypeHandle) as MetadataType;
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

            if ((mask & TypeFlags.CategoryMask) != 0 && (flags & TypeFlags.CategoryMask) == 0)
            {
                TypeDesc baseType = this.BaseType;

                if (baseType != null && baseType.IsWellKnownType(WellKnownType.ValueType) &&
                    !this.IsWellKnownType(WellKnownType.Enum))
                {
                    TypeFlags categoryFlags;
                    if (!TryGetCategoryFlagsForPrimitiveType(out categoryFlags))
                    {
                        categoryFlags = TypeFlags.ValueType;
                    }
                    flags |= categoryFlags;
                }
                else
                if (baseType != null && baseType.IsWellKnownType(WellKnownType.Enum))
                {
                    flags |= TypeFlags.Enum;
                }
                else
                {
                    if ((_typeDefinition.Flags & TypeAttributes.Interface) != 0)
                        flags |= TypeFlags.Interface;
                    else
                        flags |= TypeFlags.Class;
                }

                // All other cases are handled during TypeSystemContext intitialization
            }

            if ((mask & TypeFlags.HasGenericVarianceComputed) != 0 &&
                (flags & TypeFlags.HasGenericVarianceComputed) == 0)
            {
                flags |= TypeFlags.HasGenericVarianceComputed;

                foreach (GenericParameterDesc genericParam in Instantiation)
                {
                    if (genericParam.Variance != GenericVariance.None)
                    {
                        flags |= TypeFlags.HasGenericVariance;
                        break;
                    }
                }
            }

            if ((mask & TypeFlags.HasFinalizerComputed) != 0)
            {
                flags |= TypeFlags.HasFinalizerComputed;

                if (GetFinalizer() != null)
                    flags |= TypeFlags.HasFinalizer;
            }

            if ((mask & TypeFlags.AttributeCacheComputed) != 0)
            {
                flags |= TypeFlags.AttributeCacheComputed;

                if (IsValueType && HasCustomAttribute("System.Runtime.CompilerServices", "IsByRefLikeAttribute"))
                    flags |= TypeFlags.IsByRefLike;
            }

            return flags;
        }

        private bool TryGetCategoryFlagsForPrimitiveType(out TypeFlags categoryFlags)
        {
            categoryFlags = 0;
            if (_module != _metadataUnit.Context.SystemModule)
            {
                // Primitive types reside in the system module
                return false;
            }
            NamespaceDefinition namespaceDef = MetadataReader.GetNamespaceDefinition(_typeDefinition.NamespaceDefinition);
            if (namespaceDef.ParentScopeOrNamespace.HandleType != HandleType.NamespaceDefinition)
            {
                // Primitive types are in the System namespace the parent of which is the root namespace
                return false;
            }
            if (!namespaceDef.Name.StringEquals("System", MetadataReader))
            {
                // Namespace name must be 'System'
                return false;
            }
            NamespaceDefinitionHandle parentNamespaceDefHandle =
                namespaceDef.ParentScopeOrNamespace.ToNamespaceDefinitionHandle(MetadataReader);
            NamespaceDefinition parentDef = MetadataReader.GetNamespaceDefinition(parentNamespaceDefHandle);
            if (parentDef.ParentScopeOrNamespace.HandleType != HandleType.ScopeDefinition)
            {
                // The root parent namespace should have scope (assembly) handle as its parent
                return false;
            }
            return s_primitiveTypes.TryGetValue(Name, out categoryFlags);
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
            if (ContainingType == null)
            {
                var metadataReader = this.MetadataReader;
                _typeNamespace = metadataReader.GetNamespaceName(_typeDefinition.NamespaceDefinition);
                return _typeNamespace;
            }
            else
            {
                _typeNamespace = "";
                return _typeNamespace;
            }
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
            foreach (var handle in _typeDefinition.Methods)
            {
                yield return (MethodDesc)_metadataUnit.GetMethod(handle, this);
            }
        }

        public override MethodDesc GetMethod(string name, MethodSignature signature, Instantiation substitution)
        {
            var metadataReader = this.MetadataReader;

            foreach (var handle in _typeDefinition.Methods)
            {
                if (metadataReader.GetMethod(handle).Name.StringEquals(name, metadataReader))
                {
                    MethodDesc method = (MethodDesc)_metadataUnit.GetMethod(handle, this);
                    if (signature == null || signature.Equals(method.Signature.ApplySubstitution(substitution)))
                        return method;
                }
            }

            return null;
        }

        public override MethodDesc GetStaticConstructor()
        {
            var metadataReader = this.MetadataReader;

            foreach (var handle in _typeDefinition.Methods)
            {
                var methodDefinition = metadataReader.GetMethod(handle);
                if (methodDefinition.Flags.IsRuntimeSpecialName() &&
                    methodDefinition.Name.StringEquals(".cctor", metadataReader))
                {
                    MethodDesc method = (MethodDesc)_metadataUnit.GetMethod(handle, this);
                    return method;
                }
            }

            return null;
        }

        public override MethodDesc GetDefaultConstructor()
        {
            if (IsAbstract)
                return null;

            MetadataReader metadataReader = this.MetadataReader;

            foreach (var handle in _typeDefinition.Methods)
            {
                var methodDefinition = metadataReader.GetMethod(handle);
                MethodAttributes attributes = methodDefinition.Flags;
                if (attributes.IsRuntimeSpecialName() && attributes.IsPublic() &&
                    methodDefinition.Name.StringEquals(".ctor", metadataReader))
                {
                    MethodDesc method = (MethodDesc)_metadataUnit.GetMethod(handle, this);
                    if (method.Signature.Length != 0)
                        continue;

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
                MethodDesc impl = this.FindVirtualFunctionTargetMethodOnObjectType(decl);
                if (impl == null)
                {
                    // TODO: invalid input: the type doesn't derive from our System.Object
                    ThrowHelper.ThrowTypeLoadException(this);
                }

                if (impl.OwningType != objectType)
                {
                    return impl;
                }

                return null;
            }

            // Class library doesn't have finalizers
            return null;
        }

        public override IEnumerable<FieldDesc> GetFields()
        {
            foreach (var handle in _typeDefinition.Fields)
            {
                yield return _metadataUnit.GetField(handle, this);
            }
        }

        public override FieldDesc GetField(string name)
        {
            var metadataReader = this.MetadataReader;

            foreach (var handle in _typeDefinition.Fields)
            {
                if (metadataReader.GetField(handle).Name.StringEquals(name, metadataReader))
                {
                    return _metadataUnit.GetField(handle, this);
                }
            }

            return null;
        }

        public override IEnumerable<MetadataType> GetNestedTypes()
        {
            foreach (var handle in _typeDefinition.NestedTypes)
            {
                yield return (MetadataType)_metadataUnit.GetType(handle);
            }
        }

        public override MetadataType GetNestedType(string name)
        {
            var metadataReader = this.MetadataReader;

            foreach (var handle in _typeDefinition.NestedTypes)
            {
                if (metadataReader.GetTypeDefinition(handle).Name.StringEquals(name, metadataReader))
                    return (MetadataType)_metadataUnit.GetType(handle);
            }

            return null;
        }

        public TypeAttributes Attributes
        {
            get
            {
                return _typeDefinition.Flags;
            }
        }

        //
        // ContainingType of nested type
        //
        public override DefType ContainingType
        {
            get
            {
                var handle = _typeDefinition.EnclosingType;
                if (handle.IsNull(MetadataReader))
                    return null;
                return (DefType)_metadataUnit.GetType(handle);
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return MetadataReader.HasCustomAttribute(_typeDefinition.CustomAttributes,
                attributeNamespace, attributeName);
        }

        public override ClassLayoutMetadata GetClassLayout()
        {
            ClassLayoutMetadata result;
            result.PackingSize = checked((int)_typeDefinition.PackingSize);
            result.Size = checked((int)_typeDefinition.Size);

            // Skip reading field offsets if this is not explicit layout
            if (IsExplicitLayout)
            {
                var fieldDefinitionHandles = _typeDefinition.Fields;
                var numInstanceFields = 0;

                foreach (var handle in fieldDefinitionHandles)
                {
                    var fieldDefinition = MetadataReader.GetField(handle);
                    if ((fieldDefinition.Flags & FieldAttributes.Static) != 0)
                        continue;

                    numInstanceFields++;
                }

                result.Offsets = new FieldAndOffset[numInstanceFields];

                int index = 0;
                foreach (var handle in fieldDefinitionHandles)
                {
                    var fieldDefinition = MetadataReader.GetField(handle);
                    if ((fieldDefinition.Flags & FieldAttributes.Static) != 0)
                        continue;

                    // Note: GetOffset() returns -1 when offset was not set in the metadata
                    int fieldOffsetInMetadata = (int)fieldDefinition.Offset;
                    LayoutInt fieldOffset = fieldOffsetInMetadata == -1 ? FieldAndOffset.InvalidOffset : new LayoutInt(fieldOffsetInMetadata);
                    result.Offsets[index] =
                        new FieldAndOffset(_metadataUnit.GetField(handle, this), fieldOffset);

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
                return (_typeDefinition.Flags & TypeAttributes.ExplicitLayout) != 0;
            }
        }

        public override bool IsSequentialLayout
        {
            get
            {
                return (_typeDefinition.Flags & TypeAttributes.SequentialLayout) != 0;
            }
        }

        public override bool IsBeforeFieldInit
        {
            get
            {
                return (_typeDefinition.Flags & TypeAttributes.BeforeFieldInit) != 0;
            }
        }

        public override bool IsSealed
        {
            get
            {
                return (_typeDefinition.Flags & TypeAttributes.Sealed) != 0;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return (_typeDefinition.Flags & TypeAttributes.Abstract) != 0;
            }
        }
    }
}
