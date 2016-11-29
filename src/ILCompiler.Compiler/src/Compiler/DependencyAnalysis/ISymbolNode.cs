// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    public interface ISymbolNode
    {
        void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb);
        int Offset { get; }
    }

    static public class ISymbolNodeExtensions
    {
        [ThreadStatic]
        static Utf8StringBuilder s_cachedUtf8StringBuilder;

        static public string GetMangledName(this ISymbolNode symbolNode)
        {
            Utf8StringBuilder sb = s_cachedUtf8StringBuilder;
            if (sb == null)
                sb = new Utf8StringBuilder();

            symbolNode.AppendMangledName(NodeFactory.NameMangler, sb);
            string ret = sb.ToString();

            sb.Clear();
            s_cachedUtf8StringBuilder = sb;

            return ret;
        }
    }
}
