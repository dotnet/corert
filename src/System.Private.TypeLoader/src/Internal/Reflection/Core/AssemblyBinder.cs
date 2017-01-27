// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Reflection.Runtime.General;
using Internal.Runtime.TypeLoader;
using System.Runtime.InteropServices;

namespace Internal.Reflection.Core
{
    // Auto StructLayout used to suppress warning that order of fields is not guaranteed in partial structs
    [StructLayout(LayoutKind.Auto)]
    public partial struct AssemblyBindResult
    {
        public MetadataReader Reader;
        public ScopeDefinitionHandle ScopeDefinitionHandle;
        public IEnumerable<QScopeDefinition> OverflowScopes;
    }

    //
    // Implements the assembly binding policy Reflection domain. This gets called any time the domain needs 
    // to resolve an assembly name.
    //
    // If the binder cannot locate an assembly, it must return null and set "exception" to an exception object.
    //
    public abstract class AssemblyBinder
    {
        public const String DefaultAssemblyNameForGetType = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

        public abstract bool Bind(AssemblyName refName, out AssemblyBindResult result, out Exception exception);

        public abstract bool Bind(byte[] rawAssembly, byte[] rawSymbolStore, out AssemblyBindResult result, out Exception exception);

        // This helper is a concession to the fact that third-party binders running on top of the Win8P surface area have no sensible way
        // to perform this task due to the lack of a SetCulture() api on the AssemblyName class. Reflection.Core *is* able to do this 
        // thanks to the Internal.Reflection.Augment contract so we will expose this helper for the convenience of binders. 
        protected AssemblyName CreateAssemblyNameFromMetadata(MetadataReader reader, ScopeDefinitionHandle scopeDefinitionHandle)
        {
            return scopeDefinitionHandle.ToRuntimeAssemblyName(reader).ToAssemblyName();
        }
    }
}
