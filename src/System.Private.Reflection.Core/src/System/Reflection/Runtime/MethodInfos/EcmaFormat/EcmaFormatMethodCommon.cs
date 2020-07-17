// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.EcmaFormat;
using System.Reflection.Runtime.ParameterInfos;
using System.Reflection.Runtime.ParameterInfos.EcmaFormat;
using System.Reflection.Runtime.CustomAttributes;
using System.Runtime;
using System.Runtime.InteropServices;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;
using Internal.Runtime.CompilerServices;
using Internal.Runtime.TypeLoader;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Runtime.MethodInfos.EcmaFormat
{
    // 
    // Implements methods and properties common to RuntimeMethodInfo and RuntimeConstructorInfo.
    //
    internal struct EcmaFormatMethodCommon : IRuntimeMethodCommon<EcmaFormatMethodCommon>, IEquatable<EcmaFormatMethodCommon>
    {
        public bool IsGenericMethodDefinition => GenericParameterCount != 0;

        public MethodInvoker GetUncachedMethodInvoker(RuntimeTypeInfo[] methodArguments, MemberInfo exceptionPertainant)
        {
            return ReflectionCoreExecution.ExecutionEnvironment.GetMethodInvoker(DeclaringType, new QMethodDefinition(Reader, MethodHandle), methodArguments, exceptionPertainant);
        }

        public QSignatureTypeHandle[] QualifiedMethodSignature
        {
            get
            {
                return this.MethodSignature;
            }
        }

        public EcmaFormatMethodCommon RuntimeMethodCommonOfUninstantiatedMethod
        {
            get
            {
                return new EcmaFormatMethodCommon(MethodHandle, _definingTypeInfo, _definingTypeInfo);
            }
        }

        public void FillInMetadataDescribedParameters(ref VirtualRuntimeParameterInfoArray result, QSignatureTypeHandle[] typeSignatures, MethodBase contextMethod, TypeContext typeContext)
        {
            foreach (ParameterHandle parameterHandle in _method.GetParameters())
            {
                Parameter parameterRecord = _reader.GetParameter(parameterHandle);
                int index = parameterRecord.SequenceNumber;
                result[index] =
                    EcmaFormatMethodParameterInfo.GetEcmaFormatMethodParameterInfo(
                        contextMethod,
                        _methodHandle,
                        index - 1,
                        parameterHandle,
                        typeSignatures[index],
                        typeContext);
            }
        }

        public int GenericParameterCount => _method.GetGenericParameters().Count;

        public RuntimeTypeInfo[] GetGenericTypeParametersWithSpecifiedOwningMethod(RuntimeNamedMethodInfo<EcmaFormatMethodCommon> owningMethod)
        {
            GenericParameterHandleCollection genericParameters = _method.GetGenericParameters();
            int genericParametersCount = genericParameters.Count;
            if (genericParametersCount == 0)
                return Array.Empty<RuntimeTypeInfo>();

            RuntimeTypeInfo[] genericTypeParameters = new RuntimeTypeInfo[genericParametersCount];
            int i = 0;
            foreach (GenericParameterHandle genericParameterHandle in genericParameters)
            {
                RuntimeTypeInfo genericParameterType = EcmaFormatRuntimeGenericParameterTypeInfoForMethods.GetRuntimeGenericParameterTypeInfoForMethods(owningMethod, Reader, genericParameterHandle);
                genericTypeParameters[i++] = genericParameterType;
            }
            return genericTypeParameters;
        }

        //
        // methodHandle    - the "tkMethodDef" that identifies the method.
        // definingType   - the "tkTypeDef" that defined the method (this is where you get the metadata reader that created methodHandle.)
        // contextType    - the type that supplies the type context (i.e. substitutions for generic parameters.) Though you
        //                  get your raw information from "definingType", you report "contextType" as your DeclaringType property.
        //
        //  For example:
        //
        //       typeof(Foo<>).GetTypeInfo().DeclaredMembers
        //
        //           The definingType and contextType are both Foo<>
        //
        //       typeof(Foo<int,String>).GetTypeInfo().DeclaredMembers
        //
        //          The definingType is "Foo<,>"
        //          The contextType is "Foo<int,String>"
        //
        //  We don't report any DeclaredMembers for arrays or generic parameters so those don't apply.
        //
        public EcmaFormatMethodCommon(MethodDefinitionHandle methodHandle, EcmaFormatRuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            _definingTypeInfo = definingTypeInfo;
            _methodHandle = methodHandle;
            _contextTypeInfo = contextTypeInfo;
            _reader = definingTypeInfo.Reader;
            _method = _reader.GetMethodDefinition(methodHandle);
        }

        public MethodAttributes Attributes
        {
            get
            {
                return _method.Attributes;
            }
        }

        public CallingConventions CallingConvention
        {
            get
            {
                BlobReader signatureBlob = _reader.GetBlobReader(_method.Signature);
                CallingConventions result;
                SignatureHeader sigHeader = signatureBlob.ReadSignatureHeader();

                if (sigHeader.CallingConvention == SignatureCallingConvention.VarArgs)
                    result = CallingConventions.VarArgs;
                else
                    result = CallingConventions.Standard;

                if (sigHeader.IsInstance)
                    result |= CallingConventions.HasThis;
                
                if (sigHeader.HasExplicitThis)
                    result |= CallingConventions.ExplicitThis;

                return result;
            }
        }

        public RuntimeTypeInfo ContextTypeInfo
        {
            get
            {
                return _contextTypeInfo;
            }
        }

        public RuntimeTypeInfo DeclaringType
        {
            get
            {
                return _contextTypeInfo;
            }
        }

        public RuntimeNamedTypeInfo DefiningTypeInfo
        {
            get
            {
                return _definingTypeInfo;
            }
        }

        public MethodImplAttributes MethodImplementationFlags
        {
            get
            {
                return _method.ImplAttributes;
            }
        }

        public Module Module
        {
            get
            {
                return _definingTypeInfo.Module;
            }
        }

        public int MetadataToken
        {
            get
            {
                return MetadataTokens.GetToken(_methodHandle);
            }
        }

        public RuntimeMethodHandle GetRuntimeMethodHandle(Type[] genericArgs)
        {
            Debug.Assert(genericArgs == null || genericArgs.Length > 0);

            RuntimeTypeHandle[] genericArgHandles;
            if (genericArgs != null)
            {
                genericArgHandles = new RuntimeTypeHandle[genericArgs.Length];
                for (int i = 0; i < genericArgHandles.Length; i++)
                    genericArgHandles[i] = genericArgs[i].TypeHandle;
            }
            else
            {
                genericArgHandles = null;
            }

            IntPtr dynamicModule = ModuleList.Instance.GetModuleInfoForMetadataReader(Reader).DynamicModulePtrAsIntPtr;

            return TypeLoaderEnvironment.Instance.GetRuntimeMethodHandleForComponents(
                DeclaringType.TypeHandle,
                Name,
                RuntimeSignature.CreateFromMethodHandle(dynamicModule, MetadataToken),
                genericArgHandles);
        }

        //
        // Returns the ParameterInfo objects for the method parameters and return parameter.
        //
        // The ParameterInfo objects will report "contextMethod" as their Member property and use it to get type variable information from
        // the contextMethod's declaring type. The actual metadata, however, comes from "this."
        //
        // The methodTypeArguments provides the fill-ins for any method type variable elements in the parameter type signatures.
        //
        // Does not array-copy.
        //
        public RuntimeParameterInfo[] GetRuntimeParameters(MethodBase contextMethod, RuntimeTypeInfo[] methodTypeArguments, out RuntimeParameterInfo returnParameter)
        {
            MetadataReader reader = _reader;
            TypeContext typeContext = contextMethod.DeclaringType.CastToRuntimeTypeInfo().TypeContext;
            typeContext = new TypeContext(typeContext.GenericTypeArguments, methodTypeArguments);
            QSignatureTypeHandle[] typeSignatures = this.MethodSignature;
            int count = typeSignatures.Length;

            VirtualRuntimeParameterInfoArray result = new VirtualRuntimeParameterInfoArray(count);
            foreach (ParameterHandle parameterHandle in _method.GetParameters())
            {
                Parameter parameterRecord = _reader.GetParameter(parameterHandle);
                int index = parameterRecord.SequenceNumber;
                result[index] =
                    EcmaFormatMethodParameterInfo.GetEcmaFormatMethodParameterInfo(
                        contextMethod,
                        _methodHandle,
                        index - 1,
                        parameterHandle,
                        typeSignatures[index],
                        typeContext);
            }
            for (int i = 0; i < count; i++)
            {
                if (result[i] == null)
                {
                    result[i] =
                        RuntimeThinMethodParameterInfo.GetRuntimeThinMethodParameterInfo(
                            contextMethod,
                            i - 1,
                            typeSignatures[i],
                            typeContext);
                }
            }

            returnParameter = result.First;
            return result.Remainder;
        }

        public String Name
        {
            get
            {
                return _method.Name.GetString(_reader);
            }
        }

        public MetadataReader Reader
        {
            get
            {
                return _reader;
            }
        }

        public MethodDefinitionHandle MethodHandle
        {
            get
            {
                return _methodHandle;
            }
        }

        public bool HasSameMetadataDefinitionAs(EcmaFormatMethodCommon other)
        {
            if (!(_reader == other._reader))
                return false;
            if (!(_methodHandle.Equals(other._methodHandle)))
                return false;
            return true;
        }

        public IEnumerable<CustomAttributeData> TrueCustomAttributes => RuntimeCustomAttributeData.GetCustomAttributes(_reader, _method.GetCustomAttributes());

        public override bool Equals(Object obj)
        {
            if (!(obj is EcmaFormatMethodCommon other))
                return false;
            return Equals(other);
        }

        public bool Equals(EcmaFormatMethodCommon other)
        {
            if (!(_reader == other._reader))
                return false;
            if (!(_methodHandle.Equals(other._methodHandle)))
                return false;
            if (!(_contextTypeInfo.Equals(other._contextTypeInfo)))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            return _methodHandle.GetHashCode() ^ _contextTypeInfo.GetHashCode();
        }

        private QSignatureTypeHandle[] MethodSignature
        {
            get
            {
                BlobReader signatureBlob =  _reader.GetBlobReader(_method.Signature);
                SignatureHeader header = signatureBlob.ReadSignatureHeader();
                if (header.Kind != SignatureKind.Method)
                    throw new BadImageFormatException();
                
                int genericParameterCount = 0;
                if (header.IsGeneric)
                    genericParameterCount = signatureBlob.ReadCompressedInteger();

                int numParameters = signatureBlob.ReadCompressedInteger();
                QSignatureTypeHandle[] signatureHandles = new QSignatureTypeHandle[checked(numParameters + 1)];
                signatureHandles[0] = new QSignatureTypeHandle(_reader, signatureBlob);
                EcmaMetadataHelpers.SkipType(ref signatureBlob);
                for (int i = 0 ; i < numParameters; i++)
                {
                    signatureHandles[i + 1] = new QSignatureTypeHandle(_reader, signatureBlob);
                    EcmaMetadataHelpers.SkipType(ref signatureBlob);
                }

                return signatureHandles;
            }
        }

        private readonly EcmaFormatRuntimeNamedTypeInfo _definingTypeInfo;
        private readonly MethodDefinitionHandle _methodHandle;
        private readonly RuntimeTypeInfo _contextTypeInfo;

        private readonly MetadataReader _reader;

        private readonly MethodDefinition _method;
    }
}
