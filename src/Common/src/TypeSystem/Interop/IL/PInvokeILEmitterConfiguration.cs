// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.IL
{
    public class PInvokeILEmitterConfiguration
    {
        private int _nativeMethodIdCounter = 0;
        public bool? ForceLazyResolution { get; private set; }

        public PInvokeILEmitterConfiguration(bool? forceLazyResolution)
        {
            ForceLazyResolution = forceLazyResolution;
        }

        /// <summary>
        /// Provides a unique numeric identifier to disambiguate PInvoke method names where there
        /// might otherwise be a naming conflict 
        /// </summary>
        public int GetNextNativeMethodId()
        {
            return System.Threading.Interlocked.Increment(ref _nativeMethodIdCounter);
        }
    }
}
