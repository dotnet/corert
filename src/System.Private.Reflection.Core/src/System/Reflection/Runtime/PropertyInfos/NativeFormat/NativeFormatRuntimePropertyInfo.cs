﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.NativeFormat;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.NativeFormat;
using System.Reflection.Runtime.ParameterInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;
using NativeFormatMethodSemanticsAttributes = global::Internal.Metadata.NativeFormat.MethodSemanticsAttributes;

namespace System.Reflection.Runtime.PropertyInfos.NativeFormat
{
    //
    // The runtime's implementation of PropertyInfo's
    //
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class NativeFormatRuntimePropertyInfo : RuntimePropertyInfo
    {
        //
        // propertyHandle - the "tkPropertyDef" that identifies the property.
        // definingType   - the "tkTypeDef" that defined the field (this is where you get the metadata reader that created propertyHandle.)
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
        private NativeFormatRuntimePropertyInfo(PropertyHandle propertyHandle, NativeFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType) :
            base(contextTypeInfo, reflectedType)
        {
            _propertyHandle = propertyHandle;
            _definingTypeInfo = definingTypeInfo;
            _reader = definingTypeInfo.Reader;
            _property = propertyHandle.GetProperty(_reader);
        }

        public sealed override PropertyAttributes Attributes
        {
            get
            {
                return _property.Flags;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.PropertyInfo_CustomAttributes(this);
#endif

                foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(_reader, _property.CustomAttributes))
                    yield return cad;
                foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _propertyHandle, _definingTypeInfo.TypeDefinitionHandle))
                    yield return cad;
            }
        }

        public sealed override bool Equals(Object obj)
        {
            NativeFormatRuntimePropertyInfo other = obj as NativeFormatRuntimePropertyInfo;
            if (other == null)
                return false;
            if (!(_reader == other._reader))
                return false;
            if (!(_propertyHandle.Equals(other._propertyHandle)))
                return false;
            if (!(_contextTypeInfo.Equals(other._contextTypeInfo)))
                return false;
            if (!(_reflectedType.Equals(other._reflectedType)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _propertyHandle.GetHashCode();
        }

        public sealed override Object GetConstantValue()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.PropertyInfo_GetConstantValue(this);
#endif

            Object defaultValue;
            if (!ReflectionCoreExecution.ExecutionEnvironment.GetDefaultValueIfAny(
                _reader,
                _propertyHandle,
                this.PropertyType,
                this.CustomAttributes,
                out defaultValue))
            {
                throw new InvalidOperationException();
            }
            return defaultValue;
        }

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
            }
        }

        internal protected override QTypeDefRefOrSpec PropertyTypeHandle
        {
            get
            {
                return new QTypeDefRefOrSpec(_reader, _property.Signature.GetPropertySignature(_reader).Type);
            }
        }

        internal protected sealed override RuntimeNamedMethodInfo GetPropertyMethod(PropertyMethodSemantics whichMethod)
        {
            NativeFormatMethodSemanticsAttributes localMethodSemantics;
            switch (whichMethod)
            {
                case PropertyMethodSemantics.Getter:
                    localMethodSemantics = NativeFormatMethodSemanticsAttributes.Getter;
                    break;

                case PropertyMethodSemantics.Setter:
                    localMethodSemantics = NativeFormatMethodSemanticsAttributes.Setter;
                    break;

                default:
                    return null;
            }

            bool inherited = !_reflectedType.Equals(_contextTypeInfo);

            foreach (MethodSemanticsHandle methodSemanticsHandle in _property.MethodSemantics)
            {
                MethodSemantics methodSemantics = methodSemanticsHandle.GetMethodSemantics(_reader);
                if (methodSemantics.Attributes == localMethodSemantics)
                {
                    MethodHandle methodHandle = methodSemantics.Method;

                    if (inherited)
                    {
                        MethodAttributes flags = methodHandle.GetMethod(_reader).Flags;
                        if ((flags & MethodAttributes.MemberAccessMask) == MethodAttributes.Private)
                            continue;
                    }

                    return RuntimeNamedMethodInfoWithMetadata<NativeFormatMethodCommon>.GetRuntimeNamedMethodInfo(new NativeFormatMethodCommon(methodHandle, _definingTypeInfo, _contextTypeInfo), _reflectedType);
                }
            }
            
            return null;
        }

        internal protected sealed override string MetadataName
        {
            get
            {
                return _property.Name.GetString(_reader);
            }
        }

        internal sealed protected override RuntimeTypeInfo DefiningTypeInfo
        {
            get
            {
                return _definingTypeInfo;
            }
        }

        private readonly NativeFormatRuntimeNamedTypeInfo _definingTypeInfo;
        private readonly PropertyHandle _propertyHandle;

        private readonly MetadataReader _reader;
        private readonly Property _property;
    }
}
