// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ILCompiler.DependencyAnalysis.ARM64;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    partial class ReadyToRunGenericHelperNode
    {
        protected Register GetContextRegister(ref /* readonly */ ARM64Emitter encoder)
        {
            throw new NotImplementedException();
        }

        protected void EmitDictionaryLookup(NodeFactory factory, ref ARM64Emitter encoder, Register context, Register result, GenericLookupResult lookup, bool relocsOnly)
        {
            throw new NotImplementedException();
        }

        protected sealed override void EmitCode(NodeFactory factory, ref ARM64Emitter encoder, bool relocsOnly)
        {
            throw new NotImplementedException();
        }

        protected virtual void EmitLoadGenericContext(NodeFactory factory, ref ARM64Emitter encoder, bool relocsOnly)
        {
            throw new NotImplementedException();
        }
    }

    partial class ReadyToRunGenericLookupFromTypeNode
    {
        protected override void EmitLoadGenericContext(NodeFactory factory, ref ARM64Emitter encoder, bool relocsOnly)
        {
            throw new NotImplementedException();
        }
    }
}
