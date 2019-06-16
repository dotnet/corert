// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.IL;
using Internal.IL.Stubs;
using System.Diagnostics;

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

        public static bool IsIsSupportedMethod(MethodDesc method)
        {
            return method.Name == "get_IsSupported";
        }

        public static MethodIL GetUnsupportedImplementationIL(MethodDesc method)
        {
            if (IsIsSupportedMethod(method))
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

        public static MethodIL EmitIsSupportedIL(MethodDesc method, FieldDesc isSupportedField)
        {
            Debug.Assert(IsIsSupportedMethod(method));
            Debug.Assert(isSupportedField.IsStatic && isSupportedField.FieldType.IsWellKnownType(WellKnownType.Int32));

            TargetDetails target = method.Context.Target;
            MetadataType owningType = (MetadataType)method.OwningType;

            // Check for case of nested "X64" types on x86
            if (target.Architecture == TargetArchitecture.X86 && owningType.Name == "X64")
                return null;

            // Un-nest the type
            MetadataType containingType = (MetadataType)owningType.ContainingType;
            if (containingType != null)
                owningType = containingType;

            int flag;
            if ((target.Architecture == TargetArchitecture.X64 || target.Architecture == TargetArchitecture.X86)
                && owningType.Namespace == "System.Runtime.Intrinsics.X86")
            {
                switch (owningType.Name)
                {
                    case "Aes":
                        flag = XArchIntrinsicConstants.Aes;
                        break;
                    case "Pclmulqdq":
                        flag = XArchIntrinsicConstants.Pclmulqdq;
                        break;
                    case "Sse3":
                        flag = XArchIntrinsicConstants.Sse3;
                        break;
                    case "Ssse3":
                        flag = XArchIntrinsicConstants.Ssse3;
                        break;
                    case "Sse41":
                        flag = XArchIntrinsicConstants.Sse41;
                        break;
                    case "Sse42":
                        flag = XArchIntrinsicConstants.Sse42;
                        break;
                    case "Popcnt":
                        flag = XArchIntrinsicConstants.Popcnt;
                        break;
                    case "Lzcnt":
                        flag = XArchIntrinsicConstants.Lzcnt;
                        break;
                    default:
                        return null;
                }
            }
            else
            {
                return null;
            }

            var emit = new ILEmitter();
            ILCodeStream codeStream = emit.NewCodeStream();

            codeStream.Emit(ILOpcode.ldsfld, emit.NewToken(isSupportedField));
            codeStream.EmitLdc(flag);
            codeStream.Emit(ILOpcode.and);
            codeStream.EmitLdc(0);
            codeStream.Emit(ILOpcode.cgt_un);
            codeStream.Emit(ILOpcode.ret);

            return emit.Link(method);
        }

        private static class XArchIntrinsicConstants
        {
            public const int Aes = 0x0001;
            public const int Pclmulqdq = 0x0002;
            public const int Sse3 = 0x0004;
            public const int Ssse3 = 0x0008;
            public const int Sse41 = 0x0010;
            public const int Sse42 = 0x0020;
            public const int Popcnt = 0x0040;
            public const int Lzcnt = 0x0080;
        }
    }
}
