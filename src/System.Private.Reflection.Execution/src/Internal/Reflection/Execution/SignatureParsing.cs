using System;
using System.Diagnostics;
using System.Reflection;
using global::Internal.NativeFormat;
using global::Internal.Runtime.TypeLoader;
using Internal.Runtime.Augments;

using Debug = System.Diagnostics.Debug;

namespace Internal.Reflection.Execution
{
    internal static class SigParsing
    {
        public static RuntimeTypeHandle GetTypeFromNativeLayoutSignature(ref NativeParser parser, uint offset)
        {
            IntPtr remainingSignature;
            RuntimeTypeHandle typeHandle;

            IntPtr signatureAddress = parser.Reader.OffsetToAddress(offset);
            bool success = TypeLoaderEnvironment.Instance.GetTypeFromSignatureAndContext(signatureAddress, null, null, out typeHandle, out remainingSignature);

            // Reset the parser to after the type
            parser = new NativeParser(parser.Reader, parser.Reader.AddressToOffset(remainingSignature));

            return typeHandle;
        }
    }
    
    internal class SigComparer
    {
        public static bool CompareMethodSigWithMethodInfo(NativeParser reader, MethodBase methodBase)
        {
            if (!CompareCallingConventions((MethodCallingConvention)reader.GetUnsigned(), methodBase))
                return false;

            // Sigs are encoded in the native layout as uninstantiated. Work with the generic method def
            // here so that the arguments will compare properly.
            MethodBase uninstantiatedMethod = methodBase;
            if (methodBase is MethodInfo && methodBase.IsGenericMethod && !methodBase.IsGenericMethodDefinition)
            {
                uninstantiatedMethod = ((MethodInfo)methodBase).GetGenericMethodDefinition();
            }

            if (uninstantiatedMethod.IsGenericMethod)
            {
                uint genericParamCount1 = reader.GetUnsigned();
                int genericParamCount2 = uninstantiatedMethod.GetGenericArguments().Length;

                if (genericParamCount1 != genericParamCount2)
                    return false;
            }

            uint parameterCount = reader.GetUnsigned();
            
            if (parameterCount != uninstantiatedMethod.GetParameters().Length)
                return false;

            if (uninstantiatedMethod is MethodInfo)
            {
                // Not a constructor. Compare return types
                if (!CompareTypeSigWithType(ref reader, ((MethodInfo)uninstantiatedMethod).ReturnType))
                    return false;
            }
            else
            {
                Debug.Assert(uninstantiatedMethod is ConstructorInfo);
                // The first parameter had better be void on a constructor
                if (!CompareTypeSigWithType(ref reader, CommonRuntimeTypes.Void))
                    return false;
            }

            for (uint i = 0; i < parameterCount; i++)
            {
                if (!CompareTypeSigWithType(ref reader, uninstantiatedMethod.GetParameters()[i].ParameterType))
                    return false;
            }

            return true;
        }

        private static bool CompareTypeSigWithType(ref NativeParser parser, Type type)
        {
            // startOffset lets us backtrack to the TypeSignatureKind for external types since the TypeLoader
            // expects to read it in.
            uint startOffset = parser.Offset;

            uint data;
            var typeSignatureKind = parser.GetTypeSignatureKind(out data);

            switch (typeSignatureKind)
            {
                case TypeSignatureKind.Lookback:
                    {
                        NativeParser lookbackParser = parser.GetLookbackParser(data);
                        return CompareTypeSigWithType(ref lookbackParser, type);
                    }

                case TypeSignatureKind.Modifier:
                    {
                        // Ensure the modifier kind (vector, pointer, byref) is the same
                        TypeModifierKind modifierKind = (TypeModifierKind)data;
                        switch (modifierKind)
                        {
                            case TypeModifierKind.Array:
                                if (!type.IsArray)
                                    return false;
                                break;
                            case TypeModifierKind.ByRef:
                                if (!type.IsByRef)
                                    return false;
                                break;
                            case TypeModifierKind.Pointer:
                                if (!type.IsPointer)
                                    return false;
                                break;
                        }
                        return CompareTypeSigWithType(ref parser, type.GetElementType());
                    }

                case TypeSignatureKind.Variable:
                    {
                        if (!type.IsGenericParameter)
                            return false;

                        bool isMethodVar = (data & 0x1) == 1;
                        uint index = data >> 1;

                        if (index != type.GenericParameterPosition)
                            return false;

                        // MVARs are represented as having a non-null DeclaringMethod in the reflection object model
                        if (isMethodVar ^ (type.GetTypeInfo().DeclaringMethod != null))
                            return false;

                        break;
                    }

                case TypeSignatureKind.MultiDimArray:
                    {
                        if (!type.IsArray)
                            return false;

                        if (data != type.GetArrayRank())
                            return false;

                        if (!CompareTypeSigWithType(ref parser, type.GetElementType()))
                            return false;

                        uint boundCount1 = parser.GetUnsigned();
                        for (uint i = 0; i < boundCount1; i++)
                        {
                            parser.GetUnsigned();
                        }

                        uint lowerBoundCount1 = parser.GetUnsigned();

                        for (uint i = 0; i < lowerBoundCount1; i++)
                        {
                            parser.GetUnsigned();
                        }
                        break;
                    }

                case TypeSignatureKind.FunctionPointer:
                    {
                        // callingConvention is in data
                        uint argCount1 = parser.GetUnsigned();

                        for (uint i = 0; i < argCount1; i++)
                        {
                            if (!CompareTypeSigWithType(ref parser, type))
                                return false;
                        }
                        return false;
                    }

                case TypeSignatureKind.Instantiation:
                    {
                        // Type Def                    
                        if (!type.GetTypeInfo().IsGenericType)
                            return false;

                        if (!CompareTypeSigWithType(ref parser, type.GetGenericTypeDefinition()))
                            return false;

                        for (uint i = 0; i < data; i++)
                        {
                            if (!CompareTypeSigWithType(ref parser, type.GenericTypeArguments[i]))
                                return false;
                        }
                        break;
                    }

                case TypeSignatureKind.External:
                    {
                        RuntimeTypeHandle type1 = SigParsing.GetTypeFromNativeLayoutSignature(ref parser, startOffset);

                        if (!CanGetTypeHandle(type))
                            return false;

                        RuntimeTypeHandle type2 = type.TypeHandle;
                        if (!type1.Equals(type2))
                            return false;
                        break;
                    }

                default:
                    return false;
            }
            return true;
        }

        private static bool CompareCallingConventions(MethodCallingConvention callingConvention, MethodBase methodBase)
        {
            if (callingConvention.HasFlag(MethodCallingConvention.Static) != methodBase.IsStatic)
                return false;

            if (callingConvention.HasFlag(MethodCallingConvention.Generic) != (methodBase.IsGenericMethod | methodBase.IsGenericMethodDefinition))
                return false;

            return true;
        }

        private static bool CanGetTypeHandle(Type type)
        {
            if (type.HasElementType)
            {
                return CanGetTypeHandle(type.GetElementType());
            }
            else if (type.IsConstructedGenericType)
            {
                foreach (var typeArg in type.GenericTypeArguments)
                {
                    if (!CanGetTypeHandle(typeArg))
                    {
                        return false;
                    }
                }
            }
            else if (type.IsGenericParameter)
            {
                return false;
            }

            return true;
        }
    }
}
