// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Collections.Generic;

using global::Internal.Reflection.Extensibility;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.Modules
{
    //
    // The runtime's implementation of a Module.
    //
    // Modules are quite meaningless in ProjectN but we have to keep up the appearances since they still exist in Win8P's surface area.
    // As far as ProjectN is concerned, each Assembly has one module whose name is "<Unknown>".
    //
    internal sealed partial class RuntimeModule : ExtensibleModule
    {
        private RuntimeModule(RuntimeAssembly assembly)
            : base()
        {
            _assembly = assembly;
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return _assembly;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return Empty<CustomAttributeData>.Enumerable;
            }
        }

        public sealed override String FullyQualifiedName
        {
            get
            {
                return "<Unknown>";
            }
        }

        public sealed override String Name
        {
            get
            {
                return this.Assembly.GetName().Name;
            }
        }

        public sealed override bool Equals(Object o)
        {
            RuntimeModule other = o as RuntimeModule;
            if (other == null)
                return false;
            return this._assembly.Equals(other._assembly);
        }

        public sealed override int GetHashCode()
        {
            return _assembly.GetHashCode();
        }

        public sealed override Type GetType(String name, bool throwOnError, bool ignoreCase)
        {
            return _assembly.GetType(name, throwOnError, ignoreCase);
        }

        public sealed override String ToString()
        {
            return "<Unknown>";
        }

        private Assembly _assembly;
    }
}

