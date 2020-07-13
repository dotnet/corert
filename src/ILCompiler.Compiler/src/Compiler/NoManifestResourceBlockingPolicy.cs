// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// A blocking policy that doesn't block any manifest resources.
    /// </summary>
    public sealed class NoManifestResourceBlockingPolicy : ManifestResourceBlockingPolicy
    {
        public override bool IsManifestResourceBlocked(ModuleDesc module, string resourceName)
        {
            return false;
        }
    }
}
