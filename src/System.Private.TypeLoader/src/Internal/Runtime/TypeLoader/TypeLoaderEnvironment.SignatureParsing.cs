// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Diagnostics;
using System.Reflection;

using Internal.Runtime.TypeLoader;
using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

using Internal.Metadata.NativeFormat;
using Internal.NativeFormat;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    public sealed partial class TypeLoaderEnvironment
    {
        public bool CompareMethodSignatures(RuntimeMethodSignature signature1, RuntimeMethodSignature signature2)
        {
            IntPtr nativeLayoutSignature1 = signature1.NativeLayoutSignature;
            IntPtr nativeLayoutSignature2 = signature2.NativeLayoutSignature;

            if ((nativeLayoutSignature1 != IntPtr.Zero) && (nativeLayoutSignature2 != IntPtr.Zero))
            {
                if (nativeLayoutSignature1 == nativeLayoutSignature2)
                    return true;

                NativeReader reader1 = GetNativeLayoutInfoReader(RuntimeAugments.GetModuleFromPointer(nativeLayoutSignature1));
                NativeParser parser1 = new NativeParser(reader1, reader1.AddressToOffset(nativeLayoutSignature1));

                NativeReader reader2 = GetNativeLayoutInfoReader(RuntimeAugments.GetModuleFromPointer(nativeLayoutSignature2));
                NativeParser parser2 = new NativeParser(reader2, reader2.AddressToOffset(nativeLayoutSignature2));

                return CompareMethodSigs(parser1, parser2);
            }
            else if (nativeLayoutSignature1 != IntPtr.Zero)
            {
                int token = signature2.Token;
                MetadataReader metadataReader = ModuleList.Instance.GetMetadataReaderForModule(signature2.ModuleHandle);

                MethodSignatureComparer comparer = new MethodSignatureComparer(metadataReader, token.AsHandle().ToMethodHandle(metadataReader));
                return comparer.IsMatchingNativeLayoutMethodSignature(nativeLayoutSignature1);
            }
            else if (nativeLayoutSignature2 != IntPtr.Zero)
            {
                int token = signature1.Token;
                MetadataReader metadataReader = ModuleList.Instance.GetMetadataReaderForModule(signature1.ModuleHandle);

                MethodSignatureComparer comparer = new MethodSignatureComparer(metadataReader, token.AsHandle().ToMethodHandle(metadataReader));
                return comparer.IsMatchingNativeLayoutMethodSignature(nativeLayoutSignature2);
            }
            else
            {
                // For now, RuntimeMethodSignatures are only used to compare for method signature equality (along with their Name)
                // So we can implement this with the simple equals check
                if (signature1.Token != signature2.Token)
                    return false;

                if (signature1.ModuleHandle != signature2.ModuleHandle)
                    return false;

                return true;
            }
        }

        public uint GetGenericArgumentCountFromMethodNameAndSignature(MethodNameAndSignature signature)
        {
            if (signature.Signature.IsNativeLayoutSignature)
            {
                IntPtr sigPtr = signature.Signature.NativeLayoutSignature;
                NativeReader reader = GetNativeLayoutInfoReader(RuntimeAugments.GetModuleFromPointer(sigPtr));
                NativeParser parser = new NativeParser(reader, reader.AddressToOffset(sigPtr));

                return GetGenericArgCountFromSig(parser);
            }
            else
            {
                var metadataReader = ModuleList.Instance.GetMetadataReaderForModule(signature.Signature.ModuleHandle);
                var methodHandle = signature.Signature.Token.AsHandle().ToMethodHandle(metadataReader);

                var method = methodHandle.GetMethod(metadataReader);
                var methodSignature = method.Signature.GetMethodSignature(metadataReader);
                return checked((uint)methodSignature.GenericParameterCount);
            }
        }

        public bool TryGetMethodNameAndSignatureFromNativeLayoutSignature(ref IntPtr signature, out MethodNameAndSignature nameAndSignature)
        {
            nameAndSignature = null;

            NativeReader reader = GetNativeLayoutInfoReader(RuntimeAugments.GetModuleFromPointer(signature));
            uint offset = reader.AddressToOffset(signature);
            NativeParser parser = new NativeParser(reader, offset);
            if (parser.IsNull)
                return false;

            IntPtr methodSigPtr;
            IntPtr methodNameSigPtr;
            nameAndSignature = GetMethodNameAndSignature(ref parser, out methodNameSigPtr, out methodSigPtr);
            signature = (IntPtr)((long)signature + (parser.Offset - offset));
            return true;
        }

        public bool TryGetMethodNameAndSignaturePointersFromNativeLayoutSignature(IntPtr module, uint methodNameAndSigToken, out IntPtr methodNameSigPtr, out IntPtr methodSigPtr)
        {
            methodNameSigPtr = default(IntPtr);
            methodSigPtr = default(IntPtr);

            NativeReader reader = GetNativeLayoutInfoReader(module);
            uint offset = methodNameAndSigToken;
            NativeParser parser = new NativeParser(reader, offset);
            if (parser.IsNull)
                return false;

            methodNameSigPtr = parser.Reader.OffsetToAddress(parser.Offset);
            string methodName = parser.GetString();

            // Signatures are indirected to through a relative offset so that we don't have to parse them
            // when not comparing signatures (parsing them requires resolving types and is tremendously 
            // expensive).
            NativeParser sigParser = parser.GetParserFromRelativeOffset();
            methodSigPtr = sigParser.Reader.OffsetToAddress(sigParser.Offset);

            return true;
        }

        public bool TryGetMethodNameAndSignatureFromNativeLayoutOffset(IntPtr moduleHandle, uint nativeLayoutOffset, out MethodNameAndSignature nameAndSignature)
        {
            nameAndSignature = null;

            NativeReader reader = GetNativeLayoutInfoReader(moduleHandle);
            NativeParser parser = new NativeParser(reader, nativeLayoutOffset);
            if (parser.IsNull)
                return false;

            IntPtr methodSigPtr;
            IntPtr methodNameSigPtr;
            nameAndSignature = GetMethodNameAndSignature(ref parser, out methodNameSigPtr, out methodSigPtr);
            return true;
        }

        internal MethodNameAndSignature GetMethodNameAndSignature(ref NativeParser parser, out IntPtr methodNameSigPtr, out IntPtr methodSigPtr)
        {
            methodNameSigPtr = parser.Reader.OffsetToAddress(parser.Offset);
            string methodName = parser.GetString();

            // Signatures are indirected to through a relative offset so that we don't have to parse them
            // when not comparing signatures (parsing them requires resolving types and is tremendously 
            // expensive).
            NativeParser sigParser = parser.GetParserFromRelativeOffset();
            methodSigPtr = sigParser.Reader.OffsetToAddress(sigParser.Offset);

            return new MethodNameAndSignature(methodName, RuntimeMethodSignature.CreateFromNativeLayoutSignature(methodSigPtr));
        }

        internal bool IsStaticMethodSignature(RuntimeMethodSignature methodSig)
        {
            if (methodSig.IsNativeLayoutSignature)
            {
                IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(methodSig.NativeLayoutSignature);
                NativeReader reader = GetNativeLayoutInfoReader(moduleHandle);
                NativeParser parser = new NativeParser(reader, reader.AddressToOffset(methodSig.NativeLayoutSignature));

                MethodCallingConvention callingConvention = (MethodCallingConvention)parser.GetUnsigned();
                return callingConvention.HasFlag(MethodCallingConvention.Static);
            }
            else
            {
                var metadataReader = ModuleList.Instance.GetMetadataReaderForModule(methodSig.ModuleHandle);
                var methodHandle = methodSig.Token.AsHandle().ToMethodHandle(metadataReader);

                var method = methodHandle.GetMethod(metadataReader);
                return (method.Flags & MethodAttributes.Static) != 0;
            }
        }

        internal bool GetCallingConverterDataFromMethodSignature(TypeSystemContext context, RuntimeMethodSignature methodSig, NativeLayoutInfoLoadContext nativeLayoutContext, out bool hasThis, out TypeDesc[] parameters, out bool[] parametersWithGenericDependentLayout)
        {
            if (methodSig.IsNativeLayoutSignature)
                return GetCallingConverterDataFromMethodSignature_NativeLayout(context, methodSig.NativeLayoutSignature, nativeLayoutContext, out hasThis, out parameters, out parametersWithGenericDependentLayout);
            else
            {
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
                MetadataReader metadataReader = ModuleList.Instance.GetMetadataReaderForModule(methodSig.ModuleHandle);
                var methodHandle = methodSig.Token.AsHandle().ToMethodHandle(metadataReader);
                var metadataUnit = ((TypeLoaderTypeSystemContext)context).ResolveMetadataUnit(methodSig.ModuleHandle);
                var parser = new Internal.TypeSystem.NativeFormat.NativeFormatSignatureParser(metadataUnit, metadataReader.GetMethod(methodHandle).Signature, metadataReader);
                var signature = parser.ParseMethodSignature();

                return GetCallingConverterDataFromMethodSignature_MethodSignature(signature, nativeLayoutContext, out hasThis, out parameters, out parametersWithGenericDependentLayout);
#else
                parametersWithGenericDependentLayout = null;
                hasThis = false;
                parameters = null;
                return false;
#endif
            }
        }

        internal bool GetCallingConverterDataFromMethodSignature_NativeLayout(TypeSystemContext context, IntPtr methodSig, NativeLayoutInfoLoadContext nativeLayoutContext, out bool hasThis, out TypeDesc[] parameters, out bool[] parametersWithGenericDependentLayout)
        {
            hasThis = false;
            parameters = null;

            IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(methodSig);
            NativeReader reader = GetNativeLayoutInfoReader(moduleHandle);
            NativeParser parser = new NativeParser(reader, reader.AddressToOffset(methodSig));

            MethodCallingConvention callingConvention = (MethodCallingConvention)parser.GetUnsigned();
            hasThis = !callingConvention.HasFlag(MethodCallingConvention.Static);

            uint numGenArgs = callingConvention.HasFlag(MethodCallingConvention.Generic) ? parser.GetUnsigned() : 0;

            uint parameterCount = parser.GetUnsigned();
            parameters = new TypeDesc[parameterCount + 1];
            parametersWithGenericDependentLayout = new bool[parameterCount + 1];

            // One extra parameter to account for the return type
            for (uint i = 0; i <= parameterCount; i++)
            {
                // NativeParser is a struct, so it can be copied. 
                NativeParser parserCopy = parser;

                // Parse the signature twice. The first time to find out the exact type of the signature
                // The second time to identify if the parameter loaded via the signature should be forced to be
                // passed byref as part of the universal generic calling convention.
                parameters[i] = GetConstructedTypeFromParserAndNativeLayoutContext(ref parser, nativeLayoutContext);
                parametersWithGenericDependentLayout[i] = TypeSignatureHasVarsNeedingCallingConventionConverter(ref parserCopy, context, HasVarsInvestigationLevel.Parameter);
                if (parameters[i] == null)
                    return false;
            }

            return true;
        }

#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
        static private bool GetCallingConverterDataFromMethodSignature_MethodSignature(TypeSystem.MethodSignature methodSignature, NativeLayoutInfoLoadContext nativeLayoutContext, out bool hasThis, out TypeDesc[] parameters, out bool[] parametersWithGenericDependentLayout)
        {
            // Compute parameters dependent on generic instantiation for their layout
            parametersWithGenericDependentLayout = new bool[methodSignature.Length + 1];
            parametersWithGenericDependentLayout[0] = TypeHasLayoutDependentOnGenericInstantiation(methodSignature.ReturnType, HasVarsInvestigationLevel.Parameter);
            for (int i = 0; i < methodSignature.Length; i++)
            {
                parametersWithGenericDependentLayout[i + 1] = TypeHasLayoutDependentOnGenericInstantiation(methodSignature[i], HasVarsInvestigationLevel.Parameter);
            }

            // Compute hasThis-ness
            hasThis = !methodSignature.IsStatic;

            // Compute parameter exact types
            parameters = new TypeDesc[methodSignature.Length + 1];

            Instantiation typeInstantiation = nativeLayoutContext._typeArgumentHandles;
            Instantiation methodInstantiation = nativeLayoutContext._methodArgumentHandles;

            parameters[0] = methodSignature.ReturnType.InstantiateSignature(typeInstantiation, methodInstantiation);
            for (int i = 0; i < methodSignature.Length; i++)
            {
                parameters[i + 1] = methodSignature[i].InstantiateSignature(typeInstantiation, methodInstantiation);
            }

            return true;
        }
#endif

        /// <summary>
        /// IF THESE SEMANTICS EVER CHANGE UPDATE THE LOGIC WHICH DEFINES THIS BEHAVIOR IN 
        /// THE DYNAMIC TYPE LOADER AS WELL AS THE COMPILER. 
        ///
        /// Parameter's are considered to have type layout dependent on their generic instantiation
        /// if the type of the parameter in its signature is a type variable, or if the type is a generic 
        /// structure which meets 2 characteristics:
        /// 1. Structure size/layout is affected by the size/layout of one or more of its generic parameters
        /// 2. One or more of the generic parameters is a type variable, or a generic structure which also recursively
        ///    would satisfy constraint 2. (Note, that in the recursion case, whether or not the structure is affected
        ///    by the size/layout of its generic parameters is not investigated.)
        ///    
        /// Examples parameter types, and behavior.
        /// 
        /// T -> true
        /// List<T> -> false
        /// StructNotDependentOnArgsForSize<T> -> false
        /// GenStructDependencyOnArgsForSize<T> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<T>> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<List<T>>>> -> false
        /// 
        /// Example non-parameter type behavior
        /// T -> true
        /// List<T> -> false
        /// StructNotDependentOnArgsForSize<T> -> *true*
        /// GenStructDependencyOnArgsForSize<T> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<T>> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<List<T>>>> -> false
        /// </summary>
        static private bool TypeHasLayoutDependentOnGenericInstantiation(TypeDesc type, HasVarsInvestigationLevel investigationLevel)
        {
            if (type is SignatureVariable)
            {
                return true;
            }
            else if (type.IsDefType && type.HasInstantiation && type.IsValueType)
            {
                foreach (TypeDesc valueTypeInstantiationParam in type.Instantiation)
                {
                    if (TypeHasLayoutDependentOnGenericInstantiation(valueTypeInstantiationParam, HasVarsInvestigationLevel.NotParameter))
                    {
                        if (investigationLevel == HasVarsInvestigationLevel.Parameter)
                        {
                            bool needsCallingConventionConverter;
                            if (!TypeLoaderEnvironment.Instance.TryComputeHasInstantiationDeterminedSize((DefType)type, out needsCallingConventionConverter))
                                Environment.FailFast("Unable to setup calling convention converter correctly");
                            return needsCallingConventionConverter;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            else
            {
                // All other forms of type do not change their shape dependent on signature variables.
                return false;
            }
        }

        internal bool MethodSignatureHasVarsNeedingCallingConventionConverter(TypeSystemContext context, RuntimeMethodSignature methodSig)
        {
            if (methodSig.IsNativeLayoutSignature)
                return MethodSignatureHasVarsNeedingCallingConventionConverter_NativeLayout(context, methodSig.NativeLayoutSignature);
            else
            {
#if SUPPORTS_NATIVE_METADATA_TYPE_LOADING
                MetadataReader metadataReader = ModuleList.Instance.GetMetadataReaderForModule(methodSig.ModuleHandle);
                var methodHandle = methodSig.Token.AsHandle().ToMethodHandle(metadataReader);
                var metadataUnit = ((TypeLoaderTypeSystemContext)context).ResolveMetadataUnit(methodSig.ModuleHandle);
                var parser = new Internal.TypeSystem.NativeFormat.NativeFormatSignatureParser(metadataUnit, metadataReader.GetMethod(methodHandle).Signature, metadataReader);
                var signature = parser.ParseMethodSignature();

                return MethodSignatureHasVarsNeedingCallingConventionConverter_MethodSignature(signature);
#else
                Environment.FailFast("Cannot parse signature");
                return false;
#endif
            }
        }

        private bool MethodSignatureHasVarsNeedingCallingConventionConverter_NativeLayout(TypeSystemContext context, IntPtr methodSig)
        {
            IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(methodSig);
            NativeReader reader = GetNativeLayoutInfoReader(moduleHandle);
            NativeParser parser = new NativeParser(reader, reader.AddressToOffset(methodSig));

            MethodCallingConvention callingConvention = (MethodCallingConvention)parser.GetUnsigned();
            uint numGenArgs = callingConvention.HasFlag(MethodCallingConvention.Generic) ? parser.GetUnsigned() : 0;
            uint parameterCount = parser.GetUnsigned();

            // Check the return type of the method
            if (TypeSignatureHasVarsNeedingCallingConventionConverter(ref parser, context, HasVarsInvestigationLevel.Parameter))
                return true;

            // Check the parameters of the method
            for (uint i = 0; i < parameterCount; i++)
            {
                if (TypeSignatureHasVarsNeedingCallingConventionConverter(ref parser, context, HasVarsInvestigationLevel.Parameter))
                    return true;
            }

            return false;
        }

        static public bool MethodSignatureHasVarsNeedingCallingConventionConverter_MethodSignature(TypeSystem.MethodSignature methodSignature)
        {
            if (TypeHasLayoutDependentOnGenericInstantiation(methodSignature.ReturnType, HasVarsInvestigationLevel.Parameter))
                return true;

            for (int i = 0; i < methodSignature.Length; i++)
            {
                if (TypeHasLayoutDependentOnGenericInstantiation(methodSignature[i], HasVarsInvestigationLevel.Parameter))
                    return true;
            }

            return false;
        }


        #region Private Helpers
        private enum HasVarsInvestigationLevel
        {
            Parameter,
            NotParameter,
            Ignore
        }

        /// <summary>
        /// IF THESE SEMANTICS EVER CHANGE UPDATE THE LOGIC WHICH DEFINES THIS BEHAVIOR IN 
        /// THE DYNAMIC TYPE LOADER AS WELL AS THE COMPILER. 
        ///
        /// Parameter's are considered to have type layout dependent on their generic instantiation
        /// if the type of the parameter in its signature is a type variable, or if the type is a generic 
        /// structure which meets 2 characteristics:
        /// 1. Structure size/layout is affected by the size/layout of one or more of its generic parameters
        /// 2. One or more of the generic parameters is a type variable, or a generic structure which also recursively
        ///    would satisfy constraint 2. (Note, that in the recursion case, whether or not the structure is affected
        ///    by the size/layout of its generic parameters is not investigated.)
        ///    
        /// Examples parameter types, and behavior.
        /// 
        /// T -> true
        /// List<T> -> false
        /// StructNotDependentOnArgsForSize<T> -> false
        /// GenStructDependencyOnArgsForSize<T> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<T>> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<List<T>>>> -> false
        /// 
        /// Example non-parameter type behavior
        /// T -> true
        /// List<T> -> false
        /// StructNotDependentOnArgsForSize<T> -> *true*
        /// GenStructDependencyOnArgsForSize<T> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<T>> -> true
        /// StructNotDependentOnArgsForSize<GenStructDependencyOnArgsForSize<List<T>>>> -> false
        /// </summary>
        private bool TypeSignatureHasVarsNeedingCallingConventionConverter(ref NativeParser parser, TypeSystemContext context, HasVarsInvestigationLevel investigationLevel)
        {
            uint data;
            var kind = parser.GetTypeSignatureKind(out data);

            switch (kind)
            {
                case TypeSignatureKind.External: return false;
                case TypeSignatureKind.Variable: return true;

                case TypeSignatureKind.Lookback:
                    {
                        var lookbackParser = parser.GetLookbackParser(data);
                        return TypeSignatureHasVarsNeedingCallingConventionConverter(ref lookbackParser, context, investigationLevel);
                    }

                case TypeSignatureKind.Instantiation:
                    {
                        RuntimeTypeHandle genericTypeDef;
                        if (!TryGetTypeFromSimpleTypeSignature(ref parser, out genericTypeDef))
                        {
                            Debug.Assert(false);
                            return true;    // Returning true will prevent further reading from the native parser
                        }

                        if (!RuntimeAugments.IsValueType(genericTypeDef))
                        {
                            // Reference types are treated like pointers. No calling convention conversion needed. Just consume the rest of the signature.
                            for (uint i = 0; i < data; i++)
                                TypeSignatureHasVarsNeedingCallingConventionConverter(ref parser, context, HasVarsInvestigationLevel.Ignore);
                            return false;
                        }
                        else
                        {
                            bool result = false;
                            for (uint i = 0; i < data; i++)
                                result = TypeSignatureHasVarsNeedingCallingConventionConverter(ref parser, context, HasVarsInvestigationLevel.NotParameter) || result;

                            if ((result == true) && (investigationLevel == HasVarsInvestigationLevel.Parameter))
                            {
                                if (!TryComputeHasInstantiationDeterminedSize(genericTypeDef, context, out result))
                                    Environment.FailFast("Unable to setup calling convention converter correctly");

                                return result;
                            }

                            return result;
                        }
                    }

                case TypeSignatureKind.Modifier:
                    {
                        // Arrays, pointers and byref types signatures are treated as pointers, not requiring calling convention conversion.
                        // Just consume the parameter type from the stream and return false;
                        TypeSignatureHasVarsNeedingCallingConventionConverter(ref parser, context, HasVarsInvestigationLevel.Ignore);
                        return false;
                    }

                case TypeSignatureKind.MultiDimArray:
                    {
                        // No need for a calling convention converter for this case. Just consume the signature from the stream.

                        TypeSignatureHasVarsNeedingCallingConventionConverter(ref parser, context, HasVarsInvestigationLevel.Ignore);

                        uint boundCount = parser.GetUnsigned();
                        for (uint i = 0; i < boundCount; i++)
                            parser.GetUnsigned();

                        uint lowerBoundCount = parser.GetUnsigned();
                        for (uint i = 0; i < lowerBoundCount; i++)
                            parser.GetUnsigned();
                    }
                    return false;

                case TypeSignatureKind.FunctionPointer:
                    {
                        // No need for a calling convention converter for this case. Just consume the signature from the stream.

                        uint argCount = parser.GetUnsigned();
                        for (uint i = 0; i < argCount; i++)
                            TypeSignatureHasVarsNeedingCallingConventionConverter(ref parser, context, HasVarsInvestigationLevel.Ignore);
                    }
                    return false;

                default:
                    parser.ThrowBadImageFormatException();
                    return true;
            }
        }

        private bool TryGetTypeFromSimpleTypeSignature(ref NativeParser parser, out RuntimeTypeHandle typeHandle)
        {
            uint data;
            TypeSignatureKind kind = parser.GetTypeSignatureKind(out data);

            if (kind == TypeSignatureKind.Lookback)
            {
                var lookbackParser = parser.GetLookbackParser(data);
                return TryGetTypeFromSimpleTypeSignature(ref lookbackParser, out typeHandle);
            }
            else if (kind == TypeSignatureKind.External)
            {
                typeHandle = GetExternalTypeHandle(ref parser, data);
                return true;
            }

            // Not a simple type signature... requires more work to skip
            typeHandle = default(RuntimeTypeHandle);
            return false;
        }

        private RuntimeTypeHandle GetExternalTypeHandle(ref NativeParser parser, uint typeIndex)
        {
            IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(parser.Reader.OffsetToAddress(parser.Offset));
            Debug.Assert(moduleHandle != IntPtr.Zero);

            RuntimeTypeHandle result;

            TypeSystemContext context = TypeSystemContextFactory.Create();
            {
                NativeLayoutInfoLoadContext nativeLayoutContext = new NativeLayoutInfoLoadContext();
                nativeLayoutContext._moduleHandle = moduleHandle;
                nativeLayoutContext._typeSystemContext = context;

                TypeDesc type = nativeLayoutContext.GetExternalType(typeIndex);
                result = type.RuntimeTypeHandle;
            }
            TypeSystemContextFactory.Recycle(context);

            Debug.Assert(!result.IsNull());
            return result;
        }

        private uint GetGenericArgCountFromSig(NativeParser parser)
        {
            MethodCallingConvention callingConvention = (MethodCallingConvention)parser.GetUnsigned();

            if ((callingConvention & MethodCallingConvention.Generic) == MethodCallingConvention.Generic)
            {
                return parser.GetUnsigned();
            }
            else
            {
                return 0;
            }
        }

        private bool CompareMethodSigs(NativeParser parser1, NativeParser parser2)
        {
            MethodCallingConvention callingConvention1 = (MethodCallingConvention)parser1.GetUnsigned();
            MethodCallingConvention callingConvention2 = (MethodCallingConvention)parser2.GetUnsigned();

            if (callingConvention1 != callingConvention2)
                return false;

            if ((callingConvention1 & MethodCallingConvention.Generic) == MethodCallingConvention.Generic)
            {
                if (parser1.GetUnsigned() != parser2.GetUnsigned())
                    return false;
            }

            uint parameterCount1 = parser1.GetUnsigned();
            uint parameterCount2 = parser2.GetUnsigned();
            if (parameterCount1 != parameterCount2)
                return false;

            // Compare one extra parameter to account for the return type
            for (uint i = 0; i <= parameterCount1; i++)
            {
                if (!CompareTypeSigs(ref parser1, ref parser2))
                    return false;
            }

            return true;
        }

        private bool CompareTypeSigs(ref NativeParser parser1, ref NativeParser parser2)
        {
            // startOffset lets us backtrack to the TypeSignatureKind for external types since the TypeLoader
            // expects to read it in.
            uint data1;
            uint startOffset1 = parser1.Offset;
            var typeSignatureKind1 = parser1.GetTypeSignatureKind(out data1);

            // If the parser is at a lookback type, get a new parser for it and recurse.
            // Since we haven't read the element type of parser2 yet, we just pass it in unchanged
            if (typeSignatureKind1 == TypeSignatureKind.Lookback)
            {
                NativeParser lookbackParser1 = parser1.GetLookbackParser(data1);
                return CompareTypeSigs(ref lookbackParser1, ref parser2);
            }

            uint data2;
            uint startOffset2 = parser2.Offset;
            var typeSignatureKind2 = parser2.GetTypeSignatureKind(out data2);

            // If parser2 is a lookback type, we need to rewind parser1 to its startOffset1
            // before recursing.
            if (typeSignatureKind2 == TypeSignatureKind.Lookback)
            {
                NativeParser lookbackParser2 = parser2.GetLookbackParser(data2);
                parser1 = new NativeParser(parser1.Reader, startOffset1);
                return CompareTypeSigs(ref parser1, ref lookbackParser2);
            }

            if (typeSignatureKind1 != typeSignatureKind2)
                return false;

            switch (typeSignatureKind1)
            {
                case TypeSignatureKind.Lookback:
                    {
                        //  Recursion above better have removed all lookbacks
                        Debug.Assert(false, "Unexpected lookback type");
                        return false;
                    }

                case TypeSignatureKind.Modifier:
                    {
                        // Ensure the modifier kind (vector, pointer, byref) is the same
                        if (data1 != data2)
                            return false;
                        return CompareTypeSigs(ref parser1, ref parser2);
                    }

                case TypeSignatureKind.Variable:
                    {
                        // variable index is in data
                        if (data1 != data2)
                            return false;
                        break;
                    }

                case TypeSignatureKind.MultiDimArray:
                    {
                        // rank is in data
                        if (data1 != data2)
                            return false;

                        if (!CompareTypeSigs(ref parser1, ref parser2))
                            return false;

                        uint boundCount1 = parser1.GetUnsigned();
                        uint boundCount2 = parser2.GetUnsigned();
                        if (boundCount1 != boundCount2)
                            return false;

                        for (uint i = 0; i < boundCount1; i++)
                        {
                            if (parser1.GetUnsigned() != parser2.GetUnsigned())
                                return false;
                        }

                        uint lowerBoundCount1 = parser1.GetUnsigned();
                        uint lowerBoundCount2 = parser2.GetUnsigned();
                        if (lowerBoundCount1 != lowerBoundCount2)
                            return false;

                        for (uint i = 0; i < lowerBoundCount1; i++)
                        {
                            if (parser1.GetUnsigned() != parser2.GetUnsigned())
                                return false;
                        }
                        break;
                    }

                case TypeSignatureKind.FunctionPointer:
                    {
                        // callingConvention is in data
                        if (data1 != data2)
                            return false;
                        uint argCount1 = parser1.GetUnsigned();
                        uint argCount2 = parser2.GetUnsigned();
                        if (argCount1 != argCount2)
                            return false;
                        for (uint i = 0; i < argCount1; i++)
                        {
                            if (!CompareTypeSigs(ref parser1, ref parser2))
                                return false;
                        }
                        break;
                    }

                case TypeSignatureKind.Instantiation:
                    {
                        // Type parameter count is in data
                        if (data1 != data2)
                            return false;

                        if (!CompareTypeSigs(ref parser1, ref parser2))
                            return false;

                        for (uint i = 0; i < data1; i++)
                        {
                            if (!CompareTypeSigs(ref parser1, ref parser2))
                                return false;
                        }
                        break;
                    }

                case TypeSignatureKind.External:
                    {
                        RuntimeTypeHandle typeHandle1 = GetExternalTypeHandle(ref parser1, data1);
                        RuntimeTypeHandle typeHandle2 = GetExternalTypeHandle(ref parser2, data2);
                        if (!typeHandle1.Equals(typeHandle2))
                            return false;

                        break;
                    }

                default:
                    return false;
            }
            return true;
        }
        #endregion
    }
}
