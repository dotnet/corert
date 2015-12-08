// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem.Ecma
{
    public sealed partial class EcmaGenericParameter : TypeDesc
    {
        private EcmaModule _module;
        private GenericParameterHandle _handle;

        internal EcmaGenericParameter(EcmaModule module, GenericParameterHandle handle)
        {
            _module = module;
            _handle = handle;
        }

        public override int GetHashCode()
        {
            // TODO: Determine what a the right hash function should be. Use stable hashcode based on the type name?
            // For now, use the same hash as a SignatureVariable type.
            GenericParameter parameter = _module.MetadataReader.GetGenericParameter(_handle);
            if (parameter.Parent.Kind == HandleKind.MethodDefinition)
            {
                return parameter.Index * 0x7822381 + 0x54872645;
            }
            else
            {
                return parameter.Index * 0x5498341 + 0x832424;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _module.Context;
            }
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = 0;

            flags |= TypeFlags.ContainsGenericVariablesComputed | TypeFlags.ContainsGenericVariables;

            flags |= TypeFlags.GenericParameter;

            return flags;
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            GenericParameter parameter = _module.MetadataReader.GetGenericParameter(_handle);
            if (parameter.Parent.Kind == HandleKind.MethodDefinition)
            {
                return methodInstantiation[parameter.Index];
            }
            else
            {
                Debug.Assert(parameter.Parent.Kind == HandleKind.TypeDefinition);
                return typeInstantiation[parameter.Index];
            }
        }

#if CCIGLUE
        public TypeDesc DefiningType
        {
            get
            {
                var genericParameter = _module.MetadataReader.GetGenericParameter(_handle);
                return _module.GetObject(genericParameter.Parent) as TypeDesc;
            }
        }

        public MethodDesc DefiningMethod
        {
            get
            {
                var genericParameter = _module.MetadataReader.GetGenericParameter(_handle);
                return _module.GetObject(genericParameter.Parent) as MethodDesc;
            }
        }
#endif
    }
}
