// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//Internal.Reflection.Core
//-------------------------------------------------
//  Why does this exist?:
//   This contract exposes the service
//   of consuming metadata (through the S.R.M api) and creating
//   an Win8P-style "browse-only" Reflection object tree on top of it.
//
//  The contract allows the creation of multiple "reflection domains" with
//  custom assembly binding policies.
//
//
//  Implemented by:
//      Reflection.Core.dll on RH and desktop.
//
//  Consumed by:
//      LMR on RH and desktop.
//      "Classic reflection" on RH.
//
//

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;

namespace Internal.Reflection.Core
{
    //=====================================================================================================================================
    // A Reflection domain is an independent "universe" of loaded reflection types. Reflection entities cannot exist in multiple domains,
    // nor at they allowed to intermix.
    //
    // Reflection domains are "browse-only" - attempts to invoke, instantiate or set or get fields and properties all fail.
    //
    // Each reflection domain determines its own rules for resolving assemblies and the identity of the "core" assembly (typically
    // mscorlib.dll.)
    //=====================================================================================================================================
    public class ReflectionDomain
    {
        public ReflectionDomain(ReflectionDomainSetup setup)
        {
            throw new PlatformNotSupportedException();
        }

        public Assembly LoadAssembly(AssemblyName refName)
        {
            throw new PlatformNotSupportedException();
        }

        public Exception CreateMissingMetadataException(Type pertainant)
        {
            return this.ReflectionDomainSetup.CreateMissingMetadataException(pertainant);
        }

        public Exception CreateMissingMetadataException(TypeInfo pertainant)
        {
            return this.ReflectionDomainSetup.CreateMissingMetadataException(pertainant);
        }

        public Exception CreateMissingMetadataException(TypeInfo pertainant, string nestedTypeName)
        {
            return this.ReflectionDomainSetup.CreateMissingMetadataException(pertainant, nestedTypeName);
        }

        public Exception CreateNonInvokabilityException(MemberInfo pertainant)
        {
            return this.ReflectionDomainSetup.CreateNonInvokabilityException(pertainant);
        }


        // This private constructor exists as a temporary measure so that we can create Execution domains without enabling the
        // general ReflectionDomain case.
        internal ReflectionDomain(ReflectionDomainSetup setup, int meaningless)
        {
            this.ReflectionDomainSetup = setup;
        }

        internal ReflectionDomainSetup ReflectionDomainSetup { get; private set; }

        internal FoundationTypes FoundationTypes
        {
            get
            {
                return this.ReflectionDomainSetup.FoundationTypes;
            }
        }

        internal IEnumerable<Type> PrimitiveTypes
        {
            get
            {
                FoundationTypes foundationTypes = this.FoundationTypes;
                return new Type[]
                {
                    foundationTypes.SystemBoolean,
                    foundationTypes.SystemChar,
                    foundationTypes.SystemSByte,
                    foundationTypes.SystemByte,
                    foundationTypes.SystemInt16,
                    foundationTypes.SystemUInt16,
                    foundationTypes.SystemInt32,
                    foundationTypes.SystemUInt32,
                    foundationTypes.SystemInt64,
                    foundationTypes.SystemUInt64,
                    foundationTypes.SystemSingle,
                    foundationTypes.SystemDouble,
                    foundationTypes.SystemIntPtr,
                    foundationTypes.SystemUIntPtr,
                };
            }
        }
    }
}
