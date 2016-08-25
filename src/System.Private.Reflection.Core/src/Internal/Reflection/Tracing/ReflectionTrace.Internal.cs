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
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.CustomAttributes;

using Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Tracing
{
    public static partial class ReflectionTrace
    {
        //==============================================================================
        // Returns the type name to emit into the ETW record.
        //
        //   - If it returns null, skip writing the ETW record. Null returns can happen 
        //     for the following reasons:
        //        - Missing metadata
        //        - Open type (no need to trace these - open type creations always succeed)
        //        - Third-party-implemented Types.
        //
        //     The implementation does a reasonable-effort to avoid MME's to avoid an annoying
        //     debugger experience. However, some MME's will still get caught by the try/catch.
        //
        //   - The format happens to match what the AssemblyQualifiedName property returns
        //     but we cannot invoke that here due to the risk of infinite recursion.
        //     The implementation must be very careful what it calls.
        //==============================================================================
        private static String NameString(this Type type)
        {
            try
            {
                return type.AssemblyQualifiedTypeName();
            }
            catch
            {
                return null;
            }
        }

        //==============================================================================
        // Returns the assembly name to emit into the ETW record.
        //==============================================================================
        private static String NameString(this Assembly assembly)
        {
            try
            {
                RuntimeAssembly runtimeAssembly = assembly as RuntimeAssembly;
                if (runtimeAssembly == null)
                    return null;
                return runtimeAssembly.Scope.Handle.ToRuntimeAssemblyName(runtimeAssembly.Scope.Reader).FullName;
            }
            catch
            {
                return null;
            }
        }

        //==============================================================================
        // Returns the custom attribute type name to emit into the ETW record.
        //==============================================================================
        private static String AttributeTypeNameString(this CustomAttributeData customAttributeData)
        {
            try
            {
                RuntimeCustomAttributeData runtimeCustomAttributeData = customAttributeData as RuntimeCustomAttributeData;
                if (runtimeCustomAttributeData == null)
                    return null;
                return runtimeCustomAttributeData.AttributeType.NameString();
            }
            catch
            {
                return null;
            }
        }

        //==============================================================================
        // Returns the declaring type name (without calling MemberInfo.DeclaringType) to emit into the ETW record.
        //==============================================================================
        private static String DeclaringTypeNameString(this MemberInfo memberInfo)
        {
            try
            {
                ITraceableTypeMember traceableTypeMember = memberInfo as ITraceableTypeMember;
                if (traceableTypeMember == null)
                    return null;
                return traceableTypeMember.ContainingType.NameString();
            }
            catch
            {
                return null;
            }
        }

        //==============================================================================
        // Returns the MemberInfo.Name value (without calling MemberInfo.Name) to emit into the ETW record.
        //==============================================================================
        private static String NameString(this MemberInfo memberInfo)
        {
            try
            {
                TypeInfo typeInfo = memberInfo as TypeInfo;
                if (typeInfo != null)
                    return typeInfo.AsType().NameString();

                ITraceableTypeMember traceableTypeMember = memberInfo as ITraceableTypeMember;
                if (traceableTypeMember == null)
                    return null;
                return traceableTypeMember.MemberName;
            }
            catch
            {
                return null;
            }
        }

        //==============================================================================
        // Append type argument strings.
        //==============================================================================
        private static String GenericTypeArgumentStrings(this Type[] typeArguments)
        {
            if (typeArguments == null)
                return null;
            String s = "";
            foreach (Type typeArgument in typeArguments)
            {
                String typeArgumentString = typeArgument.NameString();
                if (typeArgumentString == null)
                    return null;
                s += "@" + typeArgumentString;
            }
            return s;
        }

        private static String NonQualifiedTypeName(this Type type)
        {
            if (!type.IsRuntimeImplemented())
                return null;

            RuntimeTypeInfo runtimeType = type.CastToRuntimeTypeInfo();
            if (runtimeType.HasElementType)
            {
                String elementTypeName = runtimeType.InternalRuntimeElementType.NonQualifiedTypeName();
                if (elementTypeName == null)
                    return null;
                String suffix;
                if (runtimeType.IsArray)
                {
                    int rank = runtimeType.GetArrayRank();
                    if (rank == 1)
                        suffix = "[" + (runtimeType.InternalIsMultiDimArray ? "*" : "") + "]";
                    else
                        suffix = "[" + new String(',', rank - 1) + "]";
                }
                else if (runtimeType.IsByRef)
                    suffix = "&";
                else if (runtimeType.IsPointer)
                    suffix = "*";
                else
                    return null;

                return elementTypeName + suffix;
            }
            else if (runtimeType.IsGenericParameter)
            {
                return null;
            }
            else if (runtimeType.IsConstructedGenericType)
            {
                StringBuilder sb = new StringBuilder();
                String genericTypeDefinitionTypeName = runtimeType.GetGenericTypeDefinition().NonQualifiedTypeName();
                if (genericTypeDefinitionTypeName == null)
                    return null;
                sb.Append(genericTypeDefinitionTypeName);
                sb.Append("[");
                String sep = "";
                foreach (RuntimeTypeInfo ga in runtimeType.InternalRuntimeGenericTypeArguments)
                {
                    String gaTypeName = ga.AssemblyQualifiedTypeName();
                    if (gaTypeName == null)
                        return null;
                    sb.Append(sep + "[" + gaTypeName + "]");
                    sep = ",";
                }
                sb.Append("]");

                return sb.ToString();
            }
            else
            {
                RuntimeNamedTypeInfo runtimeNamedTypeInfo = type.GetTypeInfo() as RuntimeNamedTypeInfo;
                if (runtimeNamedTypeInfo == null)
                    return null;
                MetadataReader reader = runtimeNamedTypeInfo.Reader;

                String s = "";
                TypeDefinitionHandle typeDefinitionHandle = runtimeNamedTypeInfo.TypeDefinitionHandle;
                NamespaceDefinitionHandle namespaceDefinitionHandle;
                do
                {
                    TypeDefinition typeDefinition = typeDefinitionHandle.GetTypeDefinition(reader);
                    String name = typeDefinition.Name.GetString(reader);
                    if (s == "")
                        s = name;
                    else
                        s = name + "+" + s;
                    namespaceDefinitionHandle = typeDefinition.NamespaceDefinition;
                    typeDefinitionHandle = typeDefinition.EnclosingType;
                }
                while (!typeDefinitionHandle.IsNull(reader));

                NamespaceChain namespaceChain = new NamespaceChain(reader, namespaceDefinitionHandle);
                String ns = namespaceChain.NameSpace;
                if (ns != null)
                    s = ns + "." + s;
                return s;
            }
        }

        private static String AssemblyQualifiedTypeName(this Type type)
        {
            if (!type.IsRuntimeImplemented())
                return null;

            RuntimeTypeInfo runtimeType = type.CastToRuntimeTypeInfo();
            if (runtimeType == null)
                return null;
            String nonqualifiedTypeName = runtimeType.NonQualifiedTypeName();
            if (nonqualifiedTypeName == null)
                return null;
            String assemblyName = runtimeType.ContainingAssemblyName();
            if (assemblyName == null)
                return assemblyName;
            return nonqualifiedTypeName + ", " + assemblyName;
        }

        private static String ContainingAssemblyName(this Type type)
        {
            if (!type.IsRuntimeImplemented())
                return null;

            RuntimeTypeInfo runtimeTypeInfo = type.CastToRuntimeTypeInfo();
            if (runtimeTypeInfo is RuntimeNoMetadataNamedTypeInfo)
                return null;
            return runtimeTypeInfo.Assembly.NameString();
        }
    }
}

