// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public abstract class Import : EmbeddedObjectNode
    {
        public abstract Signature GetSignature(NodeFactory factory);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            var signature = GetSignature(factory);
            if (signature != null)
                yield return new DependencyListEntry(signature, "Signature for ready-to-run fixup import");
        }
    }

    public class ModuleImport : Import
    {
        private readonly ReadyToRunHelperSignature _signature;

        public ModuleImport()
        {
            _signature = new ReadyToRunHelperSignature(ReadyToRunHelper.READYTORUN_HELPER_Module);
        }

        public override bool StaticDependenciesAreComputed => true;

        protected override int ClassCode => throw new NotImplementedException();

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            // This needs to be an empty target pointer since it will be filled in with Module*
            // when loaded by CoreCLR
            dataBuilder.EmitZeroPointer();
        }

        public override Signature GetSignature(NodeFactory factory) => _signature;
        
        protected override string GetName(NodeFactory context)
        {
            return "ModuleImport";
        }
    }
}
