// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// This class represents a single indirection cell used to call delay load helpers.
    /// In addition to PrecodeHelperImport instances of this import type emit GC ref map
    /// entries into the R2R executable.
    /// </summary>
    public class DelayLoadHelperMethodImport : DelayLoadHelperImport, IMethodNode
    {
        private readonly MethodFixupSignature _signature;

        private readonly ReadyToRunHelper _helper;

        private readonly ImportThunk _delayLoadHelper;

        public DelayLoadHelperMethodImport(
            ReadyToRunCodegenNodeFactory factory, 
            ImportSectionNode importSectionNode, 
            ReadyToRunHelper helper, 
            MethodFixupSignature methodSignature, 
            string callSite = null)
            : base(factory, importSectionNode, helper, methodSignature, callSite)
        {
            _helper = helper;
            _signature = methodSignature;
            _delayLoadHelper = new ImportThunk(helper, factory, this);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("DelayLoadHelperMethodImport(");
            sb.Append(_helper.ToString());
            sb.Append(") -> ");
            ImportSignature.AppendMangledName(nameMangler, sb);
            if (CallSite != null)
            {
                sb.Append(" @ ");
                sb.Append(CallSite);
            }
        }

        public override int ClassCode => 192837465;

        public MethodDesc Method => _signature.Method;
    }
}
