// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL.Stubs;

namespace Internal.IL
{
    using Workarounds;

    public sealed class ReadyToRunILProvider : ILProvider
    {
        private MethodIL TryGetIntrinsicMethodILForInterlocked(MethodDesc method)
        {
            if (method.HasInstantiation && method.Name == "CompareExchange")
            {
                TypeDesc objectType = method.Context.GetWellKnownType(WellKnownType.Object);
                MethodDesc compareExchangeObject = method.OwningType.GetKnownMethod("CompareExchange",
                    new MethodSignature(
                        MethodSignatureFlags.Static,
                        genericParameterCount: 0,
                        returnType: objectType,
                        parameters: new TypeDesc[] { objectType.MakeByRefType(), objectType, objectType }));

                ILEmitter emit = new ILEmitter();
                ILCodeStream codeStream = emit.NewCodeStream();
                codeStream.EmitLdArg(0);
                codeStream.EmitLdArg(1);
                codeStream.EmitLdArg(2);
                codeStream.Emit(ILOpcode.call, emit.NewToken(compareExchangeObject));
                codeStream.Emit(ILOpcode.ret);
                return emit.Link(method);
            }

            return null;
        }

        private MethodIL TryGetIntrinsicMethodILForActivator(MethodDesc method)
        {
            if (method.Instantiation.Length == 1
                && method.Signature.Length == 0
                && method.Name == "CreateInstance")
            {
                TypeDesc type = method.Instantiation[0];
                if (type.IsValueType && type.GetDefaultConstructor() == null)
                {
                    // Replace the body with implementation that just returns "default"
                    MethodDesc createDefaultInstance = method.OwningType.GetKnownMethod("CreateDefaultInstance", method.GetTypicalMethodDefinition().Signature);
                    return GetMethodIL(createDefaultInstance.MakeInstantiatedMethod(type));
                }
            }

            return null;
        }

        private MethodIL TryGetIntrinsicMethodILForRuntimeHelpers(MethodDesc method)
        {
            if (method.Name == "IsReferenceOrContainsReferences")
            {
                TypeDesc type = method.Instantiation[0];
                if (type.IsGCPointer)
                    return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldc_i4_1, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), Array.Empty<object>());
                else
                    return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldc_i4_0, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), Array.Empty<object>());
            }

            // Ideally we could detect automatically whether a type is trivially equatable
            // (i.e., its operator == could be implemented via memcmp). But for now we'll
            // do the simple thing and hardcode the list of types we know fulfill this contract.
            // n.b. This doesn't imply that the type's CompareTo method can be memcmp-implemented,
            // as a method like CompareTo may need to take a type's signedness into account.
            if (method.Name == "IsBitwiseEquatable")
            {
                TypeDesc type = method.Instantiation[0];
                switch (type.UnderlyingType.Category)
                {
                    case TypeFlags.Boolean:
                    case TypeFlags.Byte:
                    case TypeFlags.SByte:
                    case TypeFlags.Char:
                    case TypeFlags.UInt16:
                    case TypeFlags.Int16:
                    case TypeFlags.UInt32:
                    case TypeFlags.Int32:
                    case TypeFlags.UInt64:
                    case TypeFlags.Int64:
                    case TypeFlags.IntPtr:
                    case TypeFlags.UIntPtr:
                        return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldc_i4_1, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), Array.Empty<object>());
                    default:
                        var mdType = type as MetadataType;
                        if (mdType != null && mdType.Name == "Rune" && mdType.Namespace == "System.Text")
                            goto case TypeFlags.UInt32;

                        if (mdType != null && mdType.Name == "Char8" && mdType.Namespace == "System")
                            goto case TypeFlags.Byte;

                        return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldc_i4_0, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), Array.Empty<object>());
                }
            }

            if (method.Name == "GetRawSzArrayData")
            {
                ILEmitter emit = new ILEmitter();
                ILCodeStream codeStream = emit.NewCodeStream();
                codeStream.EmitLdArg(0);
                codeStream.Emit(ILOpcode.ldflda, emit.NewToken(method.Context.SystemModule.GetKnownType("System.Runtime.CompilerServices", "RawSzArrayData").GetField("Data")));
                codeStream.Emit(ILOpcode.ret);
                return emit.Link(method);
            }

            return null;
        }

        /// <summary>
        /// Provides method bodies for intrinsics recognized by the compiler.
        /// It can return null if it's not an intrinsic recognized by the compiler,
        /// but an intrinsic e.g. recognized by codegen.
        /// </summary>
        private MethodIL TryGetIntrinsicMethodIL(MethodDesc method)
        {
            var mdType = method.OwningType as MetadataType;
            if (mdType == null)
                return null;

            if (mdType.Name == "RuntimeHelpers" && mdType.Namespace == "System.Runtime.CompilerServices")
            {
                return TryGetIntrinsicMethodILForRuntimeHelpers(method);
            }

            if (mdType.Name == "Unsafe" && mdType.Namespace == "Internal.Runtime.CompilerServices")
            {
                return UnsafeIntrinsics.EmitIL(method);
            }

            return null;
        }

        /// <summary>
        /// Provides method bodies for intrinsics recognized by the compiler that
        /// are specialized per instantiation. It can return null if the intrinsic
        /// is not recognized.
        /// </summary>
        private MethodIL TryGetPerInstantiationIntrinsicMethodIL(MethodDesc method)
        {
            var mdType = method.OwningType as MetadataType;
            if (mdType == null)
                return null;

            if (mdType.Name == "RuntimeHelpers" && mdType.Namespace == "System.Runtime.CompilerServices")
            {
                return TryGetIntrinsicMethodILForRuntimeHelpers(method);
            }

            if (mdType.Name == "Interlocked" && mdType.Namespace == "System.Threading")
            {
                return TryGetIntrinsicMethodILForInterlocked(method);
            }

            if (mdType.Name == "Activator" && mdType.Namespace == "System")
            {
                return TryGetIntrinsicMethodILForActivator(method);
            }

            return null;
        }

        public override MethodIL GetMethodIL(MethodDesc method)
        {
            if (method is EcmaMethod)
            {
                if (method.IsIntrinsicWorkaround())
                {
                    MethodIL result = TryGetIntrinsicMethodIL(method);
                    if (result != null)
                        return result;
                }

                MethodIL methodIL = EcmaMethodIL.Create((EcmaMethod)method);
                if (methodIL != null)
                    return methodIL;

                return null;
            }
            else if (method is MethodForInstantiatedType || method is InstantiatedMethod)
            {
                // Intrinsics specialized per instantiation
                if (method.IsIntrinsicWorkaround())
                {
                    MethodIL methodIL = TryGetPerInstantiationIntrinsicMethodIL(method);
                    if (methodIL != null)
                        return methodIL;
                }

                var methodDefinitionIL = GetMethodIL(method.GetTypicalMethodDefinition());
                if (methodDefinitionIL == null)
                    return null;
                return new InstantiatedMethodIL(method, methodDefinitionIL);
            }
            else
            {
                return null;
            }
        }
    }
}

namespace Internal.IL.Workarounds
{
    static class IntrinsicExtensions
    {
        // We should ideally mark interesting methods a [Intrinsic] to avoid having to
        // name match everything in CoreLib.
        public static bool IsIntrinsicWorkaround(this MethodDesc method)
        {
            return method.OwningType is MetadataType mdType && mdType.Module == method.Context.SystemModule;
        }
    }
}
