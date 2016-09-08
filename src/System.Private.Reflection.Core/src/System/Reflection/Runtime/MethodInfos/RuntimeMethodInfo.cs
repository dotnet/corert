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
using System.Reflection.Runtime.ParameterInfos;
using System.Reflection.Runtime.BindingFlagSupport;

using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.MethodInfos
{
    //
    // Abstract base class for RuntimeNamedMethodInfo, RuntimeConstructedGenericMethodInfo.
    //
    [DebuggerDisplay("{_debugName}")]
    internal abstract partial class RuntimeMethodInfo : MethodInfo, ITraceableTypeMember
    {
        protected RuntimeMethodInfo()
        {
        }

        public abstract override MethodAttributes Attributes
        {
            get;
        }

        public abstract override CallingConventions CallingConvention
        {
            get;
        }

        public sealed override bool ContainsGenericParameters
        {
            get
            {
                if (DeclaringType.GetTypeInfo().ContainsGenericParameters)
                    return true;

                if (!IsGenericMethod)
                    return false;

                Type[] pis = GetGenericArguments();
                for (int i = 0; i < pis.Length; i++)
                {
                    if (pis[i].GetTypeInfo().ContainsGenericParameters)
                        return true;
                }

                return false;
            }
        }

        public sealed override Delegate CreateDelegate(Type delegateType)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.MethodInfo_CreateDelegate(this, delegateType);
#endif

            // Legacy: The only difference between calling CreateDelegate(type) and CreateDelegate(type, null) is that the former
            // disallows closed instance delegates for V1.1 backward compatibility.
            return CreateDelegate(delegateType, null, allowClosedInstanceDelegates: false);
        }

        public sealed override Delegate CreateDelegate(Type delegateType, Object target)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.MethodInfo_CreateDelegate(this, delegateType, target);
#endif

            return CreateDelegate(delegateType, target, allowClosedInstanceDelegates: true);
        }

        public abstract override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get;
        }

        public sealed override Type DeclaringType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.MethodBase_DeclaringType(this);
#endif

                return this.RuntimeDeclaringType;
            }
        }

        public abstract override bool Equals(object obj);

        public abstract override int GetHashCode();

        public sealed override MethodInfo GetBaseDefinition()
        {
            MethodInfo method = this;
            while (true)
            {
                MethodInfo next = method.GetImplicitlyOverriddenBaseClassMember();
                if (next == null)
                    return method;

                method = next;
            }
        }

        public sealed override Type[] GetGenericArguments()
        {
            return RuntimeGenericArgumentsOrParameters.CloneTypeArray();
        }

        public abstract override MethodInfo GetGenericMethodDefinition();

        public sealed override ParameterInfo[] GetParameters()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.MethodBase_GetParameters(this);
#endif

            RuntimeParameterInfo[] runtimeParameterInfos = RuntimeParameters;
            if (runtimeParameterInfos.Length == 0)
                return Array.Empty<ParameterInfo>();
            ParameterInfo[] result = new ParameterInfo[runtimeParameterInfos.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = runtimeParameterInfos[i];
            return result;
        }

        public sealed override ParameterInfo[] GetParametersNoCopy()
        {
            return RuntimeParameters;
        }

        [DebuggerGuidedStepThroughAttribute]
        public sealed override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.MethodBase_Invoke(this, obj, parameters);
#endif
            if (invokeAttr != BindingFlags.Default || binder != null || culture != null)
                throw new NotImplementedException();

            if (parameters == null)
                parameters = Array.Empty<Object>();
            MethodInvoker methodInvoker = this.MethodInvoker;
            object result = methodInvoker.Invoke(obj, parameters);
            System.Diagnostics.DebugAnnotations.PreviousCallContainsDebuggerStepInCode();
            return result;
        }

        public abstract override bool IsGenericMethod
        {
            get;
        }

        public abstract override bool IsGenericMethodDefinition
        {
            get;
        }

        public abstract override MethodInfo MakeGenericMethod(params Type[] typeArguments);

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
            }
        }

        public abstract override MethodImplAttributes MethodImplementationFlags
        {
            get;
        }

        public abstract override Module Module
        {
            get;
        }

        public sealed override String Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.MethodBase_Name(this);
#endif
                return this.RuntimeName;
            }
        }

        public sealed override ParameterInfo ReturnParameter
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.MethodInfo_ReturnParameter(this);
#endif

                return this.RuntimeReturnParameter;
            }
        }

        public sealed override Type ReturnType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.MethodInfo_ReturnType(this);
#endif

                return ReturnParameter.ParameterType;
            }
        }

        public abstract override String ToString();

        Type ITraceableTypeMember.ContainingType
        {
            get
            {
                return this.RuntimeDeclaringType;
            }
        }

        String ITraceableTypeMember.MemberName
        {
            get
            {
                return this.RuntimeName;
            }
        }

        internal abstract RuntimeTypeInfo RuntimeDeclaringType
        {
            get;
        }

        internal abstract String RuntimeName
        {
            get;
        }

        protected abstract MethodInvoker UncachedMethodInvoker { get; }

        //
        // The non-public version of MethodInfo.GetGenericArguments() (does not array-copy and has a more truthful name.)
        //
        internal abstract RuntimeTypeInfo[] RuntimeGenericArgumentsOrParameters { get; }

        internal abstract RuntimeParameterInfo[] GetRuntimeParameters(RuntimeMethodInfo contextMethod, out RuntimeParameterInfo returnParameter);

        //
        // The non-public version of MethodInfo.GetParameters() (does not array-copy.) 
        //
        internal RuntimeParameterInfo[] RuntimeParameters
        {
            get
            {
                RuntimeParameterInfo[] parameters = _lazyParameters;
                if (parameters == null)
                {
                    RuntimeParameterInfo returnParameter;
                    parameters = _lazyParameters = GetRuntimeParameters(this, out returnParameter);
                    _lazyReturnParameter = returnParameter;  // Opportunistically initialize the _lazyReturnParameter latch as well.
                }
                return parameters;
            }
        }

        internal RuntimeParameterInfo RuntimeReturnParameter
        {
            get
            {
                RuntimeParameterInfo returnParameter = _lazyReturnParameter;
                if (returnParameter == null)
                {
                    // Though the returnParameter is our primary objective, we can opportunistically initialize the _lazyParameters latch too.
                    _lazyParameters = GetRuntimeParameters(this, out returnParameter);
                    _lazyReturnParameter = returnParameter;
                }
                return returnParameter;
            }
        }

        private volatile RuntimeParameterInfo[] _lazyParameters;
        private volatile RuntimeParameterInfo _lazyReturnParameter;

        internal MethodInvoker MethodInvoker
        {
            get
            {
                MethodInvoker methodInvoker = _lazyMethodInvoker;
                if (methodInvoker == null)
                {
                    if (ReturnType.IsByRef)
                        throw new NotSupportedException(SR.NotSupported_ByRefReturn);
                    methodInvoker = _lazyMethodInvoker = this.UncachedMethodInvoker;
                }
                return methodInvoker;
            }
        }

        private volatile MethodInvoker _lazyMethodInvoker = null;


        //
        // Common CreateDelegate worker.
        //
        private Delegate CreateDelegate(Type delegateType, Object target, bool allowClosedInstanceDelegates)
        {
            if (delegateType == null)
                throw new ArgumentNullException(nameof(delegateType));

            ExecutionEnvironment executionEnvironment = ReflectionCoreExecution.ExecutionEnvironment;
            RuntimeTypeHandle delegateTypeHandle = delegateType.TypeHandle;
            if (!executionEnvironment.IsAssignableFrom(typeof(Delegate).TypeHandle, delegateTypeHandle))
                throw new ArgumentException(SR.Arg_MustBeDelegate);
            IEnumerator<MethodInfo> invokeMethodEnumerator = delegateType.GetTypeInfo().GetDeclaredMethods("Invoke").GetEnumerator();
            if (!invokeMethodEnumerator.MoveNext())
            {
                // No Invoke method found. Since delegate types are compiler constructed, the most likely cause is missing metadata rather than
                // a missing Invoke method. 

                // We're deliberating calling FullName rather than ToString() because if it's the type that's missing metadata, 
                // the FullName property constructs a more informative MissingMetadataException than we can. 
                String fullName = delegateType.FullName;
                throw new MissingMetadataException(SR.Format(SR.Arg_InvokeMethodMissingMetadata, fullName)); // No invoke method found.
            }
            MethodInfo invokeMethod = invokeMethodEnumerator.Current;
            if (invokeMethodEnumerator.MoveNext())
                throw new ArgumentException(SR.Arg_MustBeDelegate); // Multiple invoke methods found.

            // Make sure the return type is assignment-compatible.
            CheckIsAssignableFrom(executionEnvironment, invokeMethod.ReturnParameter.ParameterType, this.ReturnParameter.ParameterType);

            IList<ParameterInfo> delegateParameters = invokeMethod.GetParametersNoCopy();
            IList<ParameterInfo> targetParameters = this.GetParametersNoCopy();
            IEnumerator<ParameterInfo> delegateParameterEnumerator = delegateParameters.GetEnumerator();
            IEnumerator<ParameterInfo> targetParameterEnumerator = targetParameters.GetEnumerator();

            bool isStatic = this.IsStatic;
            bool isOpen;
            if (isStatic)
            {
                if (delegateParameters.Count == targetParameters.Count)
                {
                    // Open static: This is the "typical" case of calling a static method.
                    isOpen = true;
                    if (target != null)
                        throw new ArgumentException(SR.Arg_DlgtTargMeth);
                }
                else
                {
                    // Closed static: This is the "weird" v2.0 case where the delegate is closed over the target method's first parameter.
                    //   (it make some kinda sense if you think of extension methods.)
                    isOpen = false;
                    if (!targetParameterEnumerator.MoveNext())
                        throw new ArgumentException(SR.Arg_DlgtTargMeth);
                    if (target != null)
                        CheckIsAssignableFrom(executionEnvironment, targetParameterEnumerator.Current.ParameterType, target.GetType());
                }
            }
            else
            {
                if (delegateParameters.Count == targetParameters.Count)
                {
                    // Closed instance: This is the "typical" case of invoking an instance method.
                    isOpen = false;
                    if (!allowClosedInstanceDelegates)
                        throw new ArgumentException(SR.Arg_DlgtTargMeth);
                    if (target != null)
                        CheckIsAssignableFrom(executionEnvironment, this.DeclaringType, target.GetType());
                }
                else
                {
                    // Open instance: This is the "weird" v2.0 case where the delegate has a leading extra parameter that's assignable to the target method's
                    // declaring type.
                    if (!delegateParameterEnumerator.MoveNext())
                        throw new ArgumentException(SR.Arg_DlgtTargMeth);
                    isOpen = true;
                    CheckIsAssignableFrom(executionEnvironment, this.DeclaringType, delegateParameterEnumerator.Current.ParameterType);
                    if (target != null)
                        throw new ArgumentException(SR.Arg_DlgtTargMeth);
                }
            }

            // Verify that the parameters that the delegate and method have in common are assignment-compatible.
            while (delegateParameterEnumerator.MoveNext())
            {
                if (!targetParameterEnumerator.MoveNext())
                    throw new ArgumentException(SR.Arg_DlgtTargMeth);
                CheckIsAssignableFrom(executionEnvironment, targetParameterEnumerator.Current.ParameterType, delegateParameterEnumerator.Current.ParameterType);
            }
            if (targetParameterEnumerator.MoveNext())
                throw new ArgumentException(SR.Arg_DlgtTargMeth);

            return this.MethodInvoker.CreateDelegate(delegateType.TypeHandle, target, isStatic: isStatic, isVirtual: false, isOpen: isOpen);
        }


        private static void CheckIsAssignableFrom(ExecutionEnvironment executionEnvironment, Type dstType, Type srcType)
        {
            // byref types do not have a TypeHandle so we must treat these separately.
            if (dstType.IsByRef && srcType.IsByRef)
            {
                if (!dstType.Equals(srcType))
                    throw new ArgumentException(SR.Arg_DlgtTargMeth);
            }

            // Enable pointers (which don't necessarily have typehandles). todo:be able to handle intptr <-> pointer, check if we need to handle 
            // casts via pointer where the pointer types aren't identical
            if (dstType.Equals(srcType))
            {
                return;
            }

            // If assignment compatible in the normal way, allow
            if (executionEnvironment.IsAssignableFrom(dstType.TypeHandle, srcType.TypeHandle))
            {
                return;
            }

            // they are not compatible yet enums can go into each other if their underlying element type is the same
            // or into their equivalent integral type
            Type dstTypeUnderlying = dstType;
            if (dstType.GetTypeInfo().IsEnum)
            {
                dstTypeUnderlying = Enum.GetUnderlyingType(dstType);
            }
            Type srcTypeUnderlying = srcType;
            if (srcType.GetTypeInfo().IsEnum)
            {
                srcTypeUnderlying = Enum.GetUnderlyingType(srcType);
            }
            if (dstTypeUnderlying.Equals(srcTypeUnderlying))
            {
                return;
            }

            throw new ArgumentException(SR.Arg_DlgtTargMeth);
        }

        protected RuntimeMethodInfo WithDebugName()
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

        private String _debugName;
    }
}

