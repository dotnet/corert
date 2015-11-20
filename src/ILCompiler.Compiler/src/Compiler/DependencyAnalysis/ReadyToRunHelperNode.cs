// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public partial class ReadyToRunHelperNode : AssemblyStubNode
    {
        private ReadyToRunHelper _helper;

        public ReadyToRunHelperNode(ReadyToRunHelper helper)
        {
            _helper = helper;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public ReadyToRunHelper Helper
        {
            get
            {
                return _helper;
            }
        }

        public override string MangledName
        {
            get
            {
                return _helper.MangledName;
            }
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory context)
        {
            if (Helper.Id == ReadyToRunHelperId.VirtualCall)
            {
                DependencyList dependencyList = new DependencyList();
                dependencyList.Add(context.VirtualMethodUse((MethodDesc)Helper.Target), "ReadyToRun Virtual Method Call");
                return dependencyList;
            }
            else
            {
                return null;
            }
        }
    }
}
