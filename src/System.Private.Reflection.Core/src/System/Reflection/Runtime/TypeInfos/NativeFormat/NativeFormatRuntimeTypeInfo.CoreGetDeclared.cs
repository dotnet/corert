using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Reflection.Runtime.TypeInfos.NativeFormat
{
    internal sealed partial class NativeFormatRuntimeNamedTypeInfo
    {
        internal sealed override IEnumerable<ConstructorInfo> CoreGetDeclaredConstructors(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            //
            // - It may sound odd to get a non-null name filter for a constructor search, but Type.GetMember() is an api that does this.
            //
            // - All GetConstructor() apis act as if BindingFlags.DeclaredOnly were specified. So the ReflectedType will always be the declaring type and so is not passed to this method.
            //
            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                if (reflectedType == null)
                    reflectedType = this;

                MetadataReader reader = definingType.Reader;
                foreach (MethodHandle methodHandle in definingType.DeclaredMethodAndConstructorHandles)
                {
                    Method method = methodHandle.GetMethod(reader);

                    if (!MetadataReaderExtensions.IsConstructor(ref method, reader))
                        continue;

                    if (optionalNameFilter == null || optionalNameFilter.Matches(method.Name, reader))
                        yield return RuntimePlainConstructorInfo.GetRuntimePlainConstructorInfo(methodHandle, definingType, reflectedType);
                }
            }
        }

        internal sealed override IEnumerable<MethodInfo> CoreGetDeclaredMethods(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                MetadataReader reader = definingType.Reader;
                foreach (MethodHandle methodHandle in definingType.DeclaredMethodAndConstructorHandles)
                {
                    Method method = methodHandle.GetMethod(reader);

                    if (MetadataReaderExtensions.IsConstructor(ref method, reader))
                        continue;

                    if (optionalNameFilter == null || optionalNameFilter.Matches(method.Name, reader))
                        yield return RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(methodHandle, definingType, this, reflectedType);
                }
            }

            foreach (RuntimeMethodInfo syntheticMethod in SyntheticMethods)
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(syntheticMethod.Name))
                    yield return syntheticMethod;
            }
        }

        internal sealed override IEnumerable<EventInfo> CoreGetDeclaredEvents(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                MetadataReader reader = definingType.Reader;
                foreach (EventHandle eventHandle in definingType.DeclaredEventHandles)
                {
                    if (optionalNameFilter == null || optionalNameFilter.Matches(eventHandle.GetEvent(reader).Name, reader))
                        yield return RuntimeEventInfo.GetRuntimeEventInfo(eventHandle, definingType, this, reflectedType);
                }
            }
        }

        internal sealed override IEnumerable<FieldInfo> CoreGetDeclaredFields(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                MetadataReader reader = definingType.Reader;
                foreach (FieldHandle fieldHandle in definingType.DeclaredFieldHandles)
                {
                    if (optionalNameFilter == null || optionalNameFilter.Matches(fieldHandle.GetField(reader).Name, reader))
                        yield return RuntimeFieldInfo.GetRuntimeFieldInfo(fieldHandle, definingType, this, reflectedType);
                }
            }
        }

        internal sealed override IEnumerable<PropertyInfo> CoreGetDeclaredProperties(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType)
        {
            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                MetadataReader reader = definingType.Reader;
                foreach (PropertyHandle propertyHandle in definingType.DeclaredPropertyHandles)
                {
                    if (optionalNameFilter == null || optionalNameFilter.Matches(propertyHandle.GetProperty(reader).Name, reader))
                        yield return RuntimePropertyInfo.GetRuntimePropertyInfo(propertyHandle, definingType, this, reflectedType);
                }
            }
        }

        internal sealed override IEnumerable<Type> CoreGetDeclaredNestedTypes(NameFilter optionalNameFilter)
        {
            foreach (TypeDefinitionHandle nestedTypeHandle in _typeDefinition.NestedTypes)
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(nestedTypeHandle.GetTypeDefinition(_reader).Name, _reader))
                    yield return nestedTypeHandle.GetNamedType(_reader);
            }
        }
    }
}
