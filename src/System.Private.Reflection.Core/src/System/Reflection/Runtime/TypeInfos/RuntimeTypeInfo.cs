// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;

using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;
using Internal.Reflection.Augments;

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
                    GenericParameterAttributes genericParameterAttributes = baseType.GenericParameterAttributes;
                    if (0 == (genericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint))
                        baseType = CommonRuntimeTypes.Object;
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

        public sealed override MemberInfo[] GetDefaultMembers()
        {
            string defaultMemberName = GetDefaultMemberName();
            return defaultMemberName != null ? GetMember(defaultMemberName) : Array.Empty<MemberInfo>();
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
                        result.AddRange(baseType.GetInterfaces());
                    foreach (QTypeDefRefOrSpec directlyImplementedInterface in this.TypeRefDefOrSpecsForDirectlyImplementedInterfaces)
                    {
                        Type ifc = directlyImplementedInterface.Resolve(typeContext);
                        if (result.Contains(ifc))
                            continue;
                        result.Add(ifc);
                        foreach (Type indirectIfc in ifc.GetInterfaces())
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

        public sealed override bool IsAssignableFrom(TypeInfo typeInfo) => IsAssignableFrom((Type)typeInfo);

        public sealed override bool IsAssignableFrom(Type c)
        {
            if (c == null)
                return false;

            Type typeInfo = c;
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
            return Assignability.IsAssignableFrom(this, typeInfo);
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

        public sealed override bool IsSzArray
        {
            get
            {
                return IsArrayImpl() && !InternalIsMultiDimArray;
            }
        }

        public sealed override MemberTypes MemberType
        {
            get
            {
                if (IsPublic || IsNotPublic)
                    return MemberTypes.TypeInfo;
                else
                    return MemberTypes.NestedType;
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
            return this.GetArrayType();
        }

        public sealed override Type MakeArrayType(int rank)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeArrayType(this, rank);
#endif

            if (rank <= 0)
                throw new IndexOutOfRangeException();
            return this.GetMultiDimArrayType(rank);
        }

        public sealed override Type MakeByRefType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakeByRefType(this);
#endif
            return this.GetByRefType();
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
            return this.GetConstructedGenericType(typeArguments.ToRuntimeTypeInfoArray());
        }

        public sealed override Type MakePointerType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.TypeInfo_MakePointerType(this);
#endif

            return this.GetPointerType();
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

        public sealed override Type ReflectedType
        {
            get
            {
                // Desktop compat: For types, ReflectedType == DeclaringType. Nested types are always looked up as BindingFlags.DeclaredOnly was passed.
                // For non-nested types, the concept of a ReflectedType doesn't even make sense.
                return DeclaringType;
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
        // Returns true if it's possible to ask for a list of members and the base type without triggering a MissingMetadataException.
        //
        internal abstract bool CanBrowseWithoutMissingMetadataExceptions { get; }

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
                RuntimeTypeInfo baseType = null;
                if (!baseTypeDefRefOrSpec.IsNull)
                {
                    baseType = baseTypeDefRefOrSpec.Resolve(this.TypeContext);
                }
                return baseType;
            }
        }

        private string GetDefaultMemberName()
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
                        return memberName;
                    }
                }
            }

            return null;
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
                        Type enumType = CommonRuntimeTypes.Enum;
                        Type valueType = CommonRuntimeTypes.ValueType;

                        if (baseType.Equals(enumType))
                            classification |= TypeClassification.IsEnum | TypeClassification.IsValueType;
                        if (baseType.Equals(valueType) && !(this.Equals(enumType)))
                        {
                            classification |= TypeClassification.IsValueType;
                            foreach (Type primitiveType in ReflectionCoreExecution.ExecutionDomain.PrimitiveTypes)
                            {
                                if (this.Equals(primitiveType))
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

        private volatile TypeClassification _lazyClassification;

        private String _debugName;
    }
}

