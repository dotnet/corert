// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.MethodInfos;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Reflection.Tracing;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    internal sealed partial class RuntimeGenericParameterTypeInfo : RuntimeTypeInfo
    {
        private RuntimeGenericParameterTypeInfo(RuntimeGenericParameterType asType)
        {
            _asType = asType;
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return DeclaringType.GetTypeInfo().Assembly;
            }
        }

        public sealed override TypeAttributes Attributes
        {
            get
            {
                return TypeAttributes.Public;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_CustomAttributes(this);
#endif

                return _asType.CustomAttributes;
            }
        }

        public sealed override MethodBase DeclaringMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaringMethod(this);
#endif

                return _asType.DeclaringMethod;
            }
        }

        public sealed override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                return _asType.GenericParameterAttributes;
            }
        }

        public sealed override Type[] GetGenericParameterConstraints()
        {
            TypeInfo[] constraintInfos = ConstraintInfos;
            if (constraintInfos.Length == 0)
                return Array.Empty<Type>();
            Type[] result = new Type[constraintInfos.Length];
            for (int i = 0; i < constraintInfos.Length; i++)
                result[i] = constraintInfos[i].AsType();
            return result;
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeGenericParameterTypeInfo other = obj as RuntimeGenericParameterTypeInfo;
            if (other == null)
                return false;
            return this._asType.Equals(other._asType);
        }

        public sealed override int GetHashCode()
        {
            return _asType.GetHashCode();
        }

        internal sealed override RuntimeType RuntimeType
        {
            get
            {
                return _asType;
            }
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

                RuntimeNamedTypeInfo objectTypeInfo = this.ReflectionDomain.FoundationTypes.SystemObject.GetRuntimeTypeInfo<RuntimeNamedTypeInfo>();
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

        //
        // Returns the generic parameter substitutions to use when enumerating declared members, base class and implemented interfaces.
        //
        internal sealed override TypeContext TypeContext
        {
            get
            {
                return _asType.TypeContext;
            }
        }

        private QTypeDefRefOrSpec[] Constraints
        {
            get
            {
                MetadataReader reader = _asType.Reader;
                LowLevelList<QTypeDefRefOrSpec> constraints = new LowLevelList<QTypeDefRefOrSpec>();
                foreach (Handle constraintHandle in _asType.GenericParameterHandle.GetGenericParameter(_asType.Reader).Constraints)
                {
                    constraints.Add(new QTypeDefRefOrSpec(reader, constraintHandle));
                }
                return constraints.ToArray();
            }
        }

        private TypeInfo[] ConstraintInfos
        {
            get
            {
                QTypeDefRefOrSpec[] constraints = Constraints;
                if (constraints.Length == 0)
                    return Array.Empty<TypeInfo>();
                TypeInfo[] constraintInfos = new TypeInfo[constraints.Length];
                ReflectionDomain reflectionDomain = this.ReflectionDomain;
                for (int i = 0; i < constraints.Length; i++)
                {
                    constraintInfos[i] = reflectionDomain.Resolve(constraints[i].Reader, constraints[i].Handle, TypeContext).GetTypeInfo();
                }
                return constraintInfos;
            }
        }

        private RuntimeGenericParameterType _asType;
    }
}

