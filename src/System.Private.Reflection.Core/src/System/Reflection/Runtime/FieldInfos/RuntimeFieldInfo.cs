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

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.FieldInfos
{
    //
    // The Runtime's implementation of fields.
    //
    [DebuggerDisplay("{_debugName}")]
    internal abstract partial class RuntimeFieldInfo : FieldInfo, ITraceableTypeMember
    {
        //
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
        protected RuntimeFieldInfo(RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType)
        {
            _contextTypeInfo = contextTypeInfo;
            _reflectedType = reflectedType;
        }

        public sealed override Type DeclaringType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.FieldInfo_DeclaringType(this);
#endif

                return _contextTypeInfo;
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

        public sealed override Module Module
        {
            get
            {
                return DefiningType.Module;
            }
        }

        public sealed override Type ReflectedType
        {
            get
            {
                return _reflectedType;
            }
        }

        public sealed override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.FieldInfo_SetValue(this, obj, value);
#endif

            binder.EnsureNotCustomBinder();

            FieldAccessor fieldAccessor = this.FieldAccessor;
            fieldAccessor.SetField(obj, value);
        }

        Type ITraceableTypeMember.ContainingType
        {
            get
            {
                return _contextTypeInfo;
            }
        }

        protected abstract string MetadataName { get; }

        public sealed override String Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.FieldInfo_Name(this);
#endif

                return MetadataName;
            }
        }

        String ITraceableTypeMember.MemberName
        {
            get
            {
                return MetadataName;
            }
        }

        protected abstract bool TryGetDefaultValue(out object defaultValue);
        protected abstract FieldAccessor TryGetFieldAccessor();


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
                        if (!TryGetDefaultValue(out defaultValue))
                        {
                            throw new BadImageFormatException(); // Field marked literal but has no default value.
                        }

                        _lazyFieldAccessor = fieldAccessor = new LiteralFieldAccessor(defaultValue);
                    }
                    else
                    {
                        _lazyFieldAccessor = fieldAccessor = TryGetFieldAccessor();
                        if (fieldAccessor == null)
                            throw ReflectionCoreExecution.ExecutionDomain.CreateNonInvokabilityException(this);
                    }
                }
                return fieldAccessor;
            }
        }

        protected abstract RuntimeTypeInfo FieldRuntimeType { get; }

        protected RuntimeFieldInfo WithDebugName()
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

        protected abstract RuntimeTypeInfo DefiningType { get; }

        protected readonly RuntimeTypeInfo _contextTypeInfo;
        protected readonly RuntimeTypeInfo _reflectedType;

        private volatile FieldAccessor _lazyFieldAccessor = null;

        private String _debugName;
    }
}
