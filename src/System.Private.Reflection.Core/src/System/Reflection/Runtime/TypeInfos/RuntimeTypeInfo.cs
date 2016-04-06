// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Runtime.CompilerServices;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.MethodInfos;
using global::System.Reflection.Runtime.FieldInfos;
using global::System.Reflection.Runtime.PropertyInfos;
using global::System.Reflection.Runtime.EventInfos;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;
using global::Internal.Reflection.Extensibility;
using global::Internal.Reflection.Tracing;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // Abstract base class for all TypeInfo's implemented by the runtime.
    //
    // This base class performs several services:
    //
    //   - Provides default implementations that delegate to AsType() when possible and
    //     returns the "common" error result for narrowly applicable properties (such as those 
    //     that apply only to generic parameters.)
    //
    //   - Inverts the DeclaredMembers/DeclaredX relationship (DeclaredMembers is auto-implemented, others 
    //     are overriden as abstract. This ordering makes more sense when reading from metadata.)
    //
    //   - Overrides many "NotImplemented" members in TypeInfo with abstracts so failure to implement
    //     shows up as build error.
    //
    [DebuggerDisplay("{_debugName}")]
    internal abstract partial class RuntimeTypeInfo : ExtensibleTypeInfo, ITraceableTypeMember
    {
        protected RuntimeTypeInfo()
        {
        }

        public sealed override String AssemblyQualifiedName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_AssemblyQualifiedName(this);
#endif

                return AsType().AssemblyQualifiedName;
            }
        }

        public sealed override Type AsType()
        {
            return this.RuntimeType;
        }

        public sealed override bool IsCOMObject
        {
            get
            {
                return ReflectionCoreExecution.ExecutionEnvironment.IsCOMObject(this.RuntimeType);
            }
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
                RuntimeTypeHandle typeHandle;
                if (this.RuntimeType.InternalTryGetTypeHandle(out typeHandle))
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
                        baseType = this.ReflectionDomain.FoundationTypes.SystemObject;
                }
                return baseType;
            }
        }

        public sealed override bool ContainsGenericParameters
        {
            get
            {
                return this.RuntimeType.InternalIsOpen;
            }
        }

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

        public abstract override bool Equals(Object obj);
        public abstract override int GetHashCode();

        public sealed override String FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_FullName(this);
#endif

                return AsType().FullName;
            }
        }

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

        public sealed override int GenericParameterPosition
        {
            get
            {
                return AsType().GenericParameterPosition;
            }
        }

        public sealed override Type[] GenericTypeArguments
        {
            get
            {
                return AsType().GenericTypeArguments;
            }
        }

        public sealed override EventInfo GetDeclaredEvent(String name)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_GetDeclaredEvent(this, name);
#endif

            if (name == null)
                throw new ArgumentNullException("name");

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
                throw new ArgumentNullException("name");

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
                throw new ArgumentNullException("name");

            TypeInfoCachedData cachedData = this.TypeInfoCachedData;
            return cachedData.GetDeclaredMethod(name);
        }

        public sealed override PropertyInfo GetDeclaredProperty(String name)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_GetDeclaredProperty(this, name);
#endif

            if (name == null)
                throw new ArgumentNullException("name");

            TypeInfoCachedData cachedData = this.TypeInfoCachedData;
            return cachedData.GetDeclaredProperty(name);
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
                RuntimeTypeHandle typeHandle;

                if (this.RuntimeType.InternalTryGetTypeHandle(out typeHandle))
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
                    ReflectionDomain reflectionDomain = this.ReflectionDomain;
                    foreach (QTypeDefRefOrSpec directlyImplementedInterface in this.TypeRefDefOrSpecsForDirectlyImplementedInterfaces)
                    {
                        Type ifc = reflectionDomain.Resolve(directlyImplementedInterface.Reader, directlyImplementedInterface.Handle, typeContext);
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

        public sealed override bool IsAssignableFrom(TypeInfo typeInfo)
        {
            RuntimeTypeInfo toTypeInfo = this;
            RuntimeTypeInfo fromTypeInfo = typeInfo as RuntimeTypeInfo;

            if (fromTypeInfo == null)
                return false;  // Desktop compat: If typeInfo is null, or implemented by a different Reflection implementation, return "false."

            if (toTypeInfo.ReflectionDomain != fromTypeInfo.ReflectionDomain)
                return false;

            if (toTypeInfo.Equals(fromTypeInfo))
                return true;

            RuntimeTypeHandle toTypeHandle = default(RuntimeTypeHandle);
            RuntimeTypeHandle fromTypeHandle = default(RuntimeTypeHandle);
            bool haveTypeHandles = toTypeInfo.RuntimeType.InternalTryGetTypeHandle(out toTypeHandle) && fromTypeInfo.RuntimeType.InternalTryGetTypeHandle(out fromTypeHandle);
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
            return Assignability.IsAssignableFrom(this, typeInfo, fromTypeInfo.ReflectionDomain.FoundationTypes);
        }

        public sealed override bool IsEnum
        {
            get
            {
                return 0 != (Classification & TypeClassification.IsEnum);
            }
        }

        public sealed override bool IsGenericParameter
        {
            get
            {
                return AsType().IsGenericParameter;
            }
        }

        //
        // Left unsealed as generic type definitions and constructed generic types must override.
        //
        public override bool IsGenericType
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

        public sealed override bool IsPrimitive
        {
            get
            {
                return 0 != (Classification & TypeClassification.IsPrimitive);
            }
        }

        public sealed override bool IsSerializable
        {
            get
            {
                return 0 != (this.Attributes & TypeAttributes.Serializable);
            }
        }

        public sealed override bool IsValueType
        {
            get
            {
                return 0 != (Classification & TypeClassification.IsValueType);
            }
        }

        public sealed override Module Module
        {
            get
            {
                return Assembly.ManifestModule;
            }
        }

        public sealed override String Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_Namespace(this);
#endif

                return AsType().Namespace;
            }
        }

        public sealed override Type[] GenericTypeParameters
        {
            get
            {
                RuntimeType[] genericTypeParameters = RuntimeGenericTypeParameters;
                if (genericTypeParameters.Length == 0)
                    return Array.Empty<Type>();
                Type[] result = new Type[genericTypeParameters.Length];
                for (int i = 0; i < genericTypeParameters.Length; i++)
                    result[i] = genericTypeParameters[i];
                return result;
            }
        }

        public sealed override int GetArrayRank()
        {
            return AsType().GetArrayRank();
        }

        public sealed override Type GetElementType()
        {
            return AsType().GetElementType();
        }

        //
        // Left unsealed as generic parameter types must override.
        //
        public override Type[] GetGenericParameterConstraints()
        {
            Debug.Assert(!IsGenericParameter);
            throw new InvalidOperationException(SR.Arg_NotGenericParameter);
        }

        public sealed override Type GetGenericTypeDefinition()
        {
            return AsType().GetGenericTypeDefinition();
        }

        public sealed override Type MakeArrayType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeArrayType(this);
#endif

            return AsType().MakeArrayType();
        }

        public sealed override Type MakeArrayType(int rank)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeArrayType(this, rank);
#endif

            return AsType().MakeArrayType(rank);
        }

        public sealed override Type MakeByRefType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeByRefType(this);
#endif

            return AsType().MakeByRefType();
        }

        public sealed override Type MakeGenericType(params Type[] typeArguments)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeGenericType(this, typeArguments);
#endif

            return AsType().MakeGenericType(typeArguments);
        }

        public sealed override Type MakePointerType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakePointerType(this);
#endif

            return AsType().MakePointerType();
        }

        public sealed override Type DeclaringType
        {
            get
            {
                return this.InternalDeclaringType;
            }
        }

        public sealed override String Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_Name(this);
#endif

                return this.InternalName;
            }
        }

        public sealed override String ToString()
        {
            return AsType().ToString();
        }


        String ITraceableTypeMember.MemberName
        {
            get
            {
                return this.InternalName;
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
        // The non-public version of AsType().
        //
        internal abstract RuntimeType RuntimeType { get; }

        internal ReflectionDomain ReflectionDomain
        {
            get
            {
                return ReflectionCoreExecution.ExecutionDomain;   //@todo: User Reflection Domains not yet supported.
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


        //
        // The non-public version of TypeInfo.GenericTypeParameters (does not array-copy.)
        //
        internal virtual RuntimeType[] RuntimeGenericTypeParameters
        {
            get
            {
                Debug.Assert(!(this is RuntimeNamedTypeInfo));
                return Array.Empty<RuntimeType>();
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


        private String InternalName
        {
            get
            {
                return this.RuntimeType.Name;
            }
        }

        private Type InternalDeclaringType
        {
            get
            {
                return this.RuntimeType.DeclaringType;
            }
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
        private RuntimeType BaseTypeWithoutTheGenericParameterQuirk
        {
            get
            {
                QTypeDefRefOrSpec baseTypeDefRefOrSpec = TypeRefDefOrSpecForBaseType;
                MetadataReader reader = baseTypeDefRefOrSpec.Reader;
                RuntimeType baseType = null;
                ReflectionDomain reflectionDomain = this.ReflectionDomain;
                if (reader != null)
                {
                    Handle typeDefRefOrSpec = baseTypeDefRefOrSpec.Handle;
                    baseType = reflectionDomain.Resolve(reader, typeDefRefOrSpec, this.TypeContext);
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
                        FoundationTypes foundationTypes = this.ReflectionDomain.FoundationTypes;
                        Type enumType = foundationTypes.SystemEnum;
                        Type valueType = foundationTypes.SystemValueType;

                        if (baseType.Equals(enumType))
                            classification |= TypeClassification.IsEnum | TypeClassification.IsValueType;
                        if (baseType.Equals(valueType) && !(this.AsType().Equals(enumType)))
                        {
                            classification |= TypeClassification.IsValueType;
                            Type thisType = this.AsType();
                            foreach (Type primitiveType in this.ReflectionDomain.PrimitiveTypes)
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

