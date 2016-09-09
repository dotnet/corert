// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.ParameterInfos;

using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.MethodInfos
{
    //
    // The runtime's implementation of non-constructor MethodInfo's that represent a method definition.
    //
    internal sealed partial class RuntimeNamedMethodInfo : RuntimeMethodInfo
    {
        //
        // methodHandle    - the "tkMethodDef" that identifies the method.
        // definingType   - the "tkTypeDef" that defined the method (this is where you get the metadata reader that created methodHandle.)
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
        private RuntimeNamedMethodInfo(MethodHandle methodHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
            : base()
        {
            _common = new RuntimeMethodCommon(methodHandle, definingTypeInfo, contextTypeInfo);
        }

        public sealed override MethodAttributes Attributes
        {
            get
            {
                return _common.Attributes;
            }
        }

        public sealed override CallingConventions CallingConvention
        {
            get
            {
                return _common.CallingConvention;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.MethodBase_CustomAttributes(this);
#endif

                return _common.CustomAttributes;
            }
        }

        public sealed override MethodInfo GetGenericMethodDefinition()
        {
            if (IsGenericMethodDefinition)
                return this;
            throw new InvalidOperationException();
        }

        public sealed override bool IsGenericMethod
        {
            get
            {
                return IsGenericMethodDefinition;
            }
        }

        public sealed override bool IsGenericMethodDefinition
        {
            get
            {
                Method method = _common.MethodHandle.GetMethod(_common.Reader);
                return method.GenericParameters.GetEnumerator().MoveNext();
            }
        }

        public sealed override MethodInfo MakeGenericMethod(params Type[] typeArguments)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.MethodInfo_MakeGenericMethod(this, typeArguments);
#endif

            if (typeArguments == null)
                throw new ArgumentNullException(nameof(typeArguments));
            if (GenericTypeParameters.Length == 0)
                throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericMethodDefinition, this));
            RuntimeTypeInfo[] genericTypeArguments = new RuntimeTypeInfo[typeArguments.Length];
            for (int i = 0; i < typeArguments.Length; i++)
            {
                if (typeArguments[i] == null)
                    throw new ArgumentNullException();

                if (!typeArguments[i].IsRuntimeImplemented())
                    throw new ArgumentException(SR.Format(SR.Reflection_CustomReflectionObjectsNotSupported, typeArguments[i]), "typeArguments[" + i + "]"); // Not a runtime type.

                genericTypeArguments[i] = typeArguments[i].CastToRuntimeTypeInfo();
            }
            if (typeArguments.Length != GenericTypeParameters.Length)
                throw new ArgumentException(SR.Format(SR.Argument_NotEnoughGenArguments, typeArguments.Length, GenericTypeParameters.Length));
            RuntimeMethodInfo methodInfo = (RuntimeMethodInfo)RuntimeConstructedGenericMethodInfo.GetRuntimeConstructedGenericMethodInfo(this, genericTypeArguments);
            MethodInvoker methodInvoker = methodInfo.MethodInvoker; // For compatibility with other Make* apis, trigger any MissingMetadataExceptions now rather than later.
            return methodInfo;
        }

        public sealed override MethodImplAttributes MethodImplementationFlags
        {
            get
            {
                return _common.MethodImplementationFlags;
            }
        }

        public sealed override Module Module
        {
            get
            {
                return _common.Module;
            }
        }

        public sealed override String ToString()
        {
            return ComputeToString(this);
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeNamedMethodInfo other = obj as RuntimeNamedMethodInfo;
            if (other == null)
                return false;
            return _common.Equals(other._common);
        }

        public sealed override int GetHashCode()
        {
            return _common.GetHashCode();
        }

        internal String ComputeToString(RuntimeMethodInfo contextMethod)
        {
            return _common.ComputeToString(contextMethod, contextMethod.RuntimeGenericArgumentsOrParameters);
        }

        internal MethodHandle Handle
        {
            get
            {
                return _common.MethodHandle;
            }
        }

        internal MetadataReader Reader
        {
            get
            {
                return _common.Reader;
            }
        }

        internal sealed override RuntimeTypeInfo[] RuntimeGenericArgumentsOrParameters
        {
            get
            {
                return this.GenericTypeParameters;
            }
        }

        internal sealed override RuntimeParameterInfo[] GetRuntimeParameters(RuntimeMethodInfo contextMethod, out RuntimeParameterInfo returnParameter)
        {
            return _common.GetRuntimeParameters(contextMethod, contextMethod.RuntimeGenericArgumentsOrParameters, out returnParameter);
        }

        internal sealed override RuntimeTypeInfo RuntimeDeclaringType
        {
            get
            {
                return _common.DeclaringType;
            }
        }

        internal sealed override String RuntimeName
        {
            get
            {
                return _common.Name;
            }
        }

        private RuntimeTypeInfo[] GenericTypeParameters
        {
            get
            {
                Method method = _common.MethodHandle.GetMethod(_common.Reader);
                int genericParametersCount = method.GenericParameters.Count;
                if (genericParametersCount == 0)
                    return Array.Empty<RuntimeTypeInfo>();

                RuntimeTypeInfo[] genericTypeParameters = new RuntimeTypeInfo[genericParametersCount];
                int i = 0;
                foreach (GenericParameterHandle genericParameterHandle in method.GenericParameters)
                {
                    RuntimeNamedMethodInfo owningMethod = this;
                    if (DeclaringType.IsConstructedGenericType)
                    {
                        // Desktop compat: Constructed generic types and their generic type definitions share the same Type objects for method generic parameters. 
                        RuntimeNamedTypeInfo genericTypeDefinition = DeclaringType.GetGenericTypeDefinition().CastToRuntimeNamedTypeInfo();
                        owningMethod = RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(Handle, genericTypeDefinition, genericTypeDefinition);
                    }
                    RuntimeTypeInfo genericParameterType = RuntimeGenericParameterTypeInfoForMethods.GetRuntimeGenericParameterTypeInfoForMethods(owningMethod, owningMethod._common.Reader, genericParameterHandle);
                    genericTypeParameters[i++] = genericParameterType;
                }
                return genericTypeParameters;
            }
        }

        protected sealed override MethodInvoker UncachedMethodInvoker
        {
            get
            {
                return ReflectionCoreExecution.ExecutionEnvironment.GetMethodInvoker(_common.Reader, _common.DeclaringType, _common.MethodHandle, Array.Empty<RuntimeTypeInfo>(), this);
            }
        }

        private readonly RuntimeMethodCommon _common;
    }
}
