// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

namespace ILCompiler
{
    class EmptyMetadataManager : MetadataManager
    {
        public EmptyMetadataManager(CompilationModuleGroup group, CompilerTypeSystemContext typeSystemContext) : base(group, typeSystemContext)
        {
        }

        public override IEnumerable<ModuleDesc> GetCompilationModulesWithMetadata()
        {
            return Array.Empty<ModuleDesc>();
        }

        public override bool IsReflectionBlocked(MetadataType type)
        {
            return true;
        }

        protected override void ComputeMetadata(out byte[] metadataBlob, out List<MetadataMapping<MetadataType>> typeMappings, out List<MetadataMapping<MethodDesc>> methodMappings, out List<MetadataMapping<FieldDesc>> fieldMappings)
        {
            metadataBlob = Array.Empty<byte>();

            typeMappings = new List<MetadataMapping<MetadataType>>();
            methodMappings = new List<MetadataMapping<MethodDesc>>();
            fieldMappings = new List<MetadataMapping<FieldDesc>>();
        }

        /// <summary>
        /// Is there a reflection invoke stub for a method that is invokable?
        /// </summary>
        public override bool HasReflectionInvokeStubForInvokableMethod(MethodDesc method)
        {
            return false;
        }

        /// <summary>
        /// Gets a stub that can be used to reflection-invoke a method with a given signature.
        /// </summary>
        public override MethodDesc GetReflectionInvokeStub(MethodDesc method)
        {
            return null;
        }
    }
}
