// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Represents a manifest resource blocking policy. The policy dictates whether manifest resources should
    /// be generated into the executable.
    /// </summary>
    public abstract class ManifestResourceBlockingPolicy
    {
        /// <summary>
        /// Returns true if manifest resource with name '<paramref name="resourceName"/>' in module '<paramref name="module"/>'
        /// is reflection blocked.
        /// </summary>
        public abstract bool IsManifestResourceBlocked(ModuleDesc module, string resourceName);
    }

    public class FullyBlockedManifestResourceBlockingPolicy : ManifestResourceBlockingPolicy
    {
        public override bool IsManifestResourceBlocked(ModuleDesc module, string resourceName)
        {
            return true;
        }
    }
}
