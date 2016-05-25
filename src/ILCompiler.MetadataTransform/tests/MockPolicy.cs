﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.Metadata;
using Internal.TypeSystem;

namespace MetadataTransformTests
{
    struct MockPolicy : IMetadataPolicy
    {
        private Func<MetadataType, bool> _typeGeneratesMetadata;
        private Func<MethodDesc, bool> _methodGeneratesMetadata;
        private Func<FieldDesc, bool> _fieldGeneratesMetadata;

        private Func<MetadataType, bool> _isBlockedType;
        private Func<MetadataType, ModuleDesc> _moduleOfType;

        public MockPolicy(
            Func<MetadataType, bool> typeGeneratesMetadata,
            Func<MethodDesc, bool> methodGeneratesMetadata = null,
            Func<FieldDesc, bool> fieldGeneratesMetadata = null,
            Func<MetadataType, bool> isBlockedType = null,
            Func<MetadataType, ModuleDesc> moduleOfType = null)
        {
            _typeGeneratesMetadata = typeGeneratesMetadata;
            _methodGeneratesMetadata = methodGeneratesMetadata;
            _fieldGeneratesMetadata = fieldGeneratesMetadata;
            _isBlockedType = isBlockedType;
            _moduleOfType = moduleOfType;
        }

        public bool GeneratesMetadata(MethodDesc methodDef)
        {
            if (_methodGeneratesMetadata != null)
                return _methodGeneratesMetadata(methodDef);
            return false;
        }

        public bool GeneratesMetadata(FieldDesc fieldDef)
        {
            if (_fieldGeneratesMetadata != null)
                return _fieldGeneratesMetadata(fieldDef);
            return false;
        }

        public bool GeneratesMetadata(MetadataType typeDef)
        {
            return _typeGeneratesMetadata(typeDef);
        }

        public bool IsBlocked(MetadataType typeDef)
        {
            if (_isBlockedType != null)
                return _isBlockedType(typeDef);
            return false;
        }

        public ModuleDesc GetModuleOfType(MetadataType typeDef)
        {
            if (_moduleOfType != null)
                return _moduleOfType(typeDef);
            return typeDef.Module;
        }
    }
}
