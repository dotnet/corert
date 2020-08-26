// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.EcmaFormat;
using System.Reflection.Runtime.FieldInfos.EcmaFormat;
using System.Reflection.Runtime.PropertyInfos.EcmaFormat;
using System.Reflection.Runtime.EventInfos.EcmaFormat;
using NameFilter = System.Reflection.Runtime.BindingFlagSupport.NameFilter;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime.TypeInfos.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeNamedTypeInfo
    {
        internal sealed override IEnumerable<ConstructorInfo> CoreGetDeclaredConstructors(NameFilter optionalNameFilter, RuntimeTypeInfo contextTypeInfo)
        {
            //
            // - It may sound odd to get a non-null name filter for a constructor search, but Type.GetMember() is an api that does this.
            //
            // - All GetConstructor() apis act as if BindingFlags.DeclaredOnly were specified. So the ReflectedType will always be the declaring type and so is not passed to this method.
            //
            MetadataReader reader = Reader;
            foreach (MethodDefinitionHandle methodHandle in DeclaredMethodAndConstructorHandles)
            {
                MethodDefinition method = reader.GetMethodDefinition(methodHandle);

                if (!EcmaMetadataHelpers.IsConstructor(ref method, reader))
                    continue;

                if (optionalNameFilter == null || optionalNameFilter.Matches(method.Name, reader))
                    yield return RuntimePlainConstructorInfo<EcmaFormatMethodCommon>.GetRuntimePlainConstructorInfo(new EcmaFormatMethodCommon(methodHandle, this, contextTypeInfo));
            }
        }

        internal sealed override IEnumerable<MethodInfo> CoreGetDeclaredMethods(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo)
        {
            MetadataReader reader = Reader;
            foreach (MethodDefinitionHandle methodHandle in DeclaredMethodAndConstructorHandles)
            {
                MethodDefinition method = reader.GetMethodDefinition(methodHandle);

                if (EcmaMetadataHelpers.IsConstructor(ref method, reader))
                    continue;

                if (optionalNameFilter == null || optionalNameFilter.Matches(method.Name, reader))
                    yield return RuntimeNamedMethodInfo<EcmaFormatMethodCommon>.GetRuntimeNamedMethodInfo(new EcmaFormatMethodCommon(methodHandle, this, contextTypeInfo), reflectedType);
            }
        }

        internal sealed override IEnumerable<EventInfo> CoreGetDeclaredEvents(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo)
        {
            MetadataReader reader = Reader;
            foreach (EventDefinitionHandle eventHandle in DeclaredEventHandles)
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(reader.GetEventDefinition(eventHandle).Name, reader))
                    yield return EcmaFormatRuntimeEventInfo.GetRuntimeEventInfo(eventHandle, this, contextTypeInfo, reflectedType);
            }
        }

        internal sealed override IEnumerable<FieldInfo> CoreGetDeclaredFields(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo)
        {
            MetadataReader reader = Reader;
            foreach (FieldDefinitionHandle fieldHandle in DeclaredFieldHandles)
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(reader.GetFieldDefinition(fieldHandle).Name, reader))
                    yield return EcmaFormatRuntimeFieldInfo.GetRuntimeFieldInfo(fieldHandle, this, contextTypeInfo, reflectedType);
            }
        }

        internal sealed override IEnumerable<PropertyInfo> CoreGetDeclaredProperties(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo)
        {
            MetadataReader reader = Reader;
            foreach (PropertyDefinitionHandle propertyHandle in DeclaredPropertyHandles)
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(reader.GetPropertyDefinition(propertyHandle).Name, reader))
                    yield return EcmaFormatRuntimePropertyInfo.GetRuntimePropertyInfo(propertyHandle, this, contextTypeInfo, reflectedType);
            }
        }

        internal sealed override IEnumerable<Type> CoreGetDeclaredNestedTypes(NameFilter optionalNameFilter)
        {
            foreach (TypeDefinitionHandle nestedTypeHandle in _typeDefinition.GetNestedTypes())
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(_reader.GetTypeDefinition(nestedTypeHandle).Name, _reader))
                    yield return nestedTypeHandle.GetNamedType(_reader);
            }
        }
    }
}
