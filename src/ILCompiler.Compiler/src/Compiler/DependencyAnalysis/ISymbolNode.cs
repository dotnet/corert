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
        /// <summary>
        /// Set the return value of this property to true to indicate that this symbol
        /// is an indirection cell to data that is needed, not the actual data itself.
        /// Most commonly affects the code generation which accesses symbols such
        /// Types which may require an indirection to access or not.
        /// </summary>
        bool RepresentsIndirectionCell { get; }
    }

    public static class ISymbolNodeExtensions
    {
        [ThreadStatic]
        static Utf8StringBuilder s_cachedUtf8StringBuilder;

        public static string GetMangledName(this ISymbolNode symbolNode)
        {
            Utf8StringBuilder sb = s_cachedUtf8StringBuilder;
            if (sb == null)
                sb = new Utf8StringBuilder();

            symbolNode.AppendMangledName(NodeFactory.NameManglerDoNotUse, sb);
            string ret = sb.ToString();

            sb.Clear();
            s_cachedUtf8StringBuilder = sb;

            return ret;
        }
    }
}
