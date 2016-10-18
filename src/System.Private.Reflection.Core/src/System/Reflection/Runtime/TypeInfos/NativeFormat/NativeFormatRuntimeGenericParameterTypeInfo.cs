// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos.NativeFormat
{
    internal abstract partial class NativeFormatRuntimeGenericParameterTypeInfo : RuntimeGenericParameterTypeInfo
    {
        protected NativeFormatRuntimeGenericParameterTypeInfo(MetadataReader reader, GenericParameterHandle genericParameterHandle, GenericParameter genericParameter)
            : base(genericParameter.Number)
        {
            Reader = reader;
            GenericParameterHandle = genericParameterHandle;
            _genericParameter = genericParameter;
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_CustomAttributes(this);
#endif

                return RuntimeCustomAttributeData.GetCustomAttributes(Reader, _genericParameter.CustomAttributes);
            }
        }

        public sealed override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                return _genericParameter.Flags;
            }
        }

        protected sealed override int InternalGetHashCode()
        {
            return GenericParameterHandle.GetHashCode();
        }

        protected GenericParameterHandle GenericParameterHandle { get; }

        protected MetadataReader Reader { get; }

        internal sealed override string InternalGetNameIfAvailable(ref Type rootCauseForFailure)
        {
            if (_genericParameter.Name.IsNull(Reader))
                return string.Empty;
            return _genericParameter.Name.GetString(Reader);
        }

        //
        // Returns the base type as a typeDef, Ref, or Spec. Default behavior is to QTypeDefRefOrSpec.Null, which causes BaseType to return null.
        //
        internal sealed override QTypeDefRefOrSpec TypeRefDefOrSpecForBaseType
        {
            get
            {
                QTypeDefRefOrSpec[] constraints = Constraints;
                TypeInfo[] constraintInfos = ConstraintInfos;
                for (int i = 0; i < constraints.Length; i++)
                {
                    TypeInfo constraintInfo = constraintInfos[i];
                    if (constraintInfo.IsInterface)
                        continue;
                    return constraints[i];
                }

                RuntimeNamedTypeInfo objectTypeInfo = CommonRuntimeTypes.Object.CastToRuntimeNamedTypeInfo();
                return new QTypeDefRefOrSpec(objectTypeInfo.Reader, objectTypeInfo.TypeDefinitionHandle);
            }
        }

        //
        // Returns the *directly implemented* interfaces as typedefs, specs or refs. ImplementedInterfaces will take care of the transitive closure and
        // insertion of the TypeContext.
        //
        internal sealed override QTypeDefRefOrSpec[] TypeRefDefOrSpecsForDirectlyImplementedInterfaces
        {
            get
            {
                LowLevelList<QTypeDefRefOrSpec> result = new LowLevelList<QTypeDefRefOrSpec>();
                QTypeDefRefOrSpec[] constraints = Constraints;
                TypeInfo[] constraintInfos = ConstraintInfos;
                for (int i = 0; i < constraints.Length; i++)
                {
                    if (constraintInfos[i].IsInterface)
                        result.Add(constraints[i]);
                }
                return result.ToArray();
            }
        }

        private QTypeDefRefOrSpec[] Constraints
        {
            get
            {
                MetadataReader reader = Reader;
                LowLevelList<QTypeDefRefOrSpec> constraints = new LowLevelList<QTypeDefRefOrSpec>();
                foreach (Handle constraintHandle in GenericParameterHandle.GetGenericParameter(reader).Constraints)
                {
                    constraints.Add(new QTypeDefRefOrSpec(reader, constraintHandle));
                }
                return constraints.ToArray();
            }
        }

        protected sealed override TypeInfo[] ConstraintInfos
        {
            get
            {
                QTypeDefRefOrSpec[] constraints = Constraints;
                if (constraints.Length == 0)
                    return Array.Empty<TypeInfo>();
                TypeInfo[] constraintInfos = new TypeInfo[constraints.Length];
                for (int i = 0; i < constraints.Length; i++)
                {
                    constraintInfos[i] = constraints[i].Handle.Resolve(constraints[i].Reader, TypeContext);
                }
                return constraintInfos;
            }
        }

        private readonly GenericParameter _genericParameter;
    }
}
