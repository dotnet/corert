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
using global::System.Reflection.Runtime.General;

namespace Internal.Reflection.Core
{
    //=====================================================================================================================================
    // This object encapsulates the customization parameters for Reflection domains.
    //=====================================================================================================================================
    public abstract class ReflectionDomainSetup
    {
        protected ReflectionDomainSetup() { }
        public abstract AssemblyBinder AssemblyBinder { get; }
        public abstract FoundationTypes FoundationTypes { get; }
        public abstract Exception CreateMissingMetadataException(TypeInfo pertainant);
        public abstract Exception CreateMissingMetadataException(Type pertainant);
        public abstract Exception CreateMissingMetadataException(TypeInfo pertainant, string nestedTypeName);
        public abstract Exception CreateNonInvokabilityException(MemberInfo pertainant);
    }
}
