// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract class RuntimeGenericParameterTypeInfo : RuntimeTypeInfo
    {
        protected RuntimeGenericParameterTypeInfo(MetadataReader reader, GenericParameterHandle genericParameterHandle)
        {
            Reader = reader;
            GenericParameterHandle = genericParameterHandle;
            _genericParameter = genericParameterHandle.GetGenericParameter(reader);
            _position = _genericParameter.Number;
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return DeclaringType.GetTypeInfo().Assembly;
            }
        }

        public sealed override bool ContainsGenericParameters
        {
            get
            {
                return true;
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

                return RuntimeCustomAttributeData.GetCustomAttributes(Reader, _genericParameter.CustomAttributes);
            }
        }

        public abstract override MethodBase DeclaringMethod { get; }

        public sealed override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                return _genericParameter.Flags;
            }
        }

        public sealed override Type[] GetGenericParameterConstraints()
        {
            return ConstraintInfos.CloneTypeArray();
        }

        public sealed override string FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_FullName(this);
#endif
                return null;  // We return null as generic parameter types are not roundtrippable through Type.GetType().
            }
        }

        public sealed override int GenericParameterPosition
        {
            get
            {
                return _position;
            }
        }

        public sealed override bool IsGenericParameter
        {
            get
            {
                return true;
            }
        }

        public sealed override string Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_Namespace(this);
#endif
                return DeclaringType.Namespace;
            }
        }

        public sealed override StructLayoutAttribute StructLayoutAttribute
        {
            get
            {
                return null;
            }
        }

        public sealed override string ToString()
        {
            return Name;
        }

        protected sealed override TypeAttributes GetAttributeFlagsImpl()
        {
            return TypeAttributes.Public;
        }

        protected sealed override int InternalGetHashCode()
        {
            return GenericParameterHandle.GetHashCode();
        }

        protected GenericParameterHandle GenericParameterHandle { get; }

        protected MetadataReader Reader { get; }

        internal sealed override string InternalFullNameOfAssembly
        {
            get
            {
                Debug.Fail("Since this class always returns null for FullName, this helper should be unreachable.");
                return null;
            }
        }

        internal sealed override string InternalGetNameIfAvailable(ref Type rootCauseForFailure)
        {
            if (_genericParameter.Name.IsNull(Reader))
                return string.Empty;
            return _genericParameter.Name.GetString(Reader);
        }

        internal sealed override RuntimeTypeHandle InternalTypeHandleIfAvailable
        {
            get
            {
                return default(RuntimeTypeHandle);
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

                RuntimeNamedTypeInfo objectTypeInfo = ReflectionCoreExecution.ExecutionDomain.FoundationTypes.SystemObject.CastToRuntimeNamedTypeInfo();
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
        internal abstract override TypeContext TypeContext { get; }

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

        private TypeInfo[] ConstraintInfos
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
        private readonly int _position;
    }
}

