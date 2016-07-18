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
    // TODO https://github.com/dotnet/corefx/issues/9805:
    //
    //   With Type and TypeInfo being merged back into the same object, RuntimeType is being phased out in favor of RuntimeTypeInfo.
    //   This phaseout will happen slowly since RuntimeType is a widely used exchange type inside Corelib and Reflection.Core.
    //
    // TODO https://github.com/dotnet/corefx/issues/9805:
    //
    //   Most of these methods should made abstract or removed, but this requires coordination with the contracts and apicompat.
    //   Since this is now a relic class, we might not bother with that.
    //

    [DebuggerDisplay("{_debugName}")]
    public abstract class RuntimeType : ExtensibleType, IEquatable<RuntimeType>, ICloneable
    {
        protected RuntimeType()
            : base()
        {
        }

        public object Clone()
        {
            return this;
        }

        public abstract override bool Equals(Object obj);
        public abstract override int GetHashCode();

        public bool Equals(RuntimeType runtimeType)
        {
            Debug.Assert(!(this.InternalViolatesTypeIdentityRules || runtimeType.InternalViolatesTypeIdentityRules), "A shadow type escaped into the wild!");
            return Object.ReferenceEquals(this, runtimeType);
        }

        public override String AssemblyQualifiedName
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override Type DeclaringType
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override String FullName
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override int GenericParameterPosition
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override Type[] GenericTypeArguments
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override bool IsArray
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override bool IsByRef
        {
            get { throw NotImplemented.ByDesign; }
        }
        public override bool IsConstructedGenericType
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override bool IsGenericParameter
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override bool IsPointer
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override int GetArrayRank()
        {
            throw NotImplemented.ByDesign;
        }

        public override Type GetElementType()
        {
            throw NotImplemented.ByDesign;
        }

        public override Type GetGenericTypeDefinition()
        {
            throw NotImplemented.ByDesign;
        }

        public override Type MakeArrayType()
        {
            throw NotImplemented.ByDesign;
        }

        public override Type MakeArrayType(int rank)
        {
            throw NotImplemented.ByDesign;
        }

        public override Type MakeByRefType()
        {
            throw NotImplemented.ByDesign;
        }

        public override Type MakeGenericType(params Type[] instantiation)
        {
            throw NotImplemented.ByDesign;
        }

        public override Type MakePointerType()
        {
            throw NotImplemented.ByDesign;
        }

        public override String Name
        {
            get { throw NotImplemented.ByDesign; }
        }

        public override RuntimeTypeHandle TypeHandle
        {
            get { throw NotImplemented.ByDesign; }
        }

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
            throw NotImplemented.ByDesign;
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        public virtual bool InternalIsGenericTypeDefinition
        {
            get { throw NotImplemented.ByDesign; }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        public virtual RuntimeType InternalRuntimeElementType
        {
            get { throw NotImplemented.ByDesign; }
        }

        //
        // (Note: I don't like this member being public but I can't use "internal" since this is shared across two implementation assemblies.)
        //
        public virtual RuntimeType[] InternalRuntimeGenericTypeArguments
        {
            get { throw NotImplemented.ByDesign; }
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
            throw NotImplemented.ByDesign;
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
            get { throw NotImplemented.ByDesign; }
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
            throw NotImplemented.ByDesign;
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
            get { throw NotImplemented.ByDesign; }
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

        private String _debugName;
    }
}


