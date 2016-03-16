// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Text;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Runtime.CompilerServices;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.MethodInfos;
using global::System.Reflection.Runtime.ParameterInfos;
using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;
using global::Internal.Reflection.Extensibility;

using global::Internal.Reflection.Tracing;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.PropertyInfos
{
    //
    // The runtime's implementation of PropertyInfo's
    //
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class RuntimePropertyInfo : ExtensiblePropertyInfo, ITraceableTypeMember
    {
        //
        // propertyHandle - the "tkPropertyDef" that identifies the property.
        // definingType   - the "tkTypeDef" that defined the field (this is where you get the metadata reader that created propertyHandle.)
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
        private RuntimePropertyInfo(PropertyHandle propertyHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            _propertyHandle = propertyHandle;
            _definingTypeInfo = definingTypeInfo;
            _contextTypeInfo = contextTypeInfo;
            _reader = definingTypeInfo.Reader;
            _property = propertyHandle.GetProperty(_reader);
        }

        public sealed override PropertyAttributes Attributes
        {
            get
            {
                return _property.Flags;
            }
        }

        public sealed override bool CanRead
        {
            get
            {
                MethodHandle ignore;
                return GetAccessor(MethodSemanticsAttributes.Getter, out ignore);
            }
        }

        public sealed override bool CanWrite
        {
            get
            {
                MethodHandle ignore;
                return GetAccessor(MethodSemanticsAttributes.Setter, out ignore);
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.PropertyInfo_CustomAttributes(this);
#endif

                foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(_definingTypeInfo.ReflectionDomain, _reader, _property.CustomAttributes))
                    yield return cad;
                ExecutionDomain executionDomain = _definingTypeInfo.ReflectionDomain as ExecutionDomain;
                if (executionDomain != null)
                {
                    foreach (CustomAttributeData cad in executionDomain.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _propertyHandle, _definingTypeInfo.TypeDefinitionHandle))
                        yield return cad;
                }
            }
        }

        public sealed override Type DeclaringType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.PropertyInfo_DeclaringType(this);
#endif

                return _contextTypeInfo.AsType();
            }
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimePropertyInfo other = obj as RuntimePropertyInfo;
            if (other == null)
                return false;
            if (!(this._reader == other._reader))
                return false;
            if (!(this._propertyHandle.Equals(other._propertyHandle)))
                return false;
            if (!(this._contextTypeInfo.Equals(other._contextTypeInfo)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _propertyHandle.GetHashCode();
        }

        public sealed override Object GetConstantValue()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.PropertyInfo_GetConstantValue(this);
#endif

            if (!(_definingTypeInfo.ReflectionDomain is ExecutionDomain))
                throw new NotSupportedException(); // Cannot instantiate a boxed enum on a non-execution domain.

            Object defaultValue;
            if (!ReflectionCoreExecution.ExecutionEnvironment.GetDefaultValueIfAny(
                _reader,
                _propertyHandle,
                this.PropertyType,
                this.CustomAttributes,
                out defaultValue))
            {
                throw new InvalidOperationException();
            }
            return defaultValue;
        }

        public sealed override ParameterInfo[] GetIndexParameters()
        {
            bool useGetter = CanRead;
            RuntimeMethodInfo accessor = (useGetter ? Getter : Setter);
            RuntimeParameterInfo[] runtimeMethodParameterInfos = accessor.GetRuntimeParametersAndReturn(accessor);
            int count = runtimeMethodParameterInfos.Length - 1;  // Subtract one for the return parameter.
            if (!useGetter)
                count--;  // If we're taking the parameters off the setter, subtract one for the "value" parameter.
            if (count == 0)
                return Array.Empty<ParameterInfo>();
            ParameterInfo[] result = new ParameterInfo[count];
            for (int i = 0; i < count; i++)
                result[i] = RuntimePropertyIndexParameterInfo.GetRuntimePropertyIndexParameterInfo(this, runtimeMethodParameterInfos[i + 1]);
            return result;
        }

        public sealed override MethodInfo GetMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.PropertyInfo_GetMethod(this);
#endif

                return Getter;
            }
        }

        public sealed override Object GetValue(Object obj, Object[] index)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.PropertyInfo_GetValue(this, obj, index);
#endif

            if (_lazyGetterInvoker == null)
            {
                MethodHandle getterMethodHandle;
                if (!GetAccessor(MethodSemanticsAttributes.Getter, out getterMethodHandle))
                    throw new ArgumentException();
                MethodAttributes getterMethodAttributes = getterMethodHandle.GetMethod(_reader).Flags;
                _lazyGetterInvoker = ReflectionCoreExecution.ExecutionEnvironment.GetMethodInvoker(_reader, _contextTypeInfo.RuntimeType, getterMethodHandle, Array.Empty<RuntimeType>(), this);
            }
            if (index == null)
                index = Array.Empty<Object>();
            return _lazyGetterInvoker.Invoke(obj, index);
        }

        public sealed override Module Module
        {
            get
            {
                return _definingTypeInfo.Module;
            }
        }

        public sealed override String Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.PropertyInfo_Name(this);
#endif

                return _property.Name.GetString(_reader);
            }
        }

        public sealed override Type PropertyType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.PropertyInfo_PropertyType(this);
#endif

                TypeContext typeContext = _contextTypeInfo.TypeContext;
                Handle typeHandle = _property.Signature.GetPropertySignature(_reader).Type;
                return _contextTypeInfo.ReflectionDomain.Resolve(_reader, typeHandle, typeContext);
            }
        }

        public sealed override MethodInfo SetMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.PropertyInfo_SetMethod(this);
#endif

                return Setter;
            }
        }

        public sealed override void SetValue(Object obj, Object value, Object[] index)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.PropertyInfo_SetValue(this, obj, value, index);
#endif

            if (_lazySetterInvoker == null)
            {
                MethodHandle setterMethodHandle;
                if (!GetAccessor(MethodSemanticsAttributes.Setter, out setterMethodHandle))
                    throw new ArgumentException();
                MethodAttributes setterMethodAttributes = setterMethodHandle.GetMethod(_reader).Flags;
                _lazySetterInvoker = ReflectionCoreExecution.ExecutionEnvironment.GetMethodInvoker(_reader, _contextTypeInfo.RuntimeType, setterMethodHandle, Array.Empty<RuntimeType>(), this);
            }
            Object[] arguments;
            if (index == null)
            {
                arguments = new Object[] { value };
            }
            else
            {
                arguments = new Object[index.Length + 1];
                for (int i = 0; i < index.Length; i++)
                {
                    arguments[i] = index[i];
                }
                arguments[index.Length] = value;
            }
            _lazySetterInvoker.Invoke(obj, arguments);
        }

        public sealed override String ToString()
        {
            StringBuilder sb = new StringBuilder(30);

            ReflectionDomain reflectionDomain = _contextTypeInfo.ReflectionDomain;
            TypeContext typeContext = _contextTypeInfo.TypeContext;
            Handle typeHandle = _property.Signature.GetPropertySignature(_reader).Type;
            sb.Append(typeHandle.FormatTypeName(_reader, typeContext, reflectionDomain));
            sb.Append(' ');
            sb.Append(this.Name);
            ParameterInfo[] indexParameters = this.GetIndexParameters();
            if (indexParameters.Length != 0)
            {
                RuntimeParameterInfo[] indexRuntimeParameters = new RuntimeParameterInfo[indexParameters.Length];
                for (int i = 0; i < indexParameters.Length; i++)
                    indexRuntimeParameters[i] = (RuntimeParameterInfo)(indexParameters[i]);
                sb.Append(" [");
                sb.Append(RuntimeMethodCommon.ComputeParametersString(indexRuntimeParameters, 0));
                sb.Append(']');
            }

            return sb.ToString();
        }

        String ITraceableTypeMember.MemberName
        {
            get
            {
                return _property.Name.GetString(_reader);
            }
        }

        Type ITraceableTypeMember.ContainingType
        {
            get
            {
                return _contextTypeInfo.AsType();
            }
        }

        private RuntimeMethodInfo Getter
        {
            get
            {
                MethodHandle getterHandle;
                if (GetAccessor(MethodSemanticsAttributes.Getter, out getterHandle))
                {
                    return RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(getterHandle, _definingTypeInfo, _contextTypeInfo);
                }
                return null;
            }
        }

        private RuntimeMethodInfo Setter
        {
            get
            {
                MethodHandle setterHandle;
                if (GetAccessor(MethodSemanticsAttributes.Setter, out setterHandle))
                {
                    return RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(setterHandle, _definingTypeInfo, _contextTypeInfo);
                }
                return null;
            }
        }

        private bool GetAccessor(MethodSemanticsAttributes methodSemanticsAttribute, out MethodHandle methodHandle)
        {
            foreach (MethodSemanticsHandle methodSemanticsHandle in _property.MethodSemantics)
            {
                MethodSemantics methodSemantics = methodSemanticsHandle.GetMethodSemantics(_reader);
                if (methodSemantics.Attributes == methodSemanticsAttribute)
                {
                    methodHandle = methodSemantics.Method;
                    return true;
                }
            }
            methodHandle = default(MethodHandle);
            return false;
        }

        private RuntimePropertyInfo WithDebugName()
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

        private RuntimeNamedTypeInfo _definingTypeInfo;
        private PropertyHandle _propertyHandle;
        private RuntimeTypeInfo _contextTypeInfo;

        private MetadataReader _reader;
        private Property _property;

        private volatile MethodInvoker _lazyGetterInvoker = null;
        private volatile MethodInvoker _lazySetterInvoker = null;

        private String _debugName;
    }
}

