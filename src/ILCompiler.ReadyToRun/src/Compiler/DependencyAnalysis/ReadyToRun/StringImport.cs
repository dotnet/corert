// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.JitInterface;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class StringImport : Import
    {
        private readonly ModuleToken _token;

        private int _definitionOffset;

        public StringImport(ImportSectionNode table, ModuleToken token)
            : base(table, new StringImportSignature(token))
        {
            _token = token;
        }

        public override int ClassCode => throw new NotImplementedException();

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            _definitionOffset = dataBuilder.CountBytes;
            // This needs to be an empty target pointer since it will be filled in with the string pointer
            // when loaded by CoreCLR
            dataBuilder.EmitZeroPointer();
        }

        int Offset => _definitionOffset;

        public override bool RepresentsIndirectionCell => true;

        protected override string GetName(NodeFactory context)
        {
            return "StringCell: " + _token.ToString();
        }
    }
}
