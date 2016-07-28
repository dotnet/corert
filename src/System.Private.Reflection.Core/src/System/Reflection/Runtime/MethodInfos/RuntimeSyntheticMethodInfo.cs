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
        private RuntimeSyntheticMethodInfo(SyntheticMethodId syntheticMethodId, String name, RuntimeTypeInfo declaringType, RuntimeTypeInfo[] runtimeParameterTypesAndReturn, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            _syntheticMethodId = syntheticMethodId;
            _name = name;
            _declaringType = declaringType;
            _options = options;
            _invoker = invoker;
            _runtimeParameterTypesAndReturn = runtimeParameterTypesAndReturn;
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
            return RuntimeMethodCommon.ComputeToString(this, Array.Empty<RuntimeTypeInfo>(), GetRuntimeParametersAndReturn(this));
        }

        protected sealed override MethodInvoker UncachedMethodInvoker
        {
            get
            {
                RuntimeTypeHandle[] runtimeParameterTypeHandles = new RuntimeTypeHandle[_runtimeParameterTypesAndReturn.Length - 1];
                for (int i = 1; i < _runtimeParameterTypesAndReturn.Length; i++)
                    runtimeParameterTypeHandles[i - 1] = _runtimeParameterTypesAndReturn[i].TypeHandle;
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

        internal sealed override RuntimeParameterInfo[] GetRuntimeParametersAndReturn(RuntimeMethodInfo contextMethod)
        {
            RuntimeParameterInfo[] runtimeParametersAndReturn = _lazyRuntimeParametersAndReturn;
            if (runtimeParametersAndReturn == null)
            {
                runtimeParametersAndReturn = new RuntimeParameterInfo[_runtimeParameterTypesAndReturn.Length];
                for (int i = 0; i < runtimeParametersAndReturn.Length; i++)
                {
                    runtimeParametersAndReturn[i] = RuntimeSyntheticParameterInfo.GetRuntimeSyntheticParameterInfo(this, i - 1, _runtimeParameterTypesAndReturn[i]);
                }
                _lazyRuntimeParametersAndReturn = runtimeParametersAndReturn;
            }
            return runtimeParametersAndReturn;
        }

        private volatile RuntimeParameterInfo[] _lazyRuntimeParametersAndReturn;

        private readonly String _name;
        private readonly SyntheticMethodId _syntheticMethodId;
        private readonly RuntimeTypeInfo _declaringType;
        private readonly RuntimeTypeInfo[] _runtimeParameterTypesAndReturn;
        private readonly InvokerOptions _options;
        private readonly Func<Object, Object[], Object> _invoker;
    }
}
