// Licensed to the .NET Foundation under one or more agreements.
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
    public partial class ImportThunk : AssemblyStubNode, ISymbolDefinitionNode
    {
        enum Kind
        {
            Eager,
            Lazy,
            DelayLoadHelper,
            VirtualStubDispatch,
        }

        private readonly ISymbolNode _helperCell;

        private readonly Import _instanceCell;

        private readonly ISymbolNode _moduleImport;

        private readonly Kind _thunkKind;

        public ImportThunk(ReadyToRunHelper helperId, ReadyToRunCodegenNodeFactory factory, Import instanceCell)
        {
            _helperCell = factory.GetReadyToRunHelperCell(helperId & ~ReadyToRunHelper.READYTORUN_HELPER_FLAG_VSD);
            _instanceCell = instanceCell;
            _moduleImport = factory.ModuleImport;

            if ((uint)(helperId & ReadyToRunHelper.READYTORUN_HELPER_FLAG_VSD) != 0)
            {
                _thunkKind = Kind.VirtualStubDispatch;
            }
            else if (helperId == ReadyToRunHelper.READYTORUN_HELPER_GetString)
            {
                _thunkKind = Kind.Lazy;
            }
            else if (helperId == ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall ||
                helperId == ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper ||
                helperId == ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj ||
                helperId == ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_ObjObj)
            {
                _thunkKind = Kind.DelayLoadHelper;
            }
            else
            {
                _thunkKind = Kind.Eager;
            }
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

    public class DelayLoadHelperThunk_Obj : ImportThunk
    {
        public DelayLoadHelperThunk_Obj(ReadyToRunCodegenNodeFactory nodeFactory, Import instanceCell)
            : base(ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj, nodeFactory, instanceCell)
        {
        }
    }
}
