// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using System.Reflection.Runtime.FieldInfos;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.NativeFormat;
using System.Reflection.Runtime.CustomAttributes;
using System.Reflection.Runtime.BindingFlagSupport;

using Internal.Metadata.NativeFormat;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;

using Internal.Runtime.TypeLoader;

namespace System.Reflection.Runtime.FieldInfos.NativeFormat
{
    //
    // The Runtime's implementation of fields.
    //
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class NativeFormatRuntimeFieldInfo : RuntimeFieldInfo
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
        private NativeFormatRuntimeFieldInfo(FieldHandle fieldHandle, NativeFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType) :
            base(contextTypeInfo, reflectedType)
        {
            _fieldHandle = fieldHandle;
            _definingTypeInfo = definingTypeInfo;
            _reader = definingTypeInfo.Reader;
            _field = fieldHandle.GetField(_reader);
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.FieldInfo_CustomAttributes(this);
#endif

                IEnumerable<CustomAttributeData> customAttributes = RuntimeCustomAttributeData.GetCustomAttributes(_reader, _field.CustomAttributes);
                foreach (CustomAttributeData cad in customAttributes)
                    yield return cad;
                foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPseudoCustomAttributes(_reader, _fieldHandle, _definingTypeInfo.TypeDefinitionHandle))
                    yield return cad;
            }
        }

        public sealed override FieldAttributes Attributes
        {
            get
            {
                return _field.Flags;
            }
        }

        public sealed override Type[] GetOptionalCustomModifiers() => FieldTypeHandle.GetCustomModifiers(_reader, _contextTypeInfo.TypeContext, optional: true);

        public sealed override Type[] GetRequiredCustomModifiers() => FieldTypeHandle.GetCustomModifiers(_reader, _contextTypeInfo.TypeContext, optional: false);

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
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
            TypeContext typeContext = _contextTypeInfo.TypeContext;
            Handle typeHandle = _field.Signature.GetFieldSignature(_reader).Type;
            return (new QTypeDefRefOrSpec(_reader, typeHandle).FormatTypeName(typeContext)) + " " + this.Name;
        }

        public sealed override bool HasSameMetadataDefinitionAs(MemberInfo other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            NativeFormatRuntimeFieldInfo otherField = other as NativeFormatRuntimeFieldInfo;
            if (otherField == null)
                return false;
            if (!(_reader == otherField._reader))
                return false;
            if (!(_fieldHandle.Equals(otherField._fieldHandle)))
                return false;
            if (!(_definingTypeInfo.Equals(otherField._definingTypeInfo)))
                return false;
            return true;
        }

        public sealed override bool Equals(Object obj)
        {
            NativeFormatRuntimeFieldInfo other = obj as NativeFormatRuntimeFieldInfo;
            if (other == null)
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

        protected sealed override bool TryGetDefaultValue(out object defaultValue)
        {
            return ReflectionCoreExecution.ExecutionEnvironment.GetDefaultValueIfAny(
                            _reader,
                            _fieldHandle,
                            this.FieldType,
                            this.CustomAttributes,
                            out defaultValue);
        }

        protected sealed override FieldAccessor TryGetFieldAccessor()
        {
            return ReflectionCoreExecution.ExecutionEnvironment.TryGetFieldAccessor(this._reader, this.DeclaringType.TypeHandle, this.FieldType.TypeHandle, _fieldHandle);
        }

        protected sealed override RuntimeTypeInfo FieldRuntimeType
        {
            get
            {
                TypeContext typeContext = _contextTypeInfo.TypeContext;
                return FieldTypeHandle.Resolve(_reader, typeContext);
            }
        }

        protected sealed override RuntimeTypeInfo DefiningType { get { return _definingTypeInfo; } }

        private Handle FieldTypeHandle => _field.Signature.GetFieldSignature(_reader).Type;

        private readonly NativeFormatRuntimeNamedTypeInfo _definingTypeInfo;
        private readonly FieldHandle _fieldHandle;

        private readonly MetadataReader _reader;
        private readonly Field _field;
    }
}
