// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using System.Reflection.Runtime.Assemblies.EcmaFormat;

namespace System.Reflection.Runtime.Modules.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeModule : RuntimeModule
    {
        private EcmaFormatRuntimeModule(EcmaFormatRuntimeAssembly assembly)
            : base()
        {
            _assembly = assembly;
        }

        public sealed override Assembly Assembly => _assembly;

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return Empty<CustomAttributeData>.Enumerable;
            }
        }

        public sealed override string Name
        {
            get
            {
                return _assembly.GetName().Name;
            }
        }

        public sealed override int MetadataToken
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public sealed override Guid ModuleVersionId
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        private readonly EcmaFormatRuntimeAssembly _assembly;
    }
}
