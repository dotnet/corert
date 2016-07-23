// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Collections.Generic;
using global::System.Reflection;
using global::Internal.Metadata.NativeFormat;
using global::System.Reflection.Runtime.General;

namespace Internal.Reflection.Core
{
    //
    // Implements the assembly binding policy Reflection domain. This gets called any time the domain needs 
    // to resolve an assembly name.
    //
    // If the binder cannot locate an assembly, it must return null and set "exception" to an exception object.
    //
    public abstract class AssemblyBinder
    {
        public abstract bool Bind(AssemblyName refName, out MetadataReader reader, out ScopeDefinitionHandle scopeDefinitionHandle, out IEnumerable<QScopeDefinition> overflowScopes, out Exception exception);

        // This helper is a concession to the fact that third-party binders running on top of the Win8P surface area have no sensible way
        // to perform this task due to the lack of a SetCulture() api on the AssemblyName class. Reflection.Core *is* able to do this 
        // thanks to the Internal.Reflection.Augment contract so we will expose this helper for the convenience of binders. 
        protected AssemblyName CreateAssemblyNameFromMetadata(MetadataReader reader, ScopeDefinitionHandle scopeDefinitionHandle)
        {
            return scopeDefinitionHandle.ToRuntimeAssemblyName(reader).ToAssemblyName();
        }
    }
}
