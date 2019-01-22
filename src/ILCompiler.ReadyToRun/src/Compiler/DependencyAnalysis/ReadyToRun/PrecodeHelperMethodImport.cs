// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// This class represents a single method indirection cell resolved using fixup table
    /// at function startup; in addition to PrecodeHelperImport instances of this import type
    /// emit GC ref map entries into the R2R executable.
    /// </summary>
    public class PrecodeHelperMethodImport : PrecodeHelperImport, IMethodNode
    {
        private readonly MethodDesc _methodDesc;

        public PrecodeHelperMethodImport(ReadyToRunCodegenNodeFactory factory, MethodDesc methodDesc, Signature signature)
            : base(factory, signature)
        {
            _methodDesc = methodDesc;
        }

        protected override string GetName(NodeFactory factory)
        {
            return "PrecodeHelperMethodImport->" + ImportSignature.GetMangledName(factory.NameMangler);
        }

        public override int ClassCode => 668765432;

        public MethodDesc Method => _methodDesc;
    }
}
