// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Runtime.CompilerServices;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.MethodInfos;
using global::System.Reflection.Runtime.ParameterInfos;
using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Extensibility;
using global::Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.EventInfos
{
    //
    // The runtime's implementation of EventInfo's
    //
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class RuntimeEventInfo : ExtensibleEventInfo, ITraceableTypeMember
    {
        //
        // eventHandle    - the "tkEventDef" that identifies the event.
        // definingType   - the "tkTypeDef" that defined the field (this is where you get the metadata reader that created eventHandle.)
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
        private RuntimeEventInfo(EventHandle eventHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            _eventHandle = eventHandle;
            _definingTypeInfo = definingTypeInfo;
            _contextTypeInfo = contextTypeInfo;
            _reader = definingTypeInfo.Reader;
            _event = eventHandle.GetEvent(_reader);
        }

        public sealed override void AddEventHandler(Object target, Delegate handler)
        {
            MethodInfo addMethod = this.AddMethod;
            if (!addMethod.IsPublic)
                throw new InvalidOperationException(SR.InvalidOperation_NoPublicAddMethod);
            addMethod.Invoke(target, new Object[] { handler });
        }

        public sealed override MethodInfo AddMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.EventInfo_AddMethod(this);
#endif

                foreach (MethodSemanticsHandle methodSemanticsHandle in _event.MethodSemantics)
                {
                    MethodSemantics methodSemantics = methodSemanticsHandle.GetMethodSemantics(_reader);
                    if (methodSemantics.Attributes == MethodSemanticsAttributes.AddOn)
                    {
                        return RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(methodSemantics.Method, _definingTypeInfo, _contextTypeInfo);
                    }
                }
                throw new BadImageFormatException(); // Added is a required method.
            }
        }

        public sealed override EventAttributes Attributes
        {
            get
            {
                return _event.Flags;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.EventInfo_CustomAttributes(this);
#endif

                foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(_definingTypeInfo.ReflectionDomain, _reader, _event.CustomAttributes))
                    yield return cad;
                ExecutionDomain executionDomain = _definingTypeInfo.ReflectionDomain as ExecutionDomain;
                if (executionDomain != null)
                {
                    foreach (CustomAttributeData cad in executionDomain.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _eventHandle, _definingTypeInfo.TypeDefinitionHandle))
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
                    ReflectionTrace.EventInfo_DeclaringType(this);
#endif

                return _contextTypeInfo.AsType();
            }
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeEventInfo other = obj as RuntimeEventInfo;
            if (other == null)
                return false;
            if (!(this._reader == other._reader))
                return false;
            if (!(this._eventHandle.Equals(other._eventHandle)))
                return false;
            if (!(this._contextTypeInfo.Equals(other._contextTypeInfo)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _eventHandle.GetHashCode();
        }

        public sealed override Type EventHandlerType
        {
            get
            {
                return _definingTypeInfo.ReflectionDomain.Resolve(_reader, _event.Type, _contextTypeInfo.TypeContext);
            }
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
                    ReflectionTrace.EventInfo_Name(this);
#endif

                return _event.Name.GetString(_reader);
            }
        }

        public sealed override MethodInfo RaiseMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.EventInfo_RaiseMethod(this);
#endif

                foreach (MethodSemanticsHandle methodSemanticsHandle in _event.MethodSemantics)
                {
                    MethodSemantics methodSemantics = methodSemanticsHandle.GetMethodSemantics(_reader);
                    if (methodSemantics.Attributes == MethodSemanticsAttributes.Fire)
                    {
                        return RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(methodSemantics.Method, _definingTypeInfo, _contextTypeInfo);
                    }
                }
                return null;
            }
        }

        public sealed override void RemoveEventHandler(Object target, Delegate handler)
        {
            MethodInfo removeMethod = this.RemoveMethod;
            if (!removeMethod.IsPublic)
                throw new InvalidOperationException(SR.InvalidOperation_NoPublicRemoveMethod);
            removeMethod.Invoke(target, new Object[] { handler });
        }

        public sealed override MethodInfo RemoveMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.EventInfo_RemoveMethod(this);
#endif

                foreach (MethodSemanticsHandle methodSemanticsHandle in _event.MethodSemantics)
                {
                    MethodSemantics methodSemantics = methodSemanticsHandle.GetMethodSemantics(_reader);
                    if (methodSemantics.Attributes == MethodSemanticsAttributes.RemoveOn)
                    {
                        return RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(methodSemantics.Method, _definingTypeInfo, _contextTypeInfo);
                    }
                }
                throw new BadImageFormatException(); // Removed is a required method.
            }
        }

        public sealed override String ToString()
        {
            MethodInfo addMethod = this.AddMethod;
            ParameterInfo[] parameters = addMethod.GetParameters();
            if (parameters.Length == 0)
                throw new InvalidOperationException(); // Legacy: Why is a ToString() intentionally throwing an exception?
            RuntimeParameterInfo runtimeParameterInfo = (RuntimeParameterInfo)(parameters[0]);
            return runtimeParameterInfo.ParameterTypeString + " " + this.Name;
        }

        String ITraceableTypeMember.MemberName
        {
            get
            {
                return _event.Name.GetString(_reader);
            }
        }

        Type ITraceableTypeMember.ContainingType
        {
            get
            {
                return _contextTypeInfo.AsType();
            }
        }

        private RuntimeEventInfo WithDebugName()
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
        private EventHandle _eventHandle;
        private RuntimeTypeInfo _contextTypeInfo;

        private MetadataReader _reader;
        private Event _event;

        private String _debugName;
    }
}
