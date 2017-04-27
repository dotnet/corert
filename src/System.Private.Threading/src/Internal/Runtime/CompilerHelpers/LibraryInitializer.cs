// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::Internal.Threading;
using global::Internal.Threading.Tasks.Tracing;
using global::Internal.Threading.Tracing;
using global::System.Runtime.CompilerServices;

namespace Internal.Runtime.CompilerHelpers
{
    public static class LibraryInitializer
    {
        /// <summary>
        /// Early library initialization code. StartupCodeInjectorTransform emits calls to these methods
        /// to startupCodeTrigger.InternalInitialize in the toolchain-generated assembly.
        /// BuildDriver.Config defines the ordered list of assemblies to initialize in the method
        /// GetLibraryInitializers().
        /// </summary>
        public static void InitializeLibrary()
        {
            TaskTrace.Initialize(new TaskTraceCallbacksImplementation());
            SpinLockTrace.Initialize(new SpinLockTraceCallbacksImplementation());
        }
    }
}