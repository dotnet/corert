// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.ParameterInfos;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.MethodInfos
{
    // 
    // Implements methods and properties common to RuntimeMethodInfo and RuntimeConstructorInfo. In a sensible world, this
    // struct would be a common base class for RuntimeMethodInfo and RuntimeConstructorInfo. But those types are forced
    // to derive from MethodInfo and ConstructorInfo because of the way the Reflection API are designed. Hence,
    // we use containment as a substitute.
    //
    internal struct RuntimeMethodCommon
    {
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
        public RuntimeMethodCommon(MethodHandle methodHandle, RuntimeNamedTypeInfo definingTypeInfo, RuntimeTypeInfo contextTypeInfo)
        {
            _definingTypeInfo = definingTypeInfo;
            _methodHandle = methodHandle;
            _contextTypeInfo = contextTypeInfo;
            _reader = definingTypeInfo.Reader;
            _method = methodHandle.GetMethod(_reader);
        }

        public MethodAttributes Attributes
        {
            get
            {
                return _method.Flags;
            }
        }

        public CallingConventions CallingConvention
        {
            get
            {
                return MethodSignature.CallingConvention;
            }
        }

        // Compute the ToString() value in a pay-to-play-safe way.
        public String ComputeToString(MethodBase contextMethod, RuntimeTypeInfo[] methodTypeArguments)
        {
            RuntimeParameterInfo returnParameter;
            RuntimeParameterInfo[] parameters = this.GetRuntimeParameters(contextMethod, methodTypeArguments, out returnParameter);
            return ComputeToString(contextMethod, methodTypeArguments, parameters, returnParameter);
        }

        public static String ComputeToString(MethodBase contextMethod, RuntimeTypeInfo[] methodTypeArguments, RuntimeParameterInfo[] parameters, RuntimeParameterInfo returnParameter)
        {
            StringBuilder sb = new StringBuilder(30);
            sb.Append(returnParameter == null ? "Void" : returnParameter.ParameterTypeString);  // ConstructorInfos allowed to pass in null rather than craft a ReturnParameterInfo that's always of type void.
            sb.Append(' ');
            sb.Append(contextMethod.Name);
            if (methodTypeArguments.Length != 0)
            {
                String sep = "";
                sb.Append('[');
                foreach (RuntimeTypeInfo methodTypeArgument in methodTypeArguments)
                {
                    sb.Append(sep);
                    sep = ",";
                    String name = methodTypeArgument.InternalNameIfAvailable;
                    if (name == null)
                        name = ToStringUtils.UnavailableType;
                    sb.Append(methodTypeArgument.Name);
                }
                sb.Append(']');
            }
            sb.Append('(');
            sb.Append(ComputeParametersString(parameters));
            sb.Append(')');

            return sb.ToString();
        }

        // Used by method and property ToString() methods to display the list of parameter types. Replicates the behavior of MethodBase.ConstructParameters()
        // but in a pay-to-play-safe way.
        public static String ComputeParametersString(RuntimeParameterInfo[] parameters)
        {
            StringBuilder sb = new StringBuilder(30);
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i != 0)
                    sb.Append(", ");
                String parameterTypeString = parameters[i].ParameterTypeString;

                // Legacy: Why use "ByRef" for by ref parameters? What language is this? 
                // VB uses "ByRef" but it should precede (not follow) the parameter name.
                // Why don't we just use "&"?
                if (parameterTypeString.EndsWith("&"))
                    parameterTypeString = parameterTypeString.Substring(0, parameterTypeString.Length - 1) + " ByRef";
                sb.Append(parameterTypeString);
            }
            return sb.ToString();
        }

        public RuntimeTypeInfo ContextTypeInfo
        {
            get
            {
                return _contextTypeInfo;
            }
        }

        public IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                IEnumerable<CustomAttributeData> customAttributes = RuntimeCustomAttributeData.GetCustomAttributes(_reader, _method.CustomAttributes);
                foreach (CustomAttributeData cad in customAttributes)
                    yield return cad;
                foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _methodHandle, _definingTypeInfo.TypeDefinitionHandle))
                    yield return cad;
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
                return _method.ImplFlags;
            }
        }

        public Module Module
        {
            get
            {
                return _definingTypeInfo.Module;
            }
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
            MethodSignature methodSignature = this.MethodSignature;
            Handle[] typeSignatures = new Handle[methodSignature.Parameters.Count + 1];
            typeSignatures[0] = methodSignature.ReturnType;
            int paramIndex = 1;
            foreach (Handle parameterTypeSignatureHandle in methodSignature.Parameters)
            {
                typeSignatures[paramIndex++] = parameterTypeSignatureHandle;
            }
            int count = typeSignatures.Length;

            VirtualRuntimeParameterInfoArray result = new VirtualRuntimeParameterInfoArray(count);
            foreach (ParameterHandle parameterHandle in _method.Parameters)
            {
                Parameter parameterRecord = parameterHandle.GetParameter(_reader);
                int index = parameterRecord.Sequence;
                result[index] =
                    RuntimeFatMethodParameterInfo.GetRuntimeFatMethodParameterInfo(
                        contextMethod,
                        _methodHandle,
                        index - 1,
                        parameterHandle,
                        reader,
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
                        reader,
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

        public MethodHandle MethodHandle
        {
            get
            {
                return _methodHandle;
            }
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is RuntimeMethodCommon))
                return false;
            return Equals((RuntimeMethodCommon)obj);
        }

        public bool Equals(RuntimeMethodCommon other)
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

        private MethodSignature MethodSignature
        {
            get
            {
                return _method.Signature.GetMethodSignature(_reader);
            }
        }

        private readonly RuntimeNamedTypeInfo _definingTypeInfo;
        private readonly MethodHandle _methodHandle;
        private readonly RuntimeTypeInfo _contextTypeInfo;

        private readonly MetadataReader _reader;

        private readonly Method _method;

        // Helper for GetRuntimeParameters() - array mimic that supports an efficient "array.Skip(1).ToArray()" operation.
        private struct VirtualRuntimeParameterInfoArray
        {
            public VirtualRuntimeParameterInfoArray(int count)
                : this()
            {
                Debug.Assert(count >= 1);
                Remainder = (count == 1) ? Array.Empty<RuntimeParameterInfo>() : new RuntimeParameterInfo[count - 1];
            }

            public RuntimeParameterInfo this[int index]
            {
                get
                {
                    return index == 0 ? First : Remainder[index - 1];
                }
                
                set
                {
                    if (index == 0)
                        First = value;
                    else
                        Remainder[index - 1] = value;
                }
            }

            public RuntimeParameterInfo First { get; private set; }
            public RuntimeParameterInfo[] Remainder { get; }
        }
    }
}
