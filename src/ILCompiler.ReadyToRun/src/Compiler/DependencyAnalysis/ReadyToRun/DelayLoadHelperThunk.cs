﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// This node emits a thunk calling DelayLoad_Helper with a given instance signature
    /// to populate its indirection cell.
    /// </summary>
    public partial class DelayLoadHelperThunk : AssemblyStubNode, ISymbolDefinitionNode
    {
        private readonly ISymbolNode _helperCell;

        private readonly Import _instanceCell;

        private readonly ISymbolNode _moduleImport;

        private readonly bool _isVirtualStubDispatchCell;

        public DelayLoadHelperThunk(ReadyToRunHelper helperId, ReadyToRunCodegenNodeFactory factory, Import instanceCell)
        {
            _helperCell = factory.GetReadyToRunHelperCell(helperId & ~ReadyToRunHelper.READYTORUN_HELPER_FLAG_VSD);
            _instanceCell = instanceCell;
            _moduleImport = factory.ModuleImport;
            _isVirtualStubDispatchCell = (uint)(helperId & ~ReadyToRunHelper.READYTORUN_HELPER_FLAG_VSD) != 0;
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("DelayLoadHelper->");
            _instanceCell.AppendMangledName(nameMangler, sb);
        }

        protected override string GetName(NodeFactory factory)
        {
            Utf8StringBuilder sb = new Utf8StringBuilder();
            AppendMangledName(factory.NameMangler, sb);
            return sb.ToString();
        }

        public override int ClassCode => 433266948;
    }

    public class DelayLoadHelperThunk_Obj : DelayLoadHelperThunk
    {
        public DelayLoadHelperThunk_Obj(ReadyToRunCodegenNodeFactory nodeFactory, Import instanceCell)
            : base(ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj, nodeFactory, instanceCell)
        {
        }
    }
}
