// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using Internal.Runtime.TypeLoader;

namespace Internal.Runtime.JitSupport
{
    class JitInterfaceDispatchCellNode : ExternObjectSymbolNode
    {
        public JitInterfaceDispatchCellNode(MethodDesc m)
        {
            Method = m;
        }

        public MethodDesc Method { get; }

        public override GenericDictionaryCell GetDictionaryCell()
        {
            ushort slot;

            if (!LazyVTableResolver.TryGetInterfaceSlotNumberFromMethod(Method, out slot))
            {
                Environment.FailFast("Unable to get interface slot number for method");
            }

            return GenericDictionaryCell.CreateInterfaceCallCell(Method.OwningType, slot);
        }
    }
}
