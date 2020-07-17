// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using System.Reflection.Runtime;
using System.Reflection.Runtime.FieldInfos;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.General.EcmaFormat;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;
using System.Reflection.Runtime.CustomAttributes;
using System.Reflection.Runtime.BindingFlagSupport;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Runtime.TypeLoader;

using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.FieldInfos.EcmaFormat
{
    //
    // The Runtime's implementation of fields.
    //
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class EcmaFormatRuntimeFieldInfo : RuntimeFieldInfo
    {
        //
        // fieldHandle    - the "tkFieldDef" that identifies the field.
        // definingType   - the "tkTypeDef" that defined the field (this is where you get the metadata reader that created fieldHandle.)
        // contextType    - the type that supplies the type context (i.e. substitutions for generic parameters.) Though you
        //                  get your raw information from "definingType", you report "contextType" as your DeclaringType property.
        //
        //  For example:
        //
        //       typeof(Foo<>).GetTypeInfo().DeclaredMembers
        //
        //           The definingType and contextType are both Foo<>
        //
        //       typeof(Foo<int,String>).GetTypeInfo().DeclaredMembers
        //
        //          The definingType is "Foo<,>"
        //          The contextType is "Foo<int,String>"
        //
        //  We don't report any DeclaredMembers for arrays or generic parameters so those don't apply.
        //
        private EcmaFormatRuntimeFieldInfo(FieldDefinitionHandle fieldHandle, EcmaFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType) :
            base(contextTypeInfo, reflectedType)
        {
            _fieldHandle = fieldHandle;
            _definingTypeInfo = definingTypeInfo;
            _reader = definingTypeInfo.Reader;
            _field = _reader.GetFieldDefinition(fieldHandle);
        }

        public sealed override FieldAttributes Attributes
        {
            get
            {
                return _field.Attributes;
            }
        }

        public sealed override int MetadataToken
        {
            get
            {
                return MetadataTokens.GetToken(_fieldHandle);
            }
        }

        protected sealed override string MetadataName
        {
            get
            {
                return _field.Name.GetString(_reader);
            }
        }

        public sealed override String ToString()
        {
            try
            {
                TypeContext typeContext = _contextTypeInfo.TypeContext;
                ReflectionTypeProvider reflectionTypeProvider = new ReflectionTypeProvider(throwOnError: false);
                RuntimeTypeInfo fieldType = _field.DecodeSignature(reflectionTypeProvider, typeContext);

                string fieldTypeName;
                if (reflectionTypeProvider.ExceptionOccurred)
                    fieldTypeName = Type.DefaultTypeNameWhenMissingMetadata;
                else 
                    fieldTypeName = fieldType.FormatTypeNameForReflection();
                    
                return fieldTypeName + " " + this.Name;
            }
            catch
            {
                return Type.DefaultTypeNameWhenMissingMetadata + " " + this.Name;
            }
        }

        public sealed override bool HasSameMetadataDefinitionAs(MemberInfo other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (!(other is EcmaFormatRuntimeFieldInfo otherField))
                return false;
            if (!(_reader == otherField._reader))
                return false;
            if (!(_fieldHandle.Equals(otherField._fieldHandle)))
                return false;
            return true;
        }

        public sealed override bool Equals(Object obj)
        {
            if (!(obj is EcmaFormatRuntimeFieldInfo other))
                return false;
            if (!(_reader == other._reader))
                return false;
            if (!(_fieldHandle.Equals(other._fieldHandle)))
                return false;
            if (!(_contextTypeInfo.Equals(other._contextTypeInfo)))
                return false;
            if (!(_reflectedType.Equals(other._reflectedType)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _fieldHandle.GetHashCode();
        }

        public sealed override RuntimeFieldHandle FieldHandle
        {
            get
            {
                return TypeLoaderEnvironment.Instance.GetRuntimeFieldHandleForComponents(
                    DeclaringType.TypeHandle,
                    Name);
            }
        }

        public sealed override Type[] GetOptionalCustomModifiers() { throw new NotImplementedException(); }

        public sealed override Type[] GetRequiredCustomModifiers() { throw new NotImplementedException(); }

        protected sealed override bool GetDefaultValueIfAvailable(bool raw, out object defaultValue)
        {
            return DefaultValueProcessing.GetDefaultValueIfAny(_reader, ref _field, this, raw, out defaultValue);
        }

        protected sealed override FieldAccessor TryGetFieldAccessor()
        {
            throw new NotImplementedException();
        }

        protected sealed override RuntimeTypeInfo FieldRuntimeType
        {
            get
            {
                TypeContext typeContext = _contextTypeInfo.TypeContext;
                ReflectionTypeProvider reflectionTypeProvider = new ReflectionTypeProvider(throwOnError: true);
                return _field.DecodeSignature(reflectionTypeProvider, typeContext);
            }
        }

        protected sealed override RuntimeTypeInfo DefiningType { get { return _definingTypeInfo; } }

        protected sealed override IEnumerable<CustomAttributeData> TrueCustomAttributes => RuntimeCustomAttributeData.GetCustomAttributes(_reader, _field.GetCustomAttributes());

        protected sealed override int ExplicitLayoutFieldOffsetData => _field.GetOffset();

        private readonly EcmaFormatRuntimeNamedTypeInfo _definingTypeInfo;
        private readonly FieldDefinitionHandle _fieldHandle;

        private readonly MetadataReader _reader;
        private FieldDefinition _field;
    }
}
