// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Metadata.NativeFormat;
using System.Reflection.Runtime.Assemblies.NativeFormat;

namespace System.Reflection.Runtime.Modules.NativeFormat
{
    internal sealed partial class NativeFormatRuntimeModule : RuntimeModule
    {
        private NativeFormatRuntimeModule(NativeFormatRuntimeAssembly assembly)
            : base()
        {
            _assembly = assembly;
        }

        public sealed override Assembly Assembly => _assembly;

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                QScopeDefinition scope = _assembly.Scope;
                return RuntimeCustomAttributeData.GetCustomAttributes(scope.Reader, scope.ScopeDefinition.ModuleCustomAttributes);
            }
        }

        public sealed override string Name
        {
            get
            {
                QScopeDefinition scope = _assembly.Scope;
                MetadataReader reader = scope.Reader;
                string name = scope.ScopeDefinition.ModuleName.GetConstantStringValue(reader).Value;
                if (name == null)
                    return _assembly.GetName().Name + ".dll"; // Workaround for TFS 441076 - Module data not emitted for facade assemblies.
                return name;
            }
        }

        public sealed override int MetadataToken
        {
            get
            {
                throw new InvalidOperationException(SR.NoMetadataTokenAvailable);
            }
        }

        public sealed override Guid ModuleVersionId
        {
            get
            {
                byte[] mvid = _assembly.Scope.ScopeDefinition.Mvid.ToArray();
                if (mvid.Length == 0)
                    return default(Guid); // Workaround for TFS 441076 - Module data not emitted for facade assemblies.
                return new Guid(mvid);
            }
        }

        private readonly NativeFormatRuntimeAssembly _assembly;
    }
}
