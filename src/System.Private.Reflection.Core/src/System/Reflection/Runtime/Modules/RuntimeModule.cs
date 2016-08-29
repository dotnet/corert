// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Reflection.Runtime.Assemblies;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.Modules
{
    //
    // The runtime's implementation of a Module.
    //
    // Modules are quite meaningless in ProjectN but we have to keep up the appearances since they still exist in Win8P's surface area.
    // As far as ProjectN is concerned, each Assembly has one module whose name is "<Unknown>".
    //
    internal sealed partial class RuntimeModule : Module
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
            return _assembly.Equals(other._assembly);
        }

        public sealed override int GetHashCode()
        {
            return _assembly.GetHashCode();
        }

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
            }
        }

        public sealed override Type GetType(String name, bool throwOnError, bool ignoreCase)
        {
            return _assembly.GetType(name, throwOnError, ignoreCase);
        }

        public sealed override Type[] GetTypes()
        {
            Debug.Assert(this.Equals(_assembly.ManifestModule)); // We only support single-module assemblies so we have to be the manifest module.
            return _assembly.GetTypes();
        }

        public sealed override Guid ModuleVersionId
        {
            get
            {
                throw new InvalidOperationException(SR.ModuleVersionIdNotSupported);
            }
        }

        public sealed override String ToString()
        {
            return "<Unknown>";
        }

        private readonly Assembly _assembly;
    }
}

