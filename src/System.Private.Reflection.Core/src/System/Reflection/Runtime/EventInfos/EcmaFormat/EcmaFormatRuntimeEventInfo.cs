// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.EcmaFormat;
using System.Reflection.Runtime.ParameterInfos;
using System.Reflection.Runtime.CustomAttributes;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.EventInfos.EcmaFormat
{
    [DebuggerDisplay("{_debugName}")]
    internal sealed partial class EcmaFormatRuntimeEventInfo : RuntimeEventInfo
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
        private EcmaFormatRuntimeEventInfo(EventDefinitionHandle eventHandle, EcmaFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo, RuntimeTypeInfo reflectedType) :
            base(contextTypeInfo, reflectedType)
        {
            _eventHandle = eventHandle;
            _definingTypeInfo = definingTypeInfo;
            _reader = definingTypeInfo.Reader;
            _event = _reader.GetEventDefinition(eventHandle);
        }

        protected sealed override MethodInfo GetEventMethod(EventMethodSemantics whichMethod)
        {
            EventAccessors eventAccessors = _event.GetAccessors();
            MethodDefinitionHandle methodHandle;

            switch (whichMethod)
            {
                case EventMethodSemantics.Add:
                    methodHandle = eventAccessors.Adder;
                    break;

                case EventMethodSemantics.Remove:
                    methodHandle = eventAccessors.Remover;
                    break;

                case EventMethodSemantics.Fire:
                    methodHandle = eventAccessors.Raiser;
                    break;

                default:
                    return null;
            }

            /*
            This logic is part of the corresponding PropertyInfo code.. should it be here?
            bool inherited = !_reflectedType.Equals(ContextTypeInfo);
            if (inherited)
            {
                MethodAttributes flags = _reader.GetMethodDefinition(methodHandle).Attributes;
                if ((flags & MethodAttributes.MemberAccessMask) == MethodAttributes.Private)
                    return null;
            }
            */

            return RuntimeNamedMethodInfo<EcmaFormatMethodCommon>.GetRuntimeNamedMethodInfo(new EcmaFormatMethodCommon(methodHandle, _definingTypeInfo, ContextTypeInfo), ReflectedTypeInfo);
        }

        public sealed override EventAttributes Attributes
        {
            get
            {
                return _event.Attributes;
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

                return RuntimeCustomAttributeData.GetCustomAttributes(_reader, _event.GetCustomAttributes());
            }
        }

        public sealed override bool HasSameMetadataDefinitionAs(MemberInfo other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (!(other is EcmaFormatRuntimeEventInfo otherEvent))
                return false;
            if (!(_reader == otherEvent._reader))
                return false;
            if (!(_eventHandle.Equals(otherEvent._eventHandle)))
                return false;
            return true;
        }

        public sealed override bool Equals(Object obj)
        {
            if (!(obj is EcmaFormatRuntimeEventInfo other))
                return false;
            if (!(_reader == other._reader))
                return false;
            if (!(_eventHandle.Equals(other._eventHandle)))
                return false;
            if (!(ContextTypeInfo.Equals(other.ContextTypeInfo)))
                return false;
            if (!(ReflectedType.Equals(other.ReflectedType)))
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
                return _event.Type.Resolve(_reader, ContextTypeInfo.TypeContext);
            }
        }

        public sealed override int MetadataToken
        {
            get
            {
                return MetadataTokens.GetToken(_eventHandle);
            }
        }

        protected sealed override string MetadataName
        {
            get
            {
                return _event.Name.GetString(_reader);
            }
        }

        protected sealed override RuntimeTypeInfo DefiningTypeInfo
        {
            get
            {
                return _definingTypeInfo;
            }
        }

        private readonly EcmaFormatRuntimeNamedTypeInfo _definingTypeInfo;
        private readonly EventDefinitionHandle _eventHandle;

        private readonly MetadataReader _reader;
        private readonly EventDefinition _event;
    }
}
