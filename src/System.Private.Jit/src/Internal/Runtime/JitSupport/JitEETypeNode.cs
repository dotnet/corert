﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using Internal.Runtime.TypeLoader;

namespace Internal.Runtime.JitSupport
{
    class JitEETypeNode : ExternObjectSymbolNode
    {
        public JitEETypeNode(TypeDesc type)
        {
            Type = type;
        }

        public TypeDesc Type { get; }

        public override GenericDictionaryCell GetDictionaryCell()
        {
            return GenericDictionaryCell.CreateTypeHandleCell(Type);
        }
    }
}
