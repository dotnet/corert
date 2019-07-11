// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.IL;
using Internal.IL.Stubs;
using System.Diagnostics;
using Internal.Metadata.NativeFormat;
using System.Collections.Generic;

namespace ILCompiler
{
    public static class HardwareIntrinsicHelpers
    {
        /// <summary>
        /// Gets a value indicating whether this type is an intrinsic from the namespace System.Runtime.Intrinsics
        /// requiring special treatment per
        /// 
        /// https://github.com/dotnet/coreclr/blob/11137fbe46f524dfd6c2f7bb2a77035aa225524c/src/vm/methodtablebuilder.cpp#L9566
        /// 
        /// Disable AOT compiling for the SIMD hardware intrinsic types. These types require special
        /// ABI handling as they represent fundamental data types (__m64, __m128, and __m256) and not
        /// aggregate or union types. See https://github.com/dotnet/coreclr/issues/15943
        ///
        /// Once they are properly handled according to the ABI requirements, we can remove this check
        /// and allow them to be used in crossgen/AOT scenarios.
        ///
        /// We can allow these to AOT compile in CoreLib since CoreLib versions with the runtime.
        /// </summary>
        /// <param name="type">Type to check</param>
        public static bool IsHardwareIntrinsic(TypeDesc type)
        {
            return type.IsIntrinsic &&
                type is MetadataType mdType &&
                mdType.Module == type.Context.SystemModule &&
                (mdType.ContainingType ?? mdType).Namespace == "System.Runtime.Intrinsics";
        }

        /// <summary>
        /// Gets a value indicating whether this is a hardware intrinsic on the platform that we're compiling for.
        /// </summary>
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

        /// <summary>
        /// Cache used to speed up resolution of types referring to HW-dependent types.
        /// Please note this is not "just a cache", we also use it to break circles in the
        /// recursive type graph (e.g. int implements several generic interfaces instantiated over int).
        /// </summary>
        private static Dictionary<TypeDesc, bool> _hardwareIntrinsicDependentTypeCache = new Dictionary<TypeDesc, bool>();

        /// <summary>
        /// Gets a value indicating whether a given type doesn't refer to any HW intrinsics and can be safely
        /// pre-JITted. This requires a recursive scan roughly mimicking the CoreCLR MethodTableBuilder behavior;
        /// in particular, we check the following aspects:
        /// (*) The type itself;
        /// (*) For DefTypes, we recursively check all fields on the type;
        /// (*) For generic types, we recursively check all instantiation parameters;
        /// (*) For parameterized types, we recursively check the parameter type.
        /// </summary>
        /// <param name="type">Type to analyze</param>
        /// <returns>True when a given type is the owner type for a hardware intrinsic</returns>
        public static bool IsHardwareIntrinsicDependentType(TypeDesc type)
        {
            if (_hardwareIntrinsicDependentTypeCache.TryGetValue(type, out bool result))
            {
                return result;
            }

            // Initially mark the type in the cache as HW-independent so that, if we hit it
            // in the recursion scan, we don't cycle indefinitely. If such type gets identified
            // as HW-dependent during the scan, we'll overwrite the cache once the scan has finished.
            // _hardwareIntrinsicDependentTypeCache.Add(type, false);
            _hardwareIntrinsicDependentTypeCache[type] = false;
            if (IsHardwareIntrinsicDependentTypeInternal(type))
            {
                _hardwareIntrinsicDependentTypeCache[type] = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Uncached internal version of HW-dependent type detection logic. All recursive
        /// calls must go through the public method to prevent stack overflow in the presence
        /// of circles in the type definition graph.
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <returns>True if the type depends on HW intrinsics</returns>
        private static bool IsHardwareIntrinsicDependentTypeInternal(TypeDesc type)
        {
            if (type.IsSignatureVariable)
            {
                return true;
            }

            if (IsHardwareIntrinsic(type))
            {
                return true;
            }

            if (type.HasBaseType && IsHardwareIntrinsicDependentType(type.BaseType))
            {
                return true;
            }

            foreach (DefType interfaceType in type.RuntimeInterfaces)
            {
                if (IsHardwareIntrinsicDependentType(interfaceType))
                {
                    return true;
                }
            }

            foreach (FieldDesc field in type.GetFields())
            {
                if (IsHardwareIntrinsicDependentType(field.FieldType))
                {
                    return true;
                }
            }

            if (type.HasInstantiation)
            {
                foreach (TypeDesc instantiationType in type.Instantiation)
                {
                    if (IsHardwareIntrinsicDependentType(instantiationType))
                    {
                        return true;
                    }
                }
            }

            if (type.IsParameterizedType)
            {
                return IsHardwareIntrinsicDependentType(((ParameterizedType)type).ParameterType);
            }

            return false;
        }

        public static bool IsHardwareIntrinsicDependentMethod(MethodDesc methodDesc)
        {
            if (IsHardwareIntrinsicDependentType(methodDesc.OwningType) ||
                IsHardwareIntrinsicDependentType(methodDesc.Signature.ReturnType))
            {
                return true;
            }

            if (methodDesc.HasInstantiation)
            {
                foreach (TypeDesc instantiationTypeArg in methodDesc.Instantiation)
                {
                    if (IsHardwareIntrinsicDependentType(instantiationTypeArg))
                    {
                        return true;
                    }
                }
            }

            foreach (TypeDesc paramType in methodDesc.Signature)
            {
                if (IsHardwareIntrinsicDependentType(paramType))
                {
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
            // The implementation of IsSupported for codegen backends that don't support hardware intrinsics
            // at all is to return 0.
            if (IsIsSupportedMethod(method))
            {
                return new ILStubMethodIL(method,
                    new byte[] {
                        (byte)ILOpcode.ldc_i4_0,
                        (byte)ILOpcode.ret
                    },
                    Array.Empty<LocalVariableDefinition>(), null);
            }

            // Other methods throw PlatformNotSupportedException
            MethodDesc throwPnse = method.Context.GetHelperEntryPoint("ThrowHelpers", "ThrowPlatformNotSupportedException");

            return new ILStubMethodIL(method,
                    new byte[] {
                        (byte)ILOpcode.call, 1, 0, 0, 0,
                        (byte)ILOpcode.br_s, unchecked((byte)-7),
                    },
                    Array.Empty<LocalVariableDefinition>(),
                    new object[] { throwPnse });
        }

        /// <summary>
        /// Generates IL for the IsSupported property that reads this information from a field initialized by the runtime
        /// at startup. Returns null for hardware intrinsics whose support level is known at compile time
        /// (i.e. they're known to be always supported or always unsupported).
        /// </summary>
        public static MethodIL EmitIsSupportedIL(MethodDesc method, FieldDesc isSupportedField)
        {
            Debug.Assert(IsIsSupportedMethod(method));
            Debug.Assert(isSupportedField.IsStatic && isSupportedField.FieldType.IsWellKnownType(WellKnownType.Int32));

            TargetDetails target = method.Context.Target;
            MetadataType owningType = (MetadataType)method.OwningType;

            // Check for case of nested "X64" types
            if (owningType.Name == "X64")
            {
                if (target.Architecture != TargetArchitecture.X64)
                    return null;

                // Un-nest the type so that we can do a name match
                owningType = (MetadataType)owningType.ContainingType;
            }

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
                    case "Lzcnt":
                        flag = XArchIntrinsicConstants.Lzcnt;
                        break;
                    // NOTE: this switch is complemented by IsKnownSupportedIntrinsicAtCompileTime
                    // in the method below.
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

        /// <summary>
        /// Gets a value indicating whether the support for a given intrinsic is known at compile time.
        /// </summary>
        public static bool IsKnownSupportedIntrinsicAtCompileTime(MethodDesc method)
        {
            TargetDetails target = method.Context.Target;

            if (target.Architecture == TargetArchitecture.X64
                || target.Architecture == TargetArchitecture.X86)
            {
                var owningType = (MetadataType)method.OwningType;
                if (owningType.Name == "X64")
                {
                    if (target.Architecture != TargetArchitecture.X64)
                        return true;
                    owningType = (MetadataType)owningType.ContainingType;
                }

                if (owningType.Namespace != "System.Runtime.Intrinsics.X86")
                    return true;

                // Sse and Sse2 are baseline required intrinsics.
                // RyuJIT also uses Sse41/Sse42 with the general purpose Vector APIs.
                // RyuJIT only respects Popcnt if Sse41/Sse42 is also enabled.
                // Avx/Avx2/Bmi1/Bmi2 require VEX encoding and RyuJIT currently can't enable them
                // without enabling VEX encoding everywhere. We don't support them.
                // This list complements EmitIsSupportedIL above.
                return owningType.Name == "Sse" || owningType.Name == "Sse2"
                    || owningType.Name == "Sse41" || owningType.Name == "Sse42"
                    || owningType.Name == "Popcnt"
                    || owningType.Name == "Bmi1" || owningType.Name == "Bmi2"
                    || owningType.Name == "Avx" || owningType.Name == "Avx2";
            }

            return false;
        }

        // Keep this enumeration in sync with startup.cpp in the native runtime.
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
