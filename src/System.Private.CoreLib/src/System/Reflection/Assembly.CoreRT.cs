// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration.Assemblies;
using System.Runtime.Serialization;

using Internal.Reflection.Augments;

namespace System.Reflection
{
    public abstract partial class Assembly : ICustomAttributeProvider, ISerializable
    {
        public static Assembly GetEntryAssembly() => Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetEntryAssembly();

        [System.Runtime.CompilerServices.Intrinsic]
        public static Assembly GetExecutingAssembly() { throw NotImplemented.ByDesign; } //Implemented by toolchain. 

        public static Assembly GetCallingAssembly() { throw new PlatformNotSupportedException(); }

        public static Assembly Load(AssemblyName assemblyRef) => ReflectionAugments.ReflectionCoreCallbacks.Load(assemblyRef);
        public static Assembly Load(byte[] rawAssembly, byte[] rawSymbolStore) => ReflectionAugments.ReflectionCoreCallbacks.Load(rawAssembly, rawSymbolStore);

        public static Assembly Load(string assemblyString)
        {
            if (assemblyString == null)
                throw new ArgumentNullException(nameof(assemblyString));

            AssemblyName name = new AssemblyName(assemblyString);
            return Load(name);
        }

        public static Assembly LoadFile(string path) { throw new PlatformNotSupportedException(); }
        public static Assembly LoadFrom(string assemblyFile) { throw new PlatformNotSupportedException(); }
        public static Assembly LoadFrom(string assemblyFile, byte[] hashValue, AssemblyHashAlgorithm hashAlgorithm) { throw new PlatformNotSupportedException(); }
    }
}
