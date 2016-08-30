// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.FieldInfos;
using System.Reflection.Runtime.PropertyInfos;
using System.Reflection.Runtime.EventInfos;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;
using Internal.Reflection.Augments;

using Internal.Metadata.NativeFormat;

using IRuntimeImplementedType = Internal.Reflection.Core.NonPortable.IRuntimeImplementedType;

using StructLayoutAttribute = System.Runtime.InteropServices.StructLayoutAttribute;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // Abstract base class for all TypeInfo's implemented by the runtime.
    //
    // This base class performs several services:
    //
    //   - Provides default implementations whenever possible. Some of these 
    //     return the "common" error result for narrowly applicable properties (such as those 
    //     that apply only to generic parameters.)
    //
    //   - Inverts the DeclaredMembers/DeclaredX relationship (DeclaredMembers is auto-implemented, others 
    //     are overriden as abstract. This ordering makes more sense when reading from metadata.)
    //
    //   - Overrides many "NotImplemented" members in TypeInfo with abstracts so failure to implement
    //     shows up as build error.
    //
    [DebuggerDisplay("{_debugName}")]
    internal abstract partial class RuntimeTypeInfo : TypeInfo, ITraceableTypeMember, ICloneable, IRuntimeImplementedType
    {
        protected RuntimeTypeInfo()
        {
        }

        public abstract override Assembly Assembly { get; }

        public sealed override string AssemblyQualifiedName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_AssemblyQualifiedName(this);
#endif

                string fullName = FullName;
                if (fullName == null)   // Some Types (such as generic parameters) return null for FullName by design.
                    return null;
                string assemblyName = InternalFullNameOfAssembly;
                return fullName + ", " + assemblyName;
            }
        }

        public sealed override Type AsType()
        {
            return this;
        }

        public sealed override Type BaseType
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_BaseType(this);
#endif

                // If this has a RuntimeTypeHandle, let the underlying runtime engine have the first crack. If it refuses, fall back to metadata.
                RuntimeTypeHandle typeHandle = InternalTypeHandleIfAvailable;
                if (!typeHandle.IsNull())
                {
                    RuntimeTypeHandle baseTypeHandle;
                    if (ReflectionCoreExecution.ExecutionEnvironment.TryGetBaseType(typeHandle, out baseTypeHandle))
                        return Type.GetTypeFromHandle(baseTypeHandle);
                }

                Type baseType = BaseTypeWithoutTheGenericParameterQuirk;
                if (baseType != null && baseType.IsGenericParameter)
                {
                    // Desktop quirk: a generic parameter whose constraint is another generic parameter reports its BaseType as System.Object
                    // unless that other generic parameter has a "class" constraint.
                    GenericParameterAttributes genericParameterAttributes = baseType.GetTypeInfo().GenericParameterAttributes;
                    if (0 == (genericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint))
                        baseType = ReflectionCoreExecution.ExecutionDomain.FoundationTypes.SystemObject;
                }
                return baseType;
            }
        }

        public abstract override bool ContainsGenericParameters { get; }

        //
        // Left unsealed so that RuntimeNamedTypeInfo and RuntimeConstructedGenericTypeInfo and RuntimeGenericParameterTypeInfo can override.
        //
        public override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_CustomAttributes(this);
#endif

                Debug.Assert(IsArray || IsByRef || IsPointer);
                return Empty<CustomAttributeData>.Enumerable;
            }
        }

        public sealed override IEnumerable<ConstructorInfo> DeclaredConstructors
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredConstructors(this);
#endif

                return GetDeclaredConstructorsInternal(this.AnchoringTypeDefinitionForDeclaredMembers);
            }
        }

        public sealed override IEnumerable<EventInfo> DeclaredEvents
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredEvents(this);
#endif

                return GetDeclaredEventsInternal(this.AnchoringTypeDefinitionForDeclaredMembers, null);
            }
        }

        public sealed override IEnumerable<FieldInfo> DeclaredFields
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredFields(this);
#endif

                return GetDeclaredFieldsInternal(this.AnchoringTypeDefinitionForDeclaredMembers, null);
            }
        }

        public sealed override IEnumerable<MemberInfo> DeclaredMembers
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredMembers(this);
#endif

                return GetDeclaredMembersInternal(
                    this.DeclaredMethods,
                    this.DeclaredConstructors,
                    this.DeclaredProperties,
                    this.DeclaredEvents,
                    this.DeclaredFields,
                    this.DeclaredNestedTypes);
            }
        }

        public sealed override IEnumerable<MethodInfo> DeclaredMethods
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredMethods(this);
#endif

                return GetDeclaredMethodsInternal(this.AnchoringTypeDefinitionForDeclaredMembers, null);
            }
        }

        //
        // Left unsealed as named types must override.
        //
        public override IEnumerable<TypeInfo> DeclaredNestedTypes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredNestedTypes(this);
#endif

                Debug.Assert(!(this is RuntimeNamedTypeInfo));
                return Empty<TypeInfo>.Enumerable;
            }
        }

        public sealed override IEnumerable<PropertyInfo> DeclaredProperties
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredProperties(this);
#endif

                return GetDeclaredPropertiesInternal(this.AnchoringTypeDefinitionForDeclaredMembers, null);
            }
        }

        //
        // Left unsealed as generic parameter types must override.
        //
        public override MethodBase DeclaringMethod
        {
            get
            {
                Debug.Assert(!IsGenericParameter);
                throw new InvalidOperationException(SR.Arg_NotGenericParameter);
            }
        }

        //
        // Equals()/GetHashCode()
        //
        // RuntimeTypeInfo objects are interned to preserve the app-compat rule that Type objects (which are the same as TypeInfo objects)
        // can be compared using reference equality.
        //
        // We use weak pointers to intern the objects. This means we can use instance equality to implement Equals() but we cannot use
        // the instance hashcode to implement GetHashCode() (otherwise, the hash code will not be stable if the TypeInfo is released and recreated.)
        // Thus, we override and seal Equals() here but defer to a flavor-specific hash code implementation.
        //
        public sealed override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj);
        }

        public sealed override int GetHashCode()
        {
            return InternalGetHashCode();
        }

        public abstract override string FullName { get; }

        //
        // Left unsealed as generic parameter types must override.
        //
        public override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                Debug.Assert(!IsGenericParameter);
                throw new InvalidOperationException(SR.Arg_NotGenericParameter);
            }
        }

        //
        // Left unsealed as generic parameter types must override this.
        //
        public override int GenericParameterPosition
        {
            get
            {
                Debug.Assert(!IsGenericParameter);
                throw new InvalidOperationException(SR.Arg_NotGenericParameter);
            }
        }

        public sealed override Type[] GenericTypeArguments
        {
            get
            {
                return InternalRuntimeGenericTypeArguments.CloneTypeArray();
            }
        }

        public sealed override EventInfo GetDeclaredEvent(String name)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_GetDeclaredEvent(this, name);
#endif

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            TypeInfoCachedData cachedData = this.TypeInfoCachedData;
            return cachedData.GetDeclaredEvent(name);
        }

        public sealed override FieldInfo GetDeclaredField(String name)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_GetDeclaredField(this, name);
#endif

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            TypeInfoCachedData cachedData = this.TypeInfoCachedData;
            return cachedData.GetDeclaredField(name);
        }

        public sealed override MethodInfo GetDeclaredMethod(String name)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_GetDeclaredMethod(this, name);
#endif

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            TypeInfoCachedData cachedData = this.TypeInfoCachedData;
            return cachedData.GetDeclaredMethod(name);
        }

        public sealed override IEnumerable<MethodInfo> GetDeclaredMethods(string name)
        {
            foreach (MethodInfo method in DeclaredMethods)
            {
                if (method.Name == name)
                    yield return method;
            }
        }

        public sealed override TypeInfo GetDeclaredNestedType(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            TypeInfo match = null;
            foreach (TypeInfo nestedType in DeclaredNestedTypes)
            {
                if (nestedType.Name == name)
                {
                    if (match != null)
                        throw new AmbiguousMatchException();

                    match = nestedType;
                }
            }
            return match;
        }

        public sealed override PropertyInfo GetDeclaredProperty(String name)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_GetDeclaredProperty(this, name);
#endif

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            TypeInfoCachedData cachedData = this.TypeInfoCachedData;
            return cachedData.GetDeclaredProperty(name);
        }

        public sealed override MemberInfo[] GetDefaultMembers()
        {
            Type defaultMemberAttributeType = typeof(DefaultMemberAttribute);
            for (Type type = this; type != null; type = type.BaseType)
            {
                foreach (CustomAttributeData attribute in type.CustomAttributes)
                {
                    if (attribute.AttributeType == defaultMemberAttributeType)
                    {
                        // NOTE: Neither indexing nor cast can fail here. Any attempt to use fewer than 1 argument
                        // or a non-string argument would correctly trigger MissingMethodException before
                        // we reach here as that would be an attempt to reference a non-existent DefaultMemberAttribute
                        // constructor.
                        Debug.Assert(attribute.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].Value is string);

                        string memberName = (string)(attribute.ConstructorArguments[0].Value);
                        return GetMember(memberName);
                    }
                }
            }

            return Array.Empty<MemberInfo>();
        }

        public sealed override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_InterfaceMap);
        }

        //
        // Implements the correct GUID behavior for all "constructed" types (i.e. returning an all-zero GUID.) Left unsealed
        // so that RuntimeNamedTypeInfo can override.
        //
        public override Guid GUID
        {
            get
            {
                return Guid.Empty;
            }
        }

        public sealed override IEnumerable<Type> ImplementedInterfaces
        {
            get
            {
                LowLevelListWithIList<Type> result = new LowLevelListWithIList<Type>();

                bool done = false;

                // If this has a RuntimeTypeHandle, let the underlying runtime engine have the first crack. If it refuses, fall back to metadata.
                RuntimeTypeHandle typeHandle = InternalTypeHandleIfAvailable;
                if (!typeHandle.IsNull())
                {
                    IEnumerable<RuntimeTypeHandle> implementedInterfaces = ReflectionCoreExecution.ExecutionEnvironment.TryGetImplementedInterfaces(typeHandle);
                    if (implementedInterfaces != null)
                    {
                        done = true;

                        foreach (RuntimeTypeHandle th in implementedInterfaces)
                        {
                            result.Add(Type.GetTypeFromHandle(th));
                        }
                    }
                }

                if (!done)
                {
                    TypeContext typeContext = this.TypeContext;
                    Type baseType = this.BaseTypeWithoutTheGenericParameterQuirk;
                    if (baseType != null)
                        result.AddRange(baseType.GetTypeInfo().ImplementedInterfaces);
                    foreach (QTypeDefRefOrSpec directlyImplementedInterface in this.TypeRefDefOrSpecsForDirectlyImplementedInterfaces)
                    {
                        Type ifc = directlyImplementedInterface.Handle.Resolve(directlyImplementedInterface.Reader, typeContext);
                        if (result.Contains(ifc))
                            continue;
                        result.Add(ifc);
                        foreach (Type indirectIfc in ifc.GetTypeInfo().ImplementedInterfaces)
                        {
                            if (result.Contains(indirectIfc))
                                continue;
                            result.Add(indirectIfc);
                        }
                    }
                }

                return result.AsNothingButIEnumerable();
            }
        }

        public sealed override bool IsAssignableFrom(Type c)
        {
            if (c == null)
                return false;

            return IsAssignableFrom(c.GetTypeInfo());
        }

        public sealed override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            RuntimeTypeInfo toTypeInfo = this;

            if (typeInfo == null || !typeInfo.IsRuntimeImplemented())
                return false;  // Desktop compat: If typeInfo is null, or implemented by a different Reflection implementation, return "false."

            RuntimeTypeInfo fromTypeInfo = typeInfo.CastToRuntimeTypeInfo();

            if (toTypeInfo.Equals(fromTypeInfo))
                return true;

            RuntimeTypeHandle toTypeHandle = toTypeInfo.InternalTypeHandleIfAvailable;
            RuntimeTypeHandle fromTypeHandle = fromTypeInfo.InternalTypeHandleIfAvailable;
            bool haveTypeHandles = !(toTypeHandle.IsNull() || fromTypeHandle.IsNull());
            if (haveTypeHandles)
            {
                // If both types have type handles, let MRT handle this. It's not dependent on metadata.
                if (ReflectionCoreExecution.ExecutionEnvironment.IsAssignableFrom(toTypeHandle, fromTypeHandle))
                    return true;

                // Runtime IsAssignableFrom does not handle casts from generic type definitions: always returns false. For those, we fall through to the 
                // managed implementation. For everyone else, return "false".
                //
                // Runtime IsAssignableFrom does not handle pointer -> UIntPtr cast.
                if (!(fromTypeInfo.IsGenericTypeDefinition || fromTypeInfo.IsPointer))
                    return false;
            }

            // If we got here, the types are open, or reduced away, or otherwise lacking in type handles. Perform the IsAssignability check in managed code.
            return Assignability.IsAssignableFrom(this, typeInfo, ReflectionCoreExecution.ExecutionDomain.FoundationTypes);
        }

        //
        // Left unsealed as constructed generic types must override.
        //
        public override bool IsConstructedGenericType
        {
            get
            {
                return false;
            }
        }

        //
        // Left unsealed as generic parameter types must override.
        //
        public override bool IsGenericParameter
        {
            get
            {
                return false;
            }
        }

        //
        // Left unsealed as generic type definitions must override.
        //
        public override bool IsGenericTypeDefinition
        {
            get
            {
                return false;
            }
        }

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
            }
        }

        public sealed override Module Module
        {
            get
            {
                return Assembly.ManifestModule;
            }
        }

        public abstract override string Namespace { get; }

        public sealed override Type[] GenericTypeParameters
        {
            get
            {
                return RuntimeGenericTypeParameters.CloneTypeArray();
            }
        }

        //
        // Left unsealed as array types must override this.
        //
        public override int GetArrayRank()
        {
            Debug.Assert(!IsArray);
            throw new ArgumentException(SR.Argument_HasToBeArrayClass);
        }

        public sealed override Type GetElementType()
        {
            return InternalRuntimeElementType;
        }

        //
        // Left unsealed as generic parameter types must override.
        //
        public override Type[] GetGenericParameterConstraints()
        {
            Debug.Assert(!IsGenericParameter);
            throw new InvalidOperationException(SR.Arg_NotGenericParameter);
        }

        //
        // Left unsealed as IsGenericType types must override this.
        //
        public override Type GetGenericTypeDefinition()
        {
            Debug.Assert(!IsGenericType);
            throw new InvalidOperationException(SR.InvalidOperation_NotGenericType);
        }

        public sealed override Type MakeArrayType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeArrayType(this);
#endif

            // Do not implement this as a call to MakeArrayType(1) - they are not interchangable. MakeArrayType() returns a
            // vector type ("SZArray") while MakeArrayType(1) returns a multidim array of rank 1. These are distinct types
            // in the ECMA model and in CLR Reflection.
            return this.GetArrayType().AsType();
        }

        public sealed override Type MakeArrayType(int rank)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeArrayType(this, rank);
#endif

            if (rank <= 0)
                throw new IndexOutOfRangeException();
            return this.GetMultiDimArrayType(rank).AsType();
        }

        public sealed override Type MakeByRefType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeByRefType(this);
#endif
            return this.GetByRefType().AsType();
        }

        public sealed override Type MakeGenericType(params Type[] typeArguments)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeGenericType(this, typeArguments);
#endif

            if (typeArguments == null)
                throw new ArgumentNullException(nameof(typeArguments));

            if (!IsGenericTypeDefinition)
                throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericTypeDefinition, this));

            // We intentionally don't validate the number of arguments or their suitability to the generic type's constraints.
            // In a pay-for-play world, this can cause needless MissingMetadataExceptions. There is no harm in creating
            // the Type object for an inconsistent generic type - no EEType will ever match it so any attempt to "invoke" it
            // will throw an exception.
            for (int i = 0; i < typeArguments.Length; i++)
            {
                if (typeArguments[i] == null)
                    throw new ArgumentNullException();

                if (!typeArguments[i].IsRuntimeImplemented())
                    throw new PlatformNotSupportedException(SR.PlatformNotSupported_MakeGenericType); // "PlatformNotSupported" because on desktop, passing in a foreign type is allowed and creates a RefEmit.TypeBuilder
            }
            return this.GetConstructedGenericType(typeArguments.ToRuntimeTypeInfoArray()).AsType();
        }

        public sealed override Type MakePointerType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakePointerType(this);
#endif

            return this.GetPointerType().AsType();
        }

        public sealed override Type DeclaringType
        {
            get
            {
                return this.InternalDeclaringType;
            }
        }

        public sealed override string Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_Name(this);
#endif

                Type rootCauseForFailure = null;
                string name = InternalGetNameIfAvailable(ref rootCauseForFailure);
                if (name == null)
                    throw ReflectionCoreExecution.ExecutionDomain.CreateMissingMetadataException(rootCauseForFailure);
                return name;
            }
        }

        public abstract override StructLayoutAttribute StructLayoutAttribute { get; }

        public abstract override string ToString();

        public sealed override RuntimeTypeHandle TypeHandle
        {
            get
            {
                RuntimeTypeHandle typeHandle = InternalTypeHandleIfAvailable;
                if (!typeHandle.IsNull())
                    return typeHandle;

                if (IsByRef)
                    throw new PlatformNotSupportedException(SR.PlatformNotSupported_NoTypeHandleForByRef);

                // If a constructed type doesn't have an type handle, it's either because the reducer tossed it (in which case,
                // we would thrown a MissingMetadataException when attempting to construct the type) or because one of
                // component types contains open type parameters. Since we eliminated the first case, it must be the second.
                // Throwing PlatformNotSupported since the desktop does, in fact, create type handles for open types.
                if (HasElementType || IsConstructedGenericType || IsGenericParameter)
                    throw new PlatformNotSupportedException(SR.PlatformNotSupported_NoTypeHandleForOpenTypes);

                // If got here, this is a "plain old type" that has metadata but no type handle. We can get here if the only
                // representation of the type is in the native metadata and there's no EEType at the runtime side.
                // If you squint hard, this is a missing metadata situation - the metadata is missing on the runtime side - and
                // the action for the user to take is the same: go mess with RD.XML.
                throw ReflectionCoreExecution.ExecutionDomain.CreateMissingMetadataException(this);
            }
        }

        public sealed override Type UnderlyingSystemType
        {
            get
            {
                return this;
            }
        }

        protected abstract override TypeAttributes GetAttributeFlagsImpl();

        //
        // Left unsealed so that RuntimeHasElementTypeInfo can override.
        //
        protected override bool HasElementTypeImpl()
        {
            return false;
        }

        protected sealed override TypeCode GetTypeCodeImpl()
        {
            return ReflectionAugments.GetRuntimeTypeCode(this);
        }

        protected abstract int InternalGetHashCode();

        //
        // Left unsealed since array types must override.
        //
        protected override bool IsArrayImpl()
        {
            return false;
        }

        //
        // Left unsealed since byref types must override.
        //
        protected override bool IsByRefImpl()
        {
            return false;
        }

        //
        // Left unsealed since pointer types must override.
        //
        protected override bool IsPointerImpl()
        {
            return false;
        }

        protected sealed override bool IsCOMObjectImpl()
        {
            return ReflectionCoreExecution.ExecutionEnvironment.IsCOMObject(this);
        }

        protected sealed override bool IsPrimitiveImpl()
        {
            return 0 != (Classification & TypeClassification.IsPrimitive);
        }

        protected sealed override bool IsValueTypeImpl()
        {
            return 0 != (Classification & TypeClassification.IsValueType);
        }

        String ITraceableTypeMember.MemberName
        {
            get
            {
                string name = InternalNameIfAvailable;
                return name ?? string.Empty;
            }
        }

        Type ITraceableTypeMember.ContainingType
        {
            get
            {
                return this.InternalDeclaringType;
            }
        }

        //
        // Returns the anchoring typedef that declares the members that this type wants returned by the Declared*** properties.
        // The Declared*** properties will project the anchoring typedef's members by overriding their DeclaringType property with "this"
        // and substituting the value of this.TypeContext into any generic parameters.
        //
        // Default implementation returns null which causes the Declared*** properties to return no members.
        //
        // Note that this does not apply to DeclaredNestedTypes. Nested types and their containers have completely separate generic instantiation environments
        // (despite what C# might lead you to think.) Constructed generic types return the exact same same nested types that its generic type definition does
        // - i.e. their DeclaringTypes refer back to the generic type definition, not the constructed generic type.)
        //
        // Note also that we cannot use this anchoring concept for base types because of generic parameters. Generic parameters return
        // a base class and interface list based on its constraints.
        //
        internal virtual RuntimeNamedTypeInfo AnchoringTypeDefinitionForDeclaredMembers
        {
            get
            {
                return null;
            }
        }

        //
        // Return all declared events whose name matches "optionalNameFilter". If optionalNameFilter is null, return them all.
        //
        internal IEnumerable<RuntimeEventInfo> GetDeclaredEventsInternal(RuntimeNamedTypeInfo definingType, String optionalNameFilter)
        {
            if (definingType != null)
            {
                // We require the caller to pass a value that we could calculate ourselves because we're an iterator and we
                // don't want any MissingMetadataException that AnchoringType throws to be deferred.
                Debug.Assert(definingType.Equals(this.AnchoringTypeDefinitionForDeclaredMembers));

                MetadataReader reader = definingType.Reader;
                foreach (EventHandle eventHandle in definingType.DeclaredEventHandles)
                {
                    if (optionalNameFilter == null || eventHandle.GetEvent(reader).Name.StringEquals(optionalNameFilter, reader))
                        yield return RuntimeEventInfo.GetRuntimeEventInfo(eventHandle, definingType, this);
                }
            }
        }

        //
        // Return all declared fields whose name matches "optionalNameFilter". If optionalNameFilter is null, return them all.
        //
        internal IEnumerable<RuntimeFieldInfo> GetDeclaredFieldsInternal(RuntimeNamedTypeInfo definingType, String optionalNameFilter)
        {
            if (definingType != null)
            {
                // We require the caller to pass a value that we could calculate ourselves because we're an iterator and we
                // don't want any MissingMetadataException that AnchoringType throws to be deferred.
                Debug.Assert(definingType.Equals(this.AnchoringTypeDefinitionForDeclaredMembers));

                MetadataReader reader = definingType.Reader;
                foreach (FieldHandle fieldHandle in definingType.DeclaredFieldHandles)
                {
                    if (optionalNameFilter == null || fieldHandle.GetField(reader).Name.StringEquals(optionalNameFilter, reader))
                        yield return RuntimeFieldInfo.GetRuntimeFieldInfo(fieldHandle, definingType, this);
                }
            }
        }

        //
        // Return all declared methods whose name matches "optionalNameFilter". If optionalNameFilter is null, return them all.
        //
        internal IEnumerable<RuntimeMethodInfo> GetDeclaredMethodsInternal(RuntimeNamedTypeInfo definingType, String optionalNameFilter)
        {
            if (definingType != null)
            {
                // We require the caller to pass a value that we could calculate ourselves because we're an iterator and we
                // don't want any MissingMetadataException that AnchoringType throws to be deferred.
                Debug.Assert(definingType.Equals(this.AnchoringTypeDefinitionForDeclaredMembers));

                MetadataReader reader = definingType.Reader;
                foreach (MethodHandle methodHandle in definingType.DeclaredMethodAndConstructorHandles)
                {
                    Method method = methodHandle.GetMethod(reader);

                    if ((optionalNameFilter != null) && !method.Name.StringEquals(optionalNameFilter, reader))
                        continue;

                    if (MetadataReaderExtensions.IsConstructor(ref method, reader))
                        continue;
                    yield return RuntimeNamedMethodInfo.GetRuntimeNamedMethodInfo(methodHandle, definingType, this);
                }
            }

            foreach (RuntimeMethodInfo syntheticMethod in SyntheticMethods)
            {
                if (optionalNameFilter == null || optionalNameFilter == syntheticMethod.Name)
                {
                    yield return syntheticMethod;
                }
            }
        }

        //
        // Return all declared properties whose name matches "optionalNameFilter". If optionalNameFilter is null, return them all.
        //
        internal IEnumerable<RuntimePropertyInfo> GetDeclaredPropertiesInternal(RuntimeNamedTypeInfo definingType, String optionalNameFilter)
        {
            if (definingType != null)
            {
                // We require the caller to pass a value that we could calculate ourselves because we're an iterator and we
                // don't want any MissingMetadataException that AnchoringType throws to be deferred.
                Debug.Assert(definingType.Equals(this.AnchoringTypeDefinitionForDeclaredMembers));

                MetadataReader reader = definingType.Reader;
                foreach (PropertyHandle propertyHandle in definingType.DeclaredPropertyHandles)
                {
                    if (optionalNameFilter == null || propertyHandle.GetProperty(reader).Name.StringEquals(optionalNameFilter, reader))
                        yield return RuntimePropertyInfo.GetRuntimePropertyInfo(propertyHandle, definingType, this);
                }
            }
        }

        internal abstract Type InternalDeclaringType { get; }

        //
        // Return the full name of the "defining assembly" for the purpose of computing TypeInfo.AssemblyQualifiedName;
        //
        internal abstract string InternalFullNameOfAssembly { get; }

        internal abstract string InternalGetNameIfAvailable(ref Type rootCauseForFailure);

        // Left unsealed so that multidim arrays can override.
        internal virtual bool InternalIsMultiDimArray
        {
            get
            {
                return false;
            }
        }

        internal string InternalNameIfAvailable
        {
            get
            {
                Type ignore = null;
                return InternalGetNameIfAvailable(ref ignore);
            }
        }

        //
        // Left unsealed as HasElement types must override this.
        //
        internal virtual RuntimeTypeInfo InternalRuntimeElementType
        {
            get
            {
                Debug.Assert(!HasElementType);
                return null;
            }
        }

        //
        // Left unsealed as constructed generic types must override this.
        //
        internal virtual RuntimeTypeInfo[] InternalRuntimeGenericTypeArguments
        {
            get
            {
                Debug.Assert(!IsConstructedGenericType);
                return Array.Empty<RuntimeTypeInfo>();
            }
        }

        internal abstract RuntimeTypeHandle InternalTypeHandleIfAvailable { get; }

        //
        // The non-public version of TypeInfo.GenericTypeParameters (does not array-copy.)
        //
        internal virtual RuntimeTypeInfo[] RuntimeGenericTypeParameters
        {
            get
            {
                Debug.Assert(!(this is RuntimeNamedTypeInfo));
                return Array.Empty<RuntimeTypeInfo>();
            }
        }

        //
        // Normally returns empty: Overridden by array types to return constructors.
        //
        internal virtual IEnumerable<RuntimeConstructorInfo> SyntheticConstructors
        {
            get
            {
                return Empty<RuntimeConstructorInfo>.Enumerable;
            }
        }

        //
        // Normally returns empty: Overridden by array types to return the "Get" and "Set" methods.
        //
        internal virtual IEnumerable<RuntimeMethodInfo> SyntheticMethods
        {
            get
            {
                return Empty<RuntimeMethodInfo>.Enumerable;
            }
        }

        //
        // Returns the base type as a typeDef, Ref, or Spec. Default behavior is to QTypeDefRefOrSpec.Null, which causes BaseType to return null.
        //
        internal virtual QTypeDefRefOrSpec TypeRefDefOrSpecForBaseType
        {
            get
            {
                return QTypeDefRefOrSpec.Null;
            }
        }

        //
        // Returns the *directly implemented* interfaces as typedefs, specs or refs. ImplementedInterfaces will take care of the transitive closure and
        // insertion of the TypeContext.
        //
        internal virtual QTypeDefRefOrSpec[] TypeRefDefOrSpecsForDirectlyImplementedInterfaces
        {
            get
            {
                return Array.Empty<QTypeDefRefOrSpec>();
            }
        }

        //
        // Returns the generic parameter substitutions to use when enumerating declared members, base class and implemented interfaces.
        //
        internal virtual TypeContext TypeContext
        {
            get
            {
                return new TypeContext(null, null);
            }
        }

        //
        // Note: This can be (and is) called multiple times. We do not do this work in the constructor as calling ToString()
        // in the constructor causes some serious recursion issues.
        //
        internal void EstablishDebugName()
        {
            bool populateDebugNames = DeveloperExperienceState.DeveloperExperienceModeEnabled;
#if DEBUG
            populateDebugNames = true;
#endif
            if (!populateDebugNames)
                return;

            if (_debugName == null)
            {
                _debugName = "Constructing..."; // Protect against any inadvertent reentrancy.
                String debugName;
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    debugName = this.GetTraceString();  // If tracing on, call this.GetTraceString() which only gives you useful strings when metadata is available but doesn't pollute the ETW trace.
                else
#endif
                debugName = this.ToString();
                if (debugName == null)
                    debugName = "";
                _debugName = debugName;
            }
            return;
        }

        //
        // This internal method implements BaseType without the following desktop quirk: 
        //
        //     class Foo<X,Y> 
        //       where X:Y
        //       where Y:MyReferenceClass
        //
        // The desktop reports "X"'s base type as "System.Object" rather than "Y", even though it does
        // report any interfaces that are in MyReferenceClass's interface list. 
        //
        // This seriously messes up the implementation of RuntimeTypeInfo.ImplementedInterfaces which assumes
        // that it can recover the transitive interface closure by combining the directly mentioned interfaces and
        // the BaseType's own interface closure.
        //
        // To implement this with the least amount of code smell, we'll implement the idealized version of BaseType here
        // and make the special-case adjustment in the public version of BaseType.
        //
        private RuntimeTypeInfo BaseTypeWithoutTheGenericParameterQuirk
        {
            get
            {
                QTypeDefRefOrSpec baseTypeDefRefOrSpec = TypeRefDefOrSpecForBaseType;
                MetadataReader reader = baseTypeDefRefOrSpec.Reader;
                RuntimeTypeInfo baseType = null;
                if (reader != null)
                {
                    Handle typeDefRefOrSpec = baseTypeDefRefOrSpec.Handle;
                    baseType = typeDefRefOrSpec.Resolve(reader, this.TypeContext);
                }
                return baseType;
            }
        }

        //
        // Returns a latched set of flags indicating the value of IsValueType, IsEnum, etc.
        //
        private TypeClassification Classification
        {
            get
            {
                if (_lazyClassification == 0)
                {
                    TypeClassification classification = TypeClassification.Computed;
                    Type baseType = this.BaseType;
                    if (baseType != null)
                    {
                        FoundationTypes foundationTypes = ReflectionCoreExecution.ExecutionDomain.FoundationTypes;
                        Type enumType = foundationTypes.SystemEnum;
                        Type valueType = foundationTypes.SystemValueType;

                        if (baseType.Equals(enumType))
                            classification |= TypeClassification.IsEnum | TypeClassification.IsValueType;
                        if (baseType.Equals(valueType) && !(this.AsType().Equals(enumType)))
                        {
                            classification |= TypeClassification.IsValueType;
                            Type thisType = this.AsType();
                            foreach (Type primitiveType in ReflectionCoreExecution.ExecutionDomain.PrimitiveTypes)
                            {
                                if (thisType.Equals(primitiveType))
                                {
                                    classification |= TypeClassification.IsPrimitive;
                                    break;
                                }
                            }
                        }
                    }
                    _lazyClassification = classification;
                }
                return _lazyClassification;
            }
        }

        [Flags]
        private enum TypeClassification
        {
            Computed = 0x00000001,    // Always set (to indicate that the lazy evaluation has occurred)
            IsValueType = 0x00000002,
            IsEnum = 0x00000004,
            IsPrimitive = 0x00000008,
        }

        object ICloneable.Clone()
        {
            return this;
        }

        //
        // Return all declared members. This may look like a silly code sequence to wrap inside a helper but we want to separate the iterator from
        // the actual calls to get the sub-enumerations as we want any MissingMetadataException thrown by those
        // calls to happen at the time DeclaredMembers is called.
        //
        private IEnumerable<MemberInfo> GetDeclaredMembersInternal(
            IEnumerable<MethodInfo> methods,
            IEnumerable<ConstructorInfo> constructors,
            IEnumerable<PropertyInfo> properties,
            IEnumerable<EventInfo> events,
            IEnumerable<FieldInfo> fields,
            IEnumerable<TypeInfo> nestedTypes
            )
        {
            foreach (MemberInfo member in methods)
                yield return member;
            foreach (MemberInfo member in constructors)
                yield return member;
            foreach (MemberInfo member in properties)
                yield return member;
            foreach (MemberInfo member in events)
                yield return member;
            foreach (MemberInfo member in fields)
                yield return member;
            foreach (MemberInfo member in nestedTypes)
                yield return member;
        }

        //
        // Return all declared constructors.
        //
        private IEnumerable<RuntimeConstructorInfo> GetDeclaredConstructorsInternal(RuntimeNamedTypeInfo definingType)
        {
            if (definingType != null)
            {
                // We require the caller to pass a value that we could calculate ourselves because we're an iterator and we
                // don't want any MissingMetadataException that AnchoringType throws to be deferred.
                Debug.Assert(definingType.Equals(this.AnchoringTypeDefinitionForDeclaredMembers));

                RuntimeTypeInfo contextType = this;
                foreach (MethodHandle methodHandle in definingType.DeclaredConstructorHandles)
                {
                    yield return RuntimePlainConstructorInfo.GetRuntimePlainConstructorInfo(methodHandle, definingType, contextType);
                }
            }

            foreach (RuntimeConstructorInfo syntheticConstructor in SyntheticConstructors)
            {
                yield return syntheticConstructor;
            }
        }

        private volatile TypeClassification _lazyClassification;

        private TypeInfoCachedData TypeInfoCachedData
        {
            get
            {
                TypeInfoCachedData cachedData = _lazyTypeInfoCachedData;
                if (cachedData != null)
                    return cachedData;
                _lazyTypeInfoCachedData = cachedData = new TypeInfoCachedData(this);
                return cachedData;
            }
        }

        private volatile TypeInfoCachedData _lazyTypeInfoCachedData;

        private String _debugName;
    }
}

