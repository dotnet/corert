﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    interface IMethodCodeNode : IMethodNode, ISymbolDefinitionNode
    {
        void SetCode(byte[] data, Relocation[] relocs, DebugLocInfo[] debugLocInfos, DebugVarInfo[] debugVarInfos);
        void InitializeFrameInfos(FrameInfo[] frameInfos);
        void InitializeGCInfo(byte[] gcInfo);
        void InitializeEHInfo(ObjectNode.ObjectData ehInfo);
    }
}
