// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.FieldInfos;
using System.Reflection.Runtime.PropertyInfos;
using System.Reflection.Runtime.EventInfos;
using NameFilter = System.Reflection.Runtime.BindingFlagSupport.NameFilter;

using Internal.Reflection.Core.Execution;

//
// The CoreGet() methods on RuntimeTypeInfo provide the raw source material for the Type.Get*() family of apis.
//
// These retrieve directly introduced (not inherited) members whose names match the passed in NameFilter (if NameFilter is null,
// return all members.) To avoid allocating objects, prefer to pass the metadata constant string value handle to NameFilter rather 
// than strings.
//
// The ReflectedType is the type that the Type.Get*() api was invoked on. Use it to establish the returned MemberInfo object's
// ReflectedType.
//
namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        internal virtual IEnumerable<ConstructorInfo> CoreGetDeclaredConstructors(NameFilter optionalNameFilter, RuntimeTypeInfo contextTypeInfo = null)
        {
            //
            // - It may sound odd to get a non-null name filter for a constructor search, but Type.GetMember() is an api that does this.
            //
            // - All GetConstructor() apis act as if BindingFlags.DeclaredOnly were specified. So the ReflectedType will always be the declaring type and so is not passed to this method.
            //
            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (contextTypeInfo == null)
                contextTypeInfo = this;

            if (definingType != null)
            {
                return definingType.CoreGetDeclaredConstructors(optionalNameFilter, contextTypeInfo);
            }

            return CoreGetDeclaredSyntheticConstructors(optionalNameFilter);
        }

        private IEnumerable<ConstructorInfo> CoreGetDeclaredSyntheticConstructors(NameFilter optionalNameFilter)
        {
            foreach (RuntimeConstructorInfo syntheticConstructor in SyntheticConstructors)
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(syntheticConstructor.IsStatic ? ConstructorInfo.TypeConstructorName : ConstructorInfo.ConstructorName))
                    yield return syntheticConstructor;
            }
        }

        internal virtual IEnumerable<MethodInfo> CoreGetDeclaredMethods(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo = null)
        {
            if (contextTypeInfo == null)
                contextTypeInfo = this;

            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                return definingType.CoreGetDeclaredMethods(optionalNameFilter, reflectedType, contextTypeInfo);
            }

            return CoreGetDeclaredSyntheticMethods(optionalNameFilter);
        }

        private IEnumerable<MethodInfo> CoreGetDeclaredSyntheticMethods(NameFilter optionalNameFilter)
        {
            foreach (RuntimeMethodInfo syntheticMethod in SyntheticMethods)
            {
                if (optionalNameFilter == null || optionalNameFilter.Matches(syntheticMethod.Name))
                    yield return syntheticMethod;
            }
        }

        internal virtual IEnumerable<EventInfo> CoreGetDeclaredEvents(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo = null)
        {
            if (contextTypeInfo == null)
                contextTypeInfo = this;

            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                return definingType.CoreGetDeclaredEvents(optionalNameFilter, reflectedType, contextTypeInfo);
            }
            return Empty<EventInfo>.Enumerable;
        }

        internal virtual IEnumerable<FieldInfo> CoreGetDeclaredFields(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo = null)
        {
            if (contextTypeInfo == null)
                contextTypeInfo = this;

            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                return definingType.CoreGetDeclaredFields(optionalNameFilter, reflectedType, contextTypeInfo);
            }
            return Empty<FieldInfo>.Enumerable;
        }

        internal virtual IEnumerable<PropertyInfo> CoreGetDeclaredProperties(NameFilter optionalNameFilter, RuntimeTypeInfo reflectedType, RuntimeTypeInfo contextTypeInfo = null)
        {
            if (contextTypeInfo == null)
                contextTypeInfo = this;

            RuntimeNamedTypeInfo definingType = AnchoringTypeDefinitionForDeclaredMembers;
            if (definingType != null)
            {
                return definingType.CoreGetDeclaredProperties(optionalNameFilter, reflectedType, contextTypeInfo);
            }

            return Empty<PropertyInfo>.Enumerable;
        }

        //
        // - All GetNestedType() apis act as if BindingFlags.DeclaredOnly were specified. So the ReflectedType will always be the declaring type and so is not passed to this method.
        //
        // This method is left unsealed as RuntimeNamedTypeInfo and others need to override with specific implementations.
        //
        internal virtual IEnumerable<Type> CoreGetDeclaredNestedTypes(NameFilter optionalNameFilter)
        {
            return Array.Empty<Type>();
        }
    }

    internal sealed partial class RuntimeConstructedGenericTypeInfo
    {
        internal sealed override IEnumerable<Type> CoreGetDeclaredNestedTypes(NameFilter optionalNameFilter)
        {
            return GenericTypeDefinitionTypeInfo.CoreGetDeclaredNestedTypes(optionalNameFilter);
        }
    }

    internal sealed partial class RuntimeBlockedTypeInfo
    {
        internal sealed override IEnumerable<Type> CoreGetDeclaredNestedTypes(NameFilter optionalNameFilter)
        {
            return Array.Empty<Type>();
        }
    }

    internal sealed partial class RuntimeNoMetadataNamedTypeInfo
    {
        internal sealed override IEnumerable<Type> CoreGetDeclaredNestedTypes(NameFilter optionalNameFilter)
        {
            throw ReflectionCoreExecution.ExecutionDomain.CreateMissingMetadataException(this);
        }
    }
}

