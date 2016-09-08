// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
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
    // The runtime's implementation of ConstructorInfo's represented in the metadata (this is the 99% case.)
    //
    internal sealed partial class RuntimePlainConstructorInfo : RuntimeConstructorInfo
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
        private RuntimePlainConstructorInfo(MethodHandle methodHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
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

        public sealed override Type DeclaringType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.MethodBase_DeclaringType(this);
#endif

                return _common.DeclaringType;
            }
        }

        public sealed override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.ConstructorInfo_Invoke(this, parameters);
#endif
            if (invokeAttr != BindingFlags.Default || binder != null || culture != null)
                throw new NotImplementedException();

            if (parameters == null)
                parameters = Array.Empty<Object>();

            // Most objects are allocated by NewObject and their constructors return "void". But in many frameworks, 
            // there are "weird" cases (e.g. String) where the constructor must do both the allocation and initialization. 
            // Reflection.Core does not hardcode these special cases. It's up to the ExecutionEnvironment to steer 
            // us the right way by coordinating the implementation of NewObject and MethodInvoker.
            Object newObject = ReflectionCoreExecution.ExecutionEnvironment.NewObject(this.DeclaringType.TypeHandle);
            Object ctorAllocatedObject = this.MethodInvoker.Invoke(newObject, parameters);
            return newObject != null ? newObject : ctorAllocatedObject;
        }

        public sealed override MethodImplAttributes MethodImplementationFlags
        {
            get
            {
                return _common.MethodImplementationFlags;
            }
        }

        public sealed override String Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.MethodBase_Name(this);
#endif

                return _common.Name;
            }
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimePlainConstructorInfo other = obj as RuntimePlainConstructorInfo;
            if (other == null)
                return false;
            return _common.Equals(other._common);
        }

        public sealed override int GetHashCode()
        {
            return _common.GetHashCode();
        }

        public sealed override String ToString()
        {
            return _common.ComputeToString(this, Array.Empty<RuntimeTypeInfo>());
        }

        protected sealed override RuntimeParameterInfo[] RuntimeParameters
        {
            get
            {
                RuntimeParameterInfo ignore;
                return _lazyParameters ?? (_lazyParameters = _common.GetRuntimeParameters(this, Array.Empty<RuntimeTypeInfo>(), out ignore));
            }
        }

        protected sealed override MethodInvoker UncachedMethodInvoker
        {
            get
            {
                if (_common.DefiningTypeInfo.IsAbstract)
                    throw new MemberAccessException(SR.Format(SR.Acc_CreateAbstEx, _common.DefiningTypeInfo.FullName));

                if (this.IsStatic)
                    throw new MemberAccessException(SR.Acc_NotClassInit);

                return ReflectionCoreExecution.ExecutionEnvironment.GetMethodInvoker(_common.Reader, _common.DeclaringType, _common.MethodHandle, Array.Empty<RuntimeTypeInfo>(), this);
            }
        }

        private volatile RuntimeParameterInfo[] _lazyParameters;
        private readonly RuntimeMethodCommon _common;
    }
}

