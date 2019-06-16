// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for System.Runtime.Intrinsics intrinsics.
    /// </summary>
    internal static class HardwareIntrinsics
    {
        public static MethodIL EmitIL(MethodDesc method)
        {
            // We only provide special IL for the IsSupported intrinsic
            if (method.Name != "get_IsSupported")
                return null;

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
                    case "Bmi1":
                        flag = XArchIntrinsicConstants.Bmi1;
                        break;
                    case "Bmi2":
                        flag = XArchIntrinsicConstants.Bmi2;
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

            FieldDesc supportedField = ((ILCompiler.CompilerTypeSystemContext)method.Context).HardwareIntrinsicsSupportFlagsField;
            codeStream.Emit(ILOpcode.ldsfld, emit.NewToken(supportedField));
            codeStream.EmitLdc(flag);
            codeStream.Emit(ILOpcode.or);
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
            public const int Bmi1 = 0x0080;
            public const int Bmi2 = 0x0100;
            public const int Lzcnt = 0x0200;
        }
    }
}
