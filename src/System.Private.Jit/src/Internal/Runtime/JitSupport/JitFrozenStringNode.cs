// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using Internal.Runtime.TypeLoader;

namespace Internal.Runtime.JitSupport
{
    public class JitFrozenStringNode : ExternObjectSymbolNode
    {
        public JitFrozenStringNode(string stringToBeFrozen)
        {
            FrozenString = stringToBeFrozen;
        }

        public string FrozenString { get; }

        public override GenericDictionaryCell GetDictionaryCell()
        {
            return GenericDictionaryCell.CreateIntPtrCell(FrozenStrings.GetRawPointer(FrozenString));
        }
    }
}
