// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using System.Reflection.Metadata;

using Internal.JitInterface;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// This helper structure encapsulates a module-qualified token.
    /// </summary>
    public struct ModuleToken
    {
        public readonly EcmaModule Module;
        public readonly mdToken Token;

        public ModuleToken(EcmaModule module, mdToken token)
        {
            Module = module;
            Token = token;
        }

        public override int GetHashCode()
        {
            return Module.GetHashCode() ^ unchecked((int)(31 * (uint)Token));
        }

        public override string ToString()
        {
            return Module.ToString() + ":" + ((uint)Token).ToString("X8");
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleToken moduleToken &&
                Module == moduleToken.Module &&
                Token == moduleToken.Token;
        }

        public int CompareTo(ModuleToken other)
        {
            int result = 0;
            // TODO: how to compare modules?
            // result = Module.CompareTo(other.Module);
            if (result == 0)
            {
                result = Token.CompareTo(other.Token);
            }
            return result;
        }

        public MetadataReader MetadataReader => Module.PEReader.GetMetadataReader();

        public CorTokenType TokenType => SignatureBuilder.TypeFromToken(Token);

        public uint TokenRid => SignatureBuilder.RidFromToken(Token);
    }
}
