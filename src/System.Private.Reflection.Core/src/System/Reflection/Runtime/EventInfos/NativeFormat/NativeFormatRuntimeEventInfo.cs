// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.NativeFormat;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.NativeFormat;
using System.Reflection.Runtime.ParameterInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Metadata.NativeFormat;

using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.EventInfos.NativeFormat
{
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class NativeFormatRuntimeEventInfo : RuntimeEventInfo
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
        private NativeFormatRuntimeEventInfo(EventHandle eventHandle, NativeFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType) :
            base(contextTypeInfo, reflectedType)
        {
            _eventHandle = eventHandle;
            _definingTypeInfo = definingTypeInfo;
            _reader = definingTypeInfo.Reader;
            _event = eventHandle.GetEvent(_reader);
        }

        protected override MethodInfo GetEventMethod(EventMethodSemantics whichMethod)
        {
            MethodSemanticsAttributes localMethodSemantics;
            switch (whichMethod)
            {
                case EventMethodSemantics.Add:
                    localMethodSemantics = MethodSemanticsAttributes.AddOn;
                    break;

                case EventMethodSemantics.Fire:
                    localMethodSemantics = MethodSemanticsAttributes.Fire;
                    break;

                case EventMethodSemantics.Remove:
                    localMethodSemantics = MethodSemanticsAttributes.RemoveOn;
                    break;

                default:
                    return null;
            }

            foreach (MethodSemanticsHandle methodSemanticsHandle in _event.MethodSemantics)
            {
                MethodSemantics methodSemantics = methodSemanticsHandle.GetMethodSemantics(_reader);
                if (methodSemantics.Attributes == localMethodSemantics)
                {
                    return RuntimeNamedMethodInfoWithMetadata<NativeFormatMethodCommon>.GetRuntimeNamedMethodInfo(new NativeFormatMethodCommon(methodSemantics.Method, _definingTypeInfo, _contextTypeInfo), _reflectedType);
                }
            }

            return null;
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

                foreach (CustomAttributeData cad in RuntimeCustomAttributeData.GetCustomAttributes(_reader, _event.CustomAttributes))
                    yield return cad;
                foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _eventHandle, _definingTypeInfo.TypeDefinitionHandle))
                    yield return cad;
            }
        }

        public sealed override bool Equals(Object obj)
        {
            NativeFormatRuntimeEventInfo other = obj as NativeFormatRuntimeEventInfo;
            if (other == null)
                return false;
            if (!(_reader == other._reader))
                return false;
            if (!(_eventHandle.Equals(other._eventHandle)))
                return false;
            if (!(_contextTypeInfo.Equals(other._contextTypeInfo)))
                return false;
            if (!(_reflectedType.Equals(other._reflectedType)))
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
                return _event.Type.Resolve(_reader, _contextTypeInfo.TypeContext);
            }
        }

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
            }
        }

        internal protected sealed override string MetadataName
        {
            get
            {
                return _event.Name.GetString(_reader);
            }
        }

        internal sealed protected override RuntimeTypeInfo DefiningTypeInfo
        {
            get
            {
                return _definingTypeInfo;
            }
        }

        private readonly NativeFormatRuntimeNamedTypeInfo _definingTypeInfo;
        private readonly EventHandle _eventHandle;

        private readonly MetadataReader _reader;
        private readonly Event _event;
    }
}
