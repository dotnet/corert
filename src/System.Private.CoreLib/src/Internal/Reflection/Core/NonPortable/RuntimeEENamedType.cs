// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Runtime.Augments;

#if ENABLE_REFLECTION_TRACE
using Internal.Reflection.Tracing;
#endif

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents types that:
    //
    //      - have a typedef token in the ECMA metadata model (that is, "Foo" or "Foo<>" but not "Foo<int>").
    //
    //  and - have an EEType backing it.
    //
    internal abstract class RuntimeEENamedType : RuntimeType
    {
        protected RuntimeEENamedType(RuntimeTypeHandle runtimeTypeHandle)
            : base()
        {
            _runtimeTypeHandle = runtimeTypeHandle;
        }

        public sealed override bool Equals(Object obj)
        {
            return InternalIsEqual(obj);  // Do not change this - see comments in RuntimeType.cs regarding Equals()
        }

        public sealed override int GetHashCode()
        {
            return _runtimeTypeHandle.GetHashCode();
        }

        public sealed override Type DeclaringType
        {
            get
            {
                RuntimeType shadowNamedType = ShadowNamedTypeIfAvailable;
                if (shadowNamedType != null)
                    return shadowNamedType.DeclaringType;
                if (RuntimeAugments.Callbacks.IsReflectionBlocked(_runtimeTypeHandle))
                    return null;
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
            }
        }

        public sealed override String FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_FullName(this);
#endif

                RuntimeType shadowNamedType = ShadowNamedTypeIfAvailable;
                if (shadowNamedType != null)
                    return shadowNamedType.FullName;
                if (RuntimeAugments.Callbacks.IsReflectionBlocked(_runtimeTypeHandle))
                    return BlockedRuntimeTypeNameGenerator.GetNameForBlockedRuntimeType(this);
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
            }
        }

        public sealed override String Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_Namespace(this);
#endif

                RuntimeType shadowNamedType = ShadowNamedTypeIfAvailable;
                if (shadowNamedType != null)
                    return shadowNamedType.Namespace;
                if (RuntimeAugments.Callbacks.IsReflectionBlocked(_runtimeTypeHandle))
                    return null;  // Reflection-blocked framework types report themselves as existing in the "root" namespace.
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
            }
        }

        public sealed override String InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure)
        {
            RuntimeType shadowNamedType = ShadowNamedTypeIfAvailable;
            if (shadowNamedType != null)
                return shadowNamedType.Name;
            if (RuntimeAugments.Callbacks.IsReflectionBlocked(_runtimeTypeHandle))
                return BlockedRuntimeTypeNameGenerator.GetNameForBlockedRuntimeType(this);

            rootCauseForFailure = this;
            return null;
        }

        public sealed override String InternalFullNameOfAssembly
        {
            get
            {
                RuntimeType shadowNamedType = ShadowNamedTypeIfAvailable;
                if (shadowNamedType != null)
                    return shadowNamedType.InternalFullNameOfAssembly;
                if (RuntimeAugments.Callbacks.IsReflectionBlocked(_runtimeTypeHandle))
                    return BlockedRuntimeTypeNameGenerator.GetNameForBlockedRuntimeType(this);
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
            }
        }

        public sealed override RuntimeTypeHandle TypeHandle
        {
            get
            {
                return _runtimeTypeHandle;
            }
        }

        public sealed override bool InternalTryGetTypeHandle(out RuntimeTypeHandle typeHandle)
        {
            typeHandle = _runtimeTypeHandle;
            return true;
        }


        public sealed override String ToString()
        {
            RuntimeType shadowNamedType = ShadowNamedTypeIfAvailable;
            if (shadowNamedType != null)
                return shadowNamedType.ToString();

            return _runtimeTypeHandle.LastResortToString;
        }

        //
        // If metadata is available for this type, this property returns a RuntimeInspectionNamedType that we can
        // use to get information we can't get from the EEType. Otherwise, return null.
        //
        // ! Shadow types break type identity and must never be handed out to the caller!
        //
        private RuntimeType ShadowNamedTypeIfAvailable
        {
            get
            {
                if (_lazyShadowNamedType == null)
                {
                    ReflectionExecutionDomainCallbacks callbacks = RuntimeAugments.CallbacksIfAvailable;
                    if (callbacks != null)
                        _lazyShadowNamedType = (RuntimeType)(callbacks.CreateShadowRuntimeInspectionOnlyNamedTypeIfAvailable(_runtimeTypeHandle));
                }
                return _lazyShadowNamedType;
            }
        }

        //
        // If metadata is available for this type, this property returns a RuntimeInspectionNamedType that we can
        // use to get information we can't get from the EEType. Otherwise, throw a MissingMetadataException.
        //
        // ! Shadow types break type identity and must never be handed out to the caller!
        //
        private RuntimeType ShadowNamedType
        {
            get
            {
                RuntimeType shadowNamedType = ShadowNamedTypeIfAvailable;
                if (shadowNamedType == null)
                    throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
                return shadowNamedType;
            }
        }

        private volatile RuntimeType _lazyShadowNamedType;
        private RuntimeTypeHandle _runtimeTypeHandle;
    }
}

