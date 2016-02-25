// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.ParameterInfos;

using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;
using global::Internal.Reflection.Extensibility;

using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.MethodInfos
{
    //
    // The runtime's implementation of ConstructorInfo.
    //
    internal abstract partial class RuntimeConstructorInfo : ExtensibleConstructorInfo
    {
        public abstract override MethodAttributes Attributes { get; }

        public abstract override CallingConventions CallingConvention { get; }

        public sealed override bool ContainsGenericParameters
        {
            get
            {
                return DeclaringType.GetTypeInfo().ContainsGenericParameters;
            }
        }

        public abstract override IEnumerable<CustomAttributeData> CustomAttributes { get; }

        public abstract override Type DeclaringType { get; }

        public sealed override Type[] GetGenericArguments()
        {
            // Constructors cannot be generic. Desktop compat dictates that We throw NotSupported rather than returning a 0-length array.
            throw new NotSupportedException();
        }

        public sealed override ParameterInfo[] GetParameters()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.MethodBase_GetParameters(this);
#endif

            RuntimeParameterInfo[] runtimeParametersAndReturn = this.RuntimeParametersAndReturn;
            if (runtimeParametersAndReturn.Length == 1)
                return Array.Empty<ParameterInfo>();
            ParameterInfo[] result = new ParameterInfo[runtimeParametersAndReturn.Length - 1];
            for (int i = 0; i < result.Length; i++)
                result[i] = runtimeParametersAndReturn[i + 1];
            return result;
        }

        public abstract override Object Invoke(Object[] parameters);

        public sealed override Object Invoke(Object obj, Object[] parameters)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.MethodBase_Invoke(this, obj, parameters);
#endif

            if (parameters == null)
                parameters = Array.Empty<Object>();
            MethodInvoker methodInvoker;
            try
            {
                methodInvoker = this.MethodInvoker;
            }
            catch (Exception)
            {
                //
                // Project N compat note: On the desktop, ConstructorInfo.Invoke(Object[]) specifically forbids invoking static constructors (and
                // for us, that check is embedded inside the MethodInvoker property call.) Howver, MethodBase.Invoke(Object, Object[]) allows it. This was 
                // probably an oversight on the desktop. We choose not to support this loophole on Project N for the following reasons:
                //
                //  1. The Project N toolchain aggressively replaces static constructors with static initialization data whenever possible.
                //     So the static constructor may no longer exist. 
                //
                //  2. Invoking the static constructor through Reflection is not very useful as it invokes the static constructor whether or not
                //     it was already run. Since static constructors are specifically one-shot deals, this will almost certainly mess up the
                //     type's internal assumptions.
                //

                if (this.IsStatic)
                    throw new PlatformNotSupportedException(SR.Acc_NotClassInit);
                throw;
            }

            return methodInvoker.Invoke(obj, parameters);
        }

        public sealed override Module Module
        {
            get
            {
                return DeclaringType.GetTypeInfo().Module;
            }
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

        public abstract override MethodImplAttributes MethodImplementationFlags { get; }

        public abstract override String Name { get; }

        public abstract override bool Equals(Object obj);

        public abstract override int GetHashCode();

        public abstract override String ToString();

        protected MethodInvoker MethodInvoker
        {
            get
            {
                if (_lazyMethodInvoker == null)
                {
                    _lazyMethodInvoker = UncachedMethodInvoker;
                }
                return _lazyMethodInvoker;
            }
        }

        protected abstract RuntimeParameterInfo[] RuntimeParametersAndReturn { get; }

        protected abstract MethodInvoker UncachedMethodInvoker { get; }

        private volatile MethodInvoker _lazyMethodInvoker = null;
    }
}
