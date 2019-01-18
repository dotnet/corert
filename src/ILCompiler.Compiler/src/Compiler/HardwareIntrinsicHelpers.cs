// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.IL;
using Internal.IL.Stubs;

namespace ILCompiler
{
    public static class HardwareIntrinsicHelpers
    {
        public static bool IsHardwareIntrinsic(MethodDesc method)
        {
            TypeDesc owningType = method.OwningType;

            if (owningType.IsIntrinsic && owningType is MetadataType mdType)
            {
                TargetArchitecture targetArch = owningType.Context.Target.Architecture;

                if (targetArch == TargetArchitecture.X64 || targetArch == TargetArchitecture.X86)
                {
                    mdType = (MetadataType)mdType.ContainingType ?? mdType;
                    if (mdType.Namespace == "System.Runtime.Intrinsics.X86")
                        return true;
                }
                else if (targetArch == TargetArchitecture.ARM64)
                {
                    if (mdType.Namespace == "System.Runtime.Intrinsics.Arm.Arm64")
                        return true;
                }
            }

            return false;
        }

        public static MethodIL GetUnsupportedImplementationIL(MethodDesc method)
        {
            if (method.Name == "get_IsSupported")
            {
                return new ILStubMethodIL(method,
                    new byte[] {
                        (byte)ILOpcode.ldc_i4_0,
                        (byte)ILOpcode.ret
                    },
                    Array.Empty<LocalVariableDefinition>(), null);
            }

            MethodDesc throwPnse = method.Context.GetHelperEntryPoint("ThrowHelpers", "ThrowPlatformNotSupportedException");

            return new ILStubMethodIL(method,
                    new byte[] {
                        (byte)ILOpcode.call, 1, 0, 0, 0,
                        (byte)ILOpcode.br_s, unchecked((byte)-7),
                    },
                    Array.Empty<LocalVariableDefinition>(),
                    new object[] { throwPnse });
        }
    }
}
