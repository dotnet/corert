// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.CustomAttributes;
using System.Reflection.Runtime.BindingFlagSupport;

using Internal.Metadata.NativeFormat;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.FieldInfos
{
    //
    // The Runtime's implementation of fields.
    //
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class RuntimeFieldInfo : FieldInfo, ITraceableTypeMember
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
        private RuntimeFieldInfo(FieldHandle fieldHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            _fieldHandle = fieldHandle;
            _definingTypeInfo = definingTypeInfo;
            _contextTypeInfo = contextTypeInfo;
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
                foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _fieldHandle, _definingTypeInfo.TypeDefinitionHandle))
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

        public sealed override Type DeclaringType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.FieldInfo_DeclaringType(this);
#endif

                return _contextTypeInfo.AsType();
            }
        }

        public sealed override Type FieldType
        {
            get
            {
                return this.FieldRuntimeType;
            }
        }

        public sealed override Object GetValue(Object obj)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.FieldInfo_GetValue(this, obj);
#endif

            FieldAccessor fieldAccessor = this.FieldAccessor;
            return fieldAccessor.GetField(obj);
        }

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
            }
        }

        public sealed override Module Module
        {
            get
            {
                return _definingTypeInfo.Module;
            }
        }

        public sealed override String Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.FieldInfo_Name(this);
#endif

                return _field.Name.GetString(_reader);
            }
        }

        public sealed override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.FieldInfo_SetValue(this, obj, value);
#endif

            if (invokeAttr != BindingFlags.Default || !(binder is DefaultBinder) || culture != null)
                throw new NotImplementedException();

            FieldAccessor fieldAccessor = this.FieldAccessor;
            fieldAccessor.SetField(obj, value);
        }

        public sealed override String ToString()
        {
            TypeContext typeContext = _contextTypeInfo.TypeContext;
            Handle typeHandle = _field.Signature.GetFieldSignature(_reader).Type;
            return typeHandle.FormatTypeName(_reader, typeContext) + " " + this.Name;
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeFieldInfo other = obj as RuntimeFieldInfo;
            if (other == null)
                return false;
            if (!(_reader == other._reader))
                return false;
            if (!(_fieldHandle.Equals(other._fieldHandle)))
                return false;
            if (!(_contextTypeInfo.Equals(other._contextTypeInfo)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _fieldHandle.GetHashCode();
        }

        String ITraceableTypeMember.MemberName
        {
            get
            {
                return _field.Name.GetString(_reader);
            }
        }

        Type ITraceableTypeMember.ContainingType
        {
            get
            {
                return _contextTypeInfo.AsType();
            }
        }

        private FieldAccessor FieldAccessor
        {
            get
            {
                FieldAccessor fieldAccessor = _lazyFieldAccessor;
                if (fieldAccessor == null)
                {
                    if (this.IsLiteral)
                    {
                        // Legacy: ECMA335 does not require that the metadata literal match the type of the field that declares it.
                        // For desktop compat, we return the metadata literal as is and do not attempt to convert or validate against the Field type.

                        Object defaultValue;
                        if (!ReflectionCoreExecution.ExecutionEnvironment.GetDefaultValueIfAny(
                            _reader,
                            _fieldHandle,
                            this.FieldType,
                            this.CustomAttributes,
                            out defaultValue))
                        {
                            throw new BadImageFormatException(); // Field marked literal but has no default value.
                        }

                        _lazyFieldAccessor = fieldAccessor = new LiteralFieldAccessor(defaultValue);
                    }
                    else
                    {
                        _lazyFieldAccessor = fieldAccessor = ReflectionCoreExecution.ExecutionEnvironment.TryGetFieldAccessor(this.DeclaringType.TypeHandle, this.FieldType.TypeHandle, _fieldHandle);
                        if (fieldAccessor == null)
                            throw ReflectionCoreExecution.ExecutionDomain.CreateNonInvokabilityException(this);
                    }
                }
                return fieldAccessor;
            }
        }

        private RuntimeTypeInfo FieldRuntimeType
        {
            get
            {
                TypeContext typeContext = _contextTypeInfo.TypeContext;
                Handle typeHandle = _field.Signature.GetFieldSignature(_reader).Type;
                return typeHandle.Resolve(_reader, typeContext);
            }
        }

        private RuntimeFieldInfo WithDebugName()
        {
            bool populateDebugNames = DeveloperExperienceState.DeveloperExperienceModeEnabled;
#if DEBUG
            populateDebugNames = true;
#endif
            if (!populateDebugNames)
                return this;

            if (_debugName == null)
            {
                _debugName = "Constructing..."; // Protect against any inadvertent reentrancy.
                _debugName = ((ITraceableTypeMember)this).MemberName;
            }
            return this;
        }

        private readonly RuntimeNamedTypeInfo _definingTypeInfo;
        private readonly FieldHandle _fieldHandle;
        private readonly RuntimeTypeInfo _contextTypeInfo;

        private readonly MetadataReader _reader;
        private readonly Field _field;

        private volatile FieldAccessor _lazyFieldAccessor = null;

        private String _debugName;
    }
}
