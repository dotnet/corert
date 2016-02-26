// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Reflection.Extensibility;

#if ENABLE_REFLECTION_TRACE
using Internal.Reflection.Tracing;
#endif

namespace Internal.Reflection.Core.NonPortable
{
    //
    // Base type for all RuntimeType classes implemented by the runtime. The actual implementation class can live either in System.Private.CoreLib or
    // System.Reflection.Runtime.
    //
    // Mostly, this sets many of the "flavor-specific" properties to return the desktop-compatible "error case" result. 
    // This minimizes the number of methods the subclasses must override.
    //

    [DebuggerDisplay("{_debugName}")]
    public abstract class RuntimeType : ExtensibleType, IEquatable<RuntimeType>
    {
        protected RuntimeType()
            : base()
        {
        }

        //=====================================================================================================================
        // Equals/GetHashCode() - Important!
        //
        //    RuntimeType objects are currently unified and stored as weak references. Because of this, it's sufficient for Equals()
        //    to do an instance equality check, but GetHashCode() must return a hash code based on semantic identity. 
        //
        //    Ideally, we'd override Equals() and seal it but FxCop would likely complain
        //    if our derived types overrode GetHashCode() without overriding Equals(). Thus, all derived types that
        //    override GetHashCode() must override Equals() to keep FxCop happy but should implemented as:
        //
        //           public sealed override bool Equals(Object obj)
        //           {
        //               return InternalIsEqual(obj);
        //           }
        //
        //    so that the assumption of unification is encapsulated in one place.
        //
        //    We could also have sealed both overrides here and had our GetHashCode() do a call to a virtual "InternalGetHashCode".
        //    But that would impose a double virtual dispatch on the perf-critical GetHashCode() method.
        //=====================================================================================================================
        public abstract override bool Equals(Object obj);
        public abstract override int GetHashCode();

        public bool Equals(RuntimeType runtimeType)
        {
            Debug.Assert(!(this.InternalViolatesTypeIdentityRules || runtimeType.InternalViolatesTypeIdentityRules), "A shadow type escaped into the wild!");
            return Object.ReferenceEquals(this, runtimeType);
        }

        public sealed override String AssemblyQualifiedName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_AssemblyQualifiedName(this);
#endif

                String fullName = FullName;
                if (fullName == null)   // Some Types (such as generic parameters) return null for FullName by design.
                    return null;
                String assemblyName = InternalFullNameOfAssembly;
                return fullName + ", " + assemblyName;
            }
        }

        //
        // Left unsealed as nested and generic parameter types must override this.
        //
        public override Type DeclaringType
        {
            get { return null; }
        }

        //
        // Left unsealed as this is only correct for named types. Other type flavors must override this.
        //
        public override String FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_FullName(this);
#endif

                Debug.Assert(!IsConstructedGenericType);
                Debug.Assert(!IsGenericParameter);
                Debug.Assert(!HasElementType);

                String name = Name;

                Type declaringType = this.DeclaringType;
                if (declaringType != null)
                {
                    String declaringTypeFullName = declaringType.FullName;
                    return declaringTypeFullName + "+" + name;
                }

                String ns = Namespace;
                if (ns == null)
                    return name;
                return ns + "." + name;
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
                RuntimeType[] genericTypeArguments = this.InternalRuntimeGenericTypeArguments;
                if (genericTypeArguments.Length == 0)
                    return Array.Empty<Type>();
                Type[] result = new Type[genericTypeArguments.Length];
                for (int i = 0; i < genericTypeArguments.Length; i++)
                    result[i] = genericTypeArguments[i];
                return result;
            }
        }

        //
        // This group of predicates is left unsealed since most type flavors return true for at least one of them.
        //
        public override bool IsArray { get { return false; } }
        public override bool IsByRef { get { return false; } }
        public override bool IsConstructedGenericType { get { return false; } }
        public override bool IsGenericParameter { get { return false; } }
        public override bool IsPointer { get { return false; } }

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
        // Left unsealed as IsGenericType types must override this.
        //
        public override Type GetGenericTypeDefinition()
        {
            Debug.Assert(!IsConstructedGenericType);
            throw new InvalidOperationException(SR.InvalidOperation_NotGenericType);
        }

        public sealed override Type MakeArrayType()
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.Type_MakeArrayType(this);
#endif

            // Do not implement this as a call to MakeArrayType(1) - they are not interchangable. MakeArrayType() returns a
            // vector type ("SZArray") while MakeArrayType(1) returns a multidim array of rank 1. These are distinct types
            // in the ECMA model and in CLR Reflection.
            return ReflectionCoreNonPortable.GetArrayType(this);
        }

        public sealed override Type MakeArrayType(int rank)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.Type_MakeArrayType(this);
#endif

            if (rank <= 0)
                throw new IndexOutOfRangeException();
            return ReflectionCoreNonPortable.GetMultiDimArrayType(this, rank);
        }

        public sealed override Type MakeByRefType()
        {
            return ReflectionCoreNonPortable.GetByRefType(this);
        }

        public sealed override Type MakeGenericType(params Type[] instantiation)
        {
#if ENABLE_REFLECTION_TRACE
            if (ReflectionTrace.Enabled)
                ReflectionTrace.Type_MakeGenericType(this, instantiation);
#endif

            if (instantiation == null)
                throw new ArgumentNullException("instantiation");

            if (!(this.InternalIsGenericTypeDefinition))
                throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericTypeDefinition, this));

            // We intentionally don't validate the number of arguments or their suitability to the generic type's constraints.
            // In a pay-for-play world, this can cause needless MissingMetadataExceptions. There is no harm in creating
            // the Type object for an inconsistent generic type - no EEType will ever match it so any attempt to "invoke" it
            // will throw an exception.
            RuntimeType[] genericTypeArguments = new RuntimeType[instantiation.Length];
            for (int i = 0; i < instantiation.Length; i++)
            {
                genericTypeArguments[i] = instantiation[i] as RuntimeType;
                if (genericTypeArguments[i] == null)
                {
                    if (instantiation[i] == null)
                        throw new ArgumentNullException();
                    else
                        throw new PlatformNotSupportedException(SR.PlatformNotSupported_MakeGenericType); // "PlatformNotSupported" because on desktop, passing in a foreign type is allowed and creates a RefEmit.TypeBuilder
                }
            }
            return ReflectionCoreNonPortable.GetConstructedGenericType(this, genericTypeArguments);
        }

        public sealed override Type MakePointerType()
        {
            return ReflectionCoreNonPortable.GetPointerType(this);
        }

        public sealed override String Name
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_Name(this);
#endif

                RuntimeType rootCauseForFailure = null;
                String name = this.InternalGetNameIfAvailable(ref rootCauseForFailure);
                if (name == null)
                    throw RuntimeAugments.Callbacks.CreateMissingMetadataException(rootCauseForFailure);
                return name;
            }
        }

        //
        // Left unsealed so that invokable types can override. NOTE! If you override this, you must also override
        // InternalTryGetTypeHandle().
        //
        public override RuntimeTypeHandle TypeHandle
        {
            get
            {
                if (this.IsByRef)
                    throw new PlatformNotSupportedException(SR.PlatformNotSupported_NoTypeHandleForByRef);

                // If a constructed type doesn't have an type handle, it's either because the reducer tossed it (in which case,
                // we would thrown a MissingMetadataException when attempting to construct the type) or because one of
                // component types contains open type parameters. Since we eliminated the first case, it must be the second.
                // Throwing PlatformNotSupported since the desktop does, in fact, create type handles for open types.
                if (this.HasElementType || this.IsConstructedGenericType || this.IsGenericParameter)
                    throw new PlatformNotSupportedException(SR.PlatformNotSupported_NoTypeHandleForOpenTypes);

                // If got here, this is a "plain old type" that has metadata but no type handle. We can get here if the only
                // representation of the type is in the native metadata and there's no EEType at the runtime side.
                // If you squint hard, this is a missing metadata situation - the metadata is missing on the runtime side - and
                // the action for the user to take is the same: go mess with RD.XML.
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
            }
        }

        //
        // The default Type.ToString() assumes that it's safe to call Type.Name - which is not true in the world of optin metadata.
        // Block this here to make sure each subclass provides a safe implementation.
        //
        public abstract override String ToString();

        //
        // Return the full name of the "defining assembly" for the purpose of computing Type.AssemblyQualifiedName;
        //
        // (Note: I don't like this member being public but C# rules don't permit "protected", and I can't use "protected internal"
        //  since this is shared across two implementation assemblies.)
        //
        public abstract String InternalFullNameOfAssembly { get; }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        // NOTE: If you override this, you must also override TypeHandle.
        //
        public virtual bool InternalTryGetTypeHandle(out RuntimeTypeHandle typeHandle)
        {
            typeHandle = default(RuntimeTypeHandle);
            return false;
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        public virtual bool InternalIsGenericTypeDefinition
        {
            get
            {
                return false;
            }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        public virtual RuntimeType InternalRuntimeElementType
        {
            get
            {
                return null;
            }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        public virtual RuntimeType[] InternalRuntimeGenericTypeArguments
        {
            get
            {
                Debug.Assert(!IsConstructedGenericType);
                return Array.Empty<RuntimeType>();
            }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        // This helper stores the companion RuntimeTypeInfo on behalf of S.R.R. 
        //
        //  - The generic method variable RUNTIMETYPEINFO is *always* RuntimeTypeInfo. It's only generic because we can't directly mention that class
        //    from System.Private.CoreLib. You must never instantiate this method over any other type since there's only one underlying storage slot.
        //
        public RUNTIMETYPEINFO InternalGetLatchedRuntimeTypeInfo<RUNTIMETYPEINFO>(Func<RuntimeType, RUNTIMETYPEINFO> factory) where RUNTIMETYPEINFO : class
        {
            if (_lazyRuntimeTypeInfo == null)
            {
                // Note that it's possible for Type to not have a TypeInfo, in which case, _lazyRuntimeTypeInfo remains null 
                // and this "one-time" initialization will in fact run every time. The assumption is that it's better to throw
                // the null case under the bus in exchange for not introducing an extra indirection for the cases where
                // a TypeInfo actually exists.
                _lazyRuntimeTypeInfo = factory(this);
            }
            return RuntimeHelpers.UncheckedCast<RUNTIMETYPEINFO>(_lazyRuntimeTypeInfo);
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        // "Shadow" runtime types are created behind the scenes to provide metadata-level functionality for
        // types that have metadata but are represented by "EE" versions of the Type objects. These Type objects
        // necessary violate identity and must never escape into the wild. This method calls out those types
        // that checked builds can identify them.
        //
        public virtual bool InternalViolatesTypeIdentityRules
        {
            get
            {
                return false;
            }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        // This is the central implementation for all Equals(Object) overrides by RuntimeType subclasses. Ideally,
        // we'd just have sealed our own override of Equals() but this would cause FxCop violations on the subclasses for
        // overriding GetHashCode() without overriding Equals().
        //
        public bool InternalIsEqual(Object obj)
        {
            Debug.Assert(!(this.InternalViolatesTypeIdentityRules || (obj is RuntimeType && ((RuntimeType)obj).InternalViolatesTypeIdentityRules)), "A shadow type escaped into the wild!");
            return Object.ReferenceEquals(this, obj);
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        // Pay-for-play-safe implementation of Type.Name - returns null if name is unavailable due to lack of metadata.
        //
        public String InternalNameIfAvailable
        {
            get
            {
                RuntimeType ignore = null;
                return InternalGetNameIfAvailable(ref ignore);
            }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        // Pay-for-play-safe implementation of Type.Name - if name is unavailable due to lack of metadata and sets rootCauseForFailure
        // to be the type (or one of the types if multiple types were involved) that actually caused the name to be available.
        //
        public abstract String InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure);

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        public virtual bool InternalIsMultiDimArray
        {
            get
            {
                return false;
            }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public abstract bool InternalIsOpen { get; }

        //
        // Note: This can be (and is) called multiple times. We do not do this work in the constructor as calling ToString()
        // in the constructor causes some serious recursion issues.
        //
        public void EstablishDebugName()
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
                {
                    debugName = this.GetTraceString();  // If tracing on, call this.GetTraceString() which only gives you useful strings when metadata is available but doesn't pollute the ETW trace.
                }
                else
#endif
                {
                    debugName = this.ToString();
                }
                if (debugName == null)
                    debugName = "";
                _debugName = debugName;
            }
            return;
        }

        private volatile Object _lazyRuntimeTypeInfo;  // This is actually a RuntimeTypeInfo.

        private String _debugName;
    }
}


