// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;

namespace System.Reflection
{
    public sealed partial class AssemblyName : ICloneable, IDeserializationCallback, ISerializable
    {
        public void nInit()
        {
            RuntimeAssemblyName runtimeAssemblyName = AssemblyNameParser.Parse(_name);
            runtimeAssemblyName.CopyToAssemblyName(this);
        }

        private byte[] nGetPublicKeyToken()
        {
            return AssemblyNameHelpers.ComputePublicKeyToken(_publicKey);
        }

        public static AssemblyName nGetFileInformation(string assemblyFile)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported_AssemblyName_GetAssemblyName);
        }
    }
}
