// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration.Assemblies;
using System.Runtime.Serialization;

using Internal.Reflection.Augments;
using Internal.Reflection.Core.NonPortable;

namespace System.Reflection
{
    public abstract partial class Assembly : ICustomAttributeProvider, ISerializable
    {
        public static Assembly GetEntryAssembly() => Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetEntryAssembly();

        [System.Runtime.CompilerServices.Intrinsic]
        public static Assembly GetExecutingAssembly() { throw NotImplemented.ByDesign; } //Implemented by toolchain. 

        public static Assembly GetCallingAssembly() { throw new PlatformNotSupportedException(); }

        public static Assembly Load(AssemblyName assemblyRef) => ReflectionAugments.ReflectionCoreCallbacks.Load(assemblyRef, throwOnFileNotFound: true);

        public static Assembly Load(string assemblyString)
        {
            if (assemblyString == null)
                throw new ArgumentNullException(nameof(assemblyString));

            AssemblyName name = new AssemblyName(assemblyString);
            return Load(name);
        }

        public bool IsRuntimeImplemented() => this is IRuntimeImplemented; // Not an api but needs to be public because of Reflection.Core/CoreLib divide.
    }
}
