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

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.MethodInfos
{
    //
    // These methods implement the Get/Set methods on array types.
    //
    internal sealed partial class RuntimeSyntheticMethodInfo : RuntimeMethodInfo
    {
        private RuntimeSyntheticMethodInfo(SyntheticMethodId syntheticMethodId, String name, RuntimeTypeInfo declaringType, RuntimeTypeInfo[] parameterTypes, RuntimeTypeInfo returnType, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            _syntheticMethodId = syntheticMethodId;
            _name = name;
            _declaringType = declaringType;
            _options = options;
            _invoker = invoker;
            _runtimeParameterTypes = parameterTypes;
            _returnType = returnType;
        }

        public sealed override MethodAttributes Attributes
        {
            get
            {
                return MethodAttributes.Public | MethodAttributes.PrivateScope;
            }
        }

        public sealed override CallingConventions CallingConvention
        {
            get
            {
                return CallingConventions.Standard | CallingConventions.HasThis;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return Empty<CustomAttributeData>.Enumerable;
            }
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeSyntheticMethodInfo other = obj as RuntimeSyntheticMethodInfo;
            if (other == null)
                return false;
            if (_syntheticMethodId != other._syntheticMethodId)
                return false;
            if (!(_declaringType.Equals(other._declaringType)))
                return false;
            return true;
        }

        public sealed override MethodInfo GetGenericMethodDefinition()
        {
            throw new InvalidOperationException();
        }

        public sealed override int GetHashCode()
        {
            return _declaringType.GetHashCode();
        }

        public sealed override bool IsGenericMethod
        {
            get
            {
                return false;
            }
        }

        public sealed override bool IsGenericMethodDefinition
        {
            get
            {
                return false;
            }
        }

        public sealed override MethodInfo MakeGenericMethod(params Type[] typeArguments)
        {
            throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericMethodDefinition, this));
        }

        public sealed override MethodImplAttributes MethodImplementationFlags
        {
            get
            {
                return MethodImplAttributes.IL;
            }
        }

        public sealed override Module Module
        {
            get
            {
                return this.DeclaringType.GetTypeInfo().Assembly.ManifestModule;
            }
        }

        public sealed override String ToString()
        {
            return RuntimeMethodCommon.ComputeToString(this, Array.Empty<RuntimeTypeInfo>(), RuntimeParameters, RuntimeReturnParameter);
        }

        protected sealed override MethodInvoker UncachedMethodInvoker
        {
            get
            {
                RuntimeTypeInfo[] runtimeParameterTypes = _runtimeParameterTypes;
                RuntimeTypeHandle[] runtimeParameterTypeHandles = new RuntimeTypeHandle[runtimeParameterTypes.Length];
                for (int i = 0; i < runtimeParameterTypes.Length; i++)
                    runtimeParameterTypeHandles[i] = runtimeParameterTypes[i].TypeHandle;
                return ReflectionCoreExecution.ExecutionEnvironment.GetSyntheticMethodInvoker(
                    _declaringType.TypeHandle,
                    runtimeParameterTypeHandles,
                    _options,
                    _invoker);
            }
        }

        internal sealed override RuntimeTypeInfo[] RuntimeGenericArgumentsOrParameters
        {
            get
            {
                return Array.Empty<RuntimeTypeInfo>();
            }
        }

        internal sealed override RuntimeTypeInfo RuntimeDeclaringType
        {
            get
            {
                return _declaringType;
            }
        }

        internal sealed override String RuntimeName
        {
            get
            {
                return _name;
            }
        }

        internal sealed override RuntimeParameterInfo[] GetRuntimeParameters(RuntimeMethodInfo contextMethod, out RuntimeParameterInfo returnParameter)
        {
            RuntimeTypeInfo[] runtimeParameterTypes = _runtimeParameterTypes;
            RuntimeParameterInfo[] parameters = new RuntimeParameterInfo[runtimeParameterTypes.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameters[i] = RuntimeSyntheticParameterInfo.GetRuntimeSyntheticParameterInfo(this, i, runtimeParameterTypes[i]);
            }
            returnParameter = RuntimeSyntheticParameterInfo.GetRuntimeSyntheticParameterInfo(this, -1, _returnType);
            return parameters;
        }

        private readonly String _name;
        private readonly SyntheticMethodId _syntheticMethodId;
        private readonly RuntimeTypeInfo _declaringType;
        private readonly RuntimeTypeInfo[] _runtimeParameterTypes;
        private readonly RuntimeTypeInfo _returnType;
        private readonly InvokerOptions _options;
        private readonly Func<Object, Object[], Object> _invoker;
    }
}
