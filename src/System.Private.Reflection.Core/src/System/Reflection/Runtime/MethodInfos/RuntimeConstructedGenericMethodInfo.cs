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
    // The runtime's implementation of non-constructor MethodInfo's that represent an open or closed costruction of a generic method.
    //
    internal sealed partial class RuntimeConstructedGenericMethodInfo : RuntimeMethodInfo
    {
        private RuntimeConstructedGenericMethodInfo(RuntimeNamedMethodInfo genericMethodDefinition, RuntimeTypeInfo[] genericTypeArguments)
        {
            _genericMethodDefinition = genericMethodDefinition;
            _genericTypeArguments = genericTypeArguments;
        }

        public sealed override MethodAttributes Attributes
        {
            get
            {
                return _genericMethodDefinition.Attributes;
            }
        }

        public sealed override CallingConventions CallingConvention
        {
            get
            {
                return _genericMethodDefinition.CallingConvention;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return _genericMethodDefinition.CustomAttributes;
            }
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeConstructedGenericMethodInfo other = obj as RuntimeConstructedGenericMethodInfo;
            if (other == null)
                return false;
            if (!_genericMethodDefinition.Equals(other._genericMethodDefinition))
                return false;
            if (_genericTypeArguments.Length != other._genericTypeArguments.Length)
                return false;
            for (int i = 0; i < _genericTypeArguments.Length; i++)
            {
                if (!_genericTypeArguments[i].Equals(other._genericTypeArguments[i]))
                    return false;
            }
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _genericMethodDefinition.GetHashCode();
        }

        public sealed override MethodInfo GetGenericMethodDefinition()
        {
            return _genericMethodDefinition;
        }

        public sealed override bool IsGenericMethod
        {
            get
            {
                return true;
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
                return _genericMethodDefinition.MethodImplementationFlags;
            }
        }

        public sealed override Module Module
        {
            get
            {
                return _genericMethodDefinition.Module;
            }
        }

        public sealed override String ToString()
        {
            return _genericMethodDefinition.ComputeToString(this);
        }

        protected sealed override MethodInvoker UncachedMethodInvoker
        {
            get
            {
                return ReflectionCoreExecution.ExecutionEnvironment.GetMethodInvoker(
                        _genericMethodDefinition.Reader,
                        _genericMethodDefinition.RuntimeDeclaringType,
                        _genericMethodDefinition.Handle,
                        _genericTypeArguments,
                        this);
            }
        }

        internal sealed override RuntimeTypeInfo RuntimeDeclaringType
        {
            get
            {
                return _genericMethodDefinition.RuntimeDeclaringType;
            }
        }

        internal sealed override RuntimeTypeInfo[] RuntimeGenericArgumentsOrParameters
        {
            get
            {
                return _genericTypeArguments;
            }
        }

        internal sealed override String RuntimeName
        {
            get
            {
                return _genericMethodDefinition.RuntimeName;
            }
        }

        internal sealed override RuntimeParameterInfo[] GetRuntimeParameters(RuntimeMethodInfo contextMethod, out RuntimeParameterInfo returnParameter)
        {
            return _genericMethodDefinition.GetRuntimeParameters(this, out returnParameter);
        }

        private readonly RuntimeNamedMethodInfo _genericMethodDefinition;
        private readonly RuntimeTypeInfo[] _genericTypeArguments;
    }
}

