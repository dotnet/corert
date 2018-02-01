// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class CompilerComparer : TypeSystemComparer, IComparer<ISortableSymbolNode>
    {
        public int Compare(ISortableSymbolNode x, ISortableSymbolNode y)
        {
            if (x == y)
            {
                return 0;
            }

            int codeX = x.ClassCode;
            int codeY = y.ClassCode;
            if (codeX == codeY)
            {
                Debug.Assert(x.GetType() == y.GetType());

                int result = x.CompareToImpl(y, this);

                // We did a reference equality check above so an "Equal" result is not expected
                Debug.Assert(result != 0);

                return result;
            }
            else
            {
                Debug.Assert(x.GetType() != y.GetType());
                return codeX > codeY ? -1 : 1;
            }
        }
    }
}
