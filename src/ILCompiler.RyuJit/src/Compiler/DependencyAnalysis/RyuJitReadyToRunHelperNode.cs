// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class RyuJitReadyToRunHelperNode : ReadyToRunHelperNode, INodeWithDebugInfo
    {
        public RyuJitReadyToRunHelperNode(NodeFactory factory, ReadyToRunHelperId id, Object target) : base(factory, id, target) { }

        DebugLocInfo[] INodeWithDebugInfo.DebugLocInfos
        {
            get
            {
                if (_id == ReadyToRunHelperId.VirtualCall)
                {
                    // Generate debug information that lets debuggers step into the virtual calls.
                    // We generate a step into sequence point at the point where the helper jumps to
                    // the target of the virtual call.
                    TargetDetails target = ((MethodDesc)_target).Context.Target;
                    int debuggerStepInOffset = -1;
                    switch (target.Architecture)
                    {
                        case TargetArchitecture.X64:
                            debuggerStepInOffset = 3;
                            break;
                    }
                    if (debuggerStepInOffset != -1)
                    {
                        return new DebugLocInfo[]
                        {
                            new DebugLocInfo(0, String.Empty, WellKnownLineNumber.DebuggerStepThrough),
                            new DebugLocInfo(debuggerStepInOffset, String.Empty, WellKnownLineNumber.DebuggerStepIn)
                        };
                    }
                }

                return Array.Empty<DebugLocInfo>();
            }
        }

        DebugVarInfo[] INodeWithDebugInfo.DebugVarInfos
        {
            get
            {
                return Array.Empty<DebugVarInfo>();
            }
        }
    }
}
