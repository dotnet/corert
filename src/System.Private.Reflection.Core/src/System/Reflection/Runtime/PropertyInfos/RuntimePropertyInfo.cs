// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.ParameterInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;


namespace System.Reflection.Runtime.PropertyInfos
{
    //
    // The runtime's implementation of PropertyInfo's
    //
    [DebuggerDisplay("{_debugName}")]
    internal abstract partial class RuntimePropertyInfo : PropertyInfo, ITraceableTypeMember
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
        protected RuntimePropertyInfo(RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType)
        {
            ContextTypeInfo = contextTypeInfo;
            _reflectedType = reflectedType;
        }

        public sealed override bool CanRead
        {
            get
            {
                return Getter != null;
            }
        }

        public sealed override bool CanWrite
        {
            get
            {
                return Setter != null;
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

                return ContextTypeInfo;
            }
        }

        public sealed override ParameterInfo[] GetIndexParameters()
        {
            ParameterInfo[] indexParameters = _lazyIndexParameters;
            if (indexParameters == null)
            {
                bool useGetter = CanRead;
                RuntimeMethodInfo accessor = (useGetter ? Getter : Setter);
                RuntimeParameterInfo[] runtimeMethodParameterInfos = accessor.RuntimeParameters;
                int count = runtimeMethodParameterInfos.Length;
                if (!useGetter)
                    count--;  // If we're taking the parameters off the setter, subtract one for the "value" parameter.
                if (count == 0)
                {
                    _lazyIndexParameters = indexParameters = Array.Empty<ParameterInfo>();
                }
                else
                {
                    indexParameters = new ParameterInfo[count];
                    for (int i = 0; i < count; i++)
                    {
                        indexParameters[i] = RuntimePropertyIndexParameterInfo.GetRuntimePropertyIndexParameterInfo(this, runtimeMethodParameterInfos[i]);
                    }
                    _lazyIndexParameters = indexParameters;
                }
            }

            int numParameters = indexParameters.Length;
            if (numParameters == 0)
                return indexParameters;
            ParameterInfo[] result = new ParameterInfo[numParameters];
            for (int i = 0; i < numParameters; i++)
            {
                result[i] = indexParameters[i];
            }
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

        public sealed override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.PropertyInfo_GetValue(this, obj, index);
#endif
            binder.EnsureNotCustomBinder();

            if (_lazyGetterInvoker == null)
            {
                if (!CanRead)
                    throw new ArgumentException();

                _lazyGetterInvoker = Getter.GetUncachedMethodInvoker(Array.Empty<RuntimeTypeInfo>(), this);
            }
            if (index == null)
                index = Array.Empty<Object>();
            return _lazyGetterInvoker.Invoke(obj, index);
        }

        public sealed override Module Module
        {
            get
            {
                return DefiningTypeInfo.Module;
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

                return MetadataName;
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

                TypeContext typeContext = ContextTypeInfo.TypeContext;
                return PropertyTypeHandle.Resolve(typeContext);
            }
        }

        public sealed override Type ReflectedType
        {
            get
            {
                return _reflectedType;
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

        public sealed override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.PropertyInfo_SetValue(this, obj, value, index);
#endif
            binder.EnsureNotCustomBinder();

            if (_lazySetterInvoker == null)
            {
                if (!CanWrite)
                    throw new ArgumentException();

                _lazySetterInvoker = Setter.GetUncachedMethodInvoker(Array.Empty<RuntimeTypeInfo>(), this);
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

            TypeContext typeContext = ContextTypeInfo.TypeContext;
            sb.Append(PropertyTypeHandle.FormatTypeName(typeContext));
            sb.Append(' ');
            sb.Append(this.Name);
            ParameterInfo[] indexParameters = this.GetIndexParameters();
            if (indexParameters.Length != 0)
            {
                RuntimeParameterInfo[] indexRuntimeParameters = new RuntimeParameterInfo[indexParameters.Length];
                for (int i = 0; i < indexParameters.Length; i++)
                    indexRuntimeParameters[i] = (RuntimeParameterInfo)(indexParameters[i]);
                sb.Append(" [");
                sb.Append(RuntimeMethodHelpers.ComputeParametersString(indexRuntimeParameters));
                sb.Append(']');
            }

            return sb.ToString();
        }

        String ITraceableTypeMember.MemberName
        {
            get
            {
                return MetadataName;
            }
        }

        Type ITraceableTypeMember.ContainingType
        {
            get
            {
                return ContextTypeInfo;
            }
        }

        private RuntimeNamedMethodInfo Getter
        {
            get
            {
                RuntimeNamedMethodInfo getter = _lazyGetter;
                if (getter == null)
                {
                    getter = GetPropertyMethod(PropertyMethodSemantics.Getter);

                    if (getter == null)
                        getter = RuntimeDummyMethodInfo.Instance;

                    _lazyGetter = getter;
                }

                return object.ReferenceEquals(getter, RuntimeDummyMethodInfo.Instance) ? null : getter;
            }
        }

        private RuntimeNamedMethodInfo Setter
        {
            get
            {
                RuntimeNamedMethodInfo setter = _lazySetter;
                if (setter == null)
                {
                    setter = GetPropertyMethod(PropertyMethodSemantics.Setter);

                    if (setter == null)
                        setter = RuntimeDummyMethodInfo.Instance;

                    _lazySetter = setter;
                }

                return object.ReferenceEquals(setter, RuntimeDummyMethodInfo.Instance) ? null : setter;
            }
        }

        protected RuntimePropertyInfo WithDebugName()
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
                _debugName = MetadataName;
            }
            return this;
        }

        // Types that derive from RuntimePropertyInfo must implement the following public surface area members
        public abstract override PropertyAttributes Attributes { get; }
        public abstract override IEnumerable<CustomAttributeData> CustomAttributes { get; }
        public abstract override bool Equals(Object obj);
        public abstract override int GetHashCode();
        public abstract override Object GetConstantValue();
        public abstract override int MetadataToken { get; }

        /// <summary>
        /// Return a qualified handle that can be used to get the type of the property.
        /// </summary>
        protected abstract QTypeDefRefOrSpec PropertyTypeHandle { get; }

        protected enum PropertyMethodSemantics
        {
            Getter,
            Setter,
        }

        /// <summary>
        /// Override to return the Method that corresponds to the specified semantic.
        /// Return null if a method of the appropriate semantic does not exist
        /// </summary>
        protected abstract RuntimeNamedMethodInfo GetPropertyMethod(PropertyMethodSemantics whichMethod);

        /// <summary>
        /// Override to provide the metadata based name of a property. (Different from the Name
        /// property in that it does not go into the reflection trace logic.)
        /// </summary>
        protected abstract string MetadataName { get; }

        /// <summary>
        /// Return the DefiningTypeInfo as a RuntimeTypeInfo (instead of as a format specific type info)
        /// </summary>
        protected abstract RuntimeTypeInfo DefiningTypeInfo { get; }

        protected readonly RuntimeTypeInfo ContextTypeInfo;
        protected readonly RuntimeTypeInfo _reflectedType;

        private volatile MethodInvoker _lazyGetterInvoker = null;
        private volatile MethodInvoker _lazySetterInvoker = null;

        private volatile RuntimeNamedMethodInfo _lazyGetter;
        private volatile RuntimeNamedMethodInfo _lazySetter;

        private volatile ParameterInfo[] _lazyIndexParameters;

        private String _debugName;
    }
}

