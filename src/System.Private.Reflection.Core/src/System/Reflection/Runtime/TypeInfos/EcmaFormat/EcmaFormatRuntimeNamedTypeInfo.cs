// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.Assemblies.EcmaFormat;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.CustomAttributes;
using System.Runtime.InteropServices;

using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Runtime.TypeInfos.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeNamedTypeInfo : RuntimeNamedTypeInfo
    {
        private EcmaFormatRuntimeNamedTypeInfo(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle, RuntimeTypeHandle typeHandle) :
            base(typeHandle)
        {
            _reader = reader;
            _typeDefinitionHandle = typeDefinitionHandle;
            _typeDefinition = reader.GetTypeDefinition(_typeDefinitionHandle);
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return EcmaFormatRuntimeAssembly.GetRuntimeAssembly(_reader);
            }
        }

        protected sealed override Guid? ComputeGuidFromCustomAttributes()
        {
            //
            // Look for a [Guid] attribute. If found, return that.
            // 
            foreach (CustomAttributeHandle cah in _typeDefinition.GetCustomAttributes())
            {
                // We can't reference the GuidAttribute class directly as we don't have an official dependency on System.Runtime.InteropServices.
                // Following age-old CLR tradition, we search for the custom attribute using a name-based search. Since this makes it harder
                // to be sure we won't run into custom attribute constructors that comply with the GuidAttribute(String) signature, 
                // we'll check that it does and silently skip the CA if it doesn't match the expected pattern.
                CustomAttribute attribute = Reader.GetCustomAttribute(cah);
                EntityHandle ctorType;
                EcmaMetadataHelpers.GetAttributeTypeDefRefOrSpecHandle(_reader, attribute.Constructor, out ctorType);
                StringHandle typeNameHandle;
                StringHandle typeNamespaceHandle;
                if (EcmaMetadataHelpers.GetAttributeNamespaceAndName(Reader, ctorType, out typeNamespaceHandle, out typeNameHandle))
                {
                    MetadataStringComparer stringComparer = Reader.StringComparer;
                    if (stringComparer.Equals(typeNamespaceHandle, "System.Runtime.InteropServices"))
                    {
                        if (stringComparer.Equals(typeNameHandle, "GuidAttribute"))
                        {
                            ReflectionTypeProvider typeProvider = new ReflectionTypeProvider(throwOnError: false);

                            CustomAttributeValue<RuntimeTypeInfo> customAttributeValue = attribute.DecodeValue(typeProvider);
                            if (customAttributeValue.FixedArguments.Length != 1)
                                continue;
                            
                            CustomAttributeTypedArgument<RuntimeTypeInfo> firstArg = customAttributeValue.FixedArguments[0];
                            if (firstArg.Value == null)
                                continue;

                            if (!(firstArg.Value is string guidString))
                                continue;

                            return new Guid(guidString);
                        }
                    }
                }
            }

            return null;
        }

        protected sealed override void GetPackSizeAndSize(out int packSize, out int size)
        {
            TypeLayout layout = _typeDefinition.GetLayout();
            packSize = layout.PackingSize;
            size = unchecked((int)(layout.Size));
        }

        public sealed override bool IsGenericTypeDefinition
        {
            get
            {
                return _typeDefinition.GetGenericParameters().Count != 0;
            }
        }

        public sealed override string Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_Namespace(this);
#endif

                return _reader.GetString(_typeDefinition.Namespace).EscapeTypeNameIdentifier();
            }
        }

        public sealed override Type GetGenericTypeDefinition()
        {
            if (IsGenericTypeDefinition)
                return this;
            return base.GetGenericTypeDefinition();
        }

        public sealed override int MetadataToken
        {
            get
            {
                return MetadataTokens.GetToken(_typeDefinitionHandle);
            }
        }

        public sealed override string ToString()
        {
            StringBuilder sb = null;

            foreach (GenericParameterHandle genericParameterHandle in _typeDefinition.GetGenericParameters())
            {
                if (sb == null)
                {
                    sb = new StringBuilder(FullName);
                    sb.Append('[');
                }
                else
                {
                    sb.Append(',');
                }

                GenericParameter genericParameter = _reader.GetGenericParameter(genericParameterHandle);
                sb.Append(genericParameter.Name.GetString(_reader));
            }

            if (sb == null)
            {
                return FullName;
            }
            else
            {
                return sb.Append(']').ToString();
            }
        }

        protected sealed override TypeAttributes GetAttributeFlagsImpl()
        {
            TypeAttributes attr = _typeDefinition.Attributes;
            return attr;
        }

        protected sealed override int InternalGetHashCode()
        {
            return _typeDefinitionHandle.GetHashCode();
        }

        internal sealed override Type InternalDeclaringType
        {
            get
            {
                RuntimeTypeInfo declaringType = null;
                if (EcmaMetadataHelpers.IsNested(_typeDefinition.Attributes))
                {
                    TypeDefinitionHandle enclosingTypeDefHandle = _typeDefinition.GetDeclaringType();
                    declaringType = enclosingTypeDefHandle.ResolveTypeDefinition(_reader);
                }
                return declaringType;
            }
        }

        internal sealed override string InternalFullNameOfAssembly
        {
            get
            {
                return Assembly.FullName;
            }
        }

        public sealed override string InternalGetNameIfAvailable(ref Type rootCauseForFailure)
        {
            string name = _typeDefinition.Name.GetString(_reader);
            return name.EscapeTypeNameIdentifier();
        }

        protected sealed override IEnumerable<CustomAttributeData> TrueCustomAttributes => RuntimeCustomAttributeData.GetCustomAttributes(_reader, _typeDefinition.GetCustomAttributes());

        internal sealed override RuntimeTypeInfo[] RuntimeGenericTypeParameters
        {
            get
            {
                LowLevelList<RuntimeTypeInfo> genericTypeParameters = new LowLevelList<RuntimeTypeInfo>();

                foreach (GenericParameterHandle genericParameterHandle in _typeDefinition.GetGenericParameters())
                {
                    RuntimeTypeInfo genericParameterType = EcmaFormatRuntimeGenericParameterTypeInfoForTypes.GetRuntimeGenericParameterTypeInfoForTypes(this, genericParameterHandle);
                    genericTypeParameters.Add(genericParameterType);
                }

                return genericTypeParameters.ToArray();
            }
        }

        //
        // Returns the base type as a typeDef, Ref, or Spec. Default behavior is to QTypeDefRefOrSpec.Null, which causes BaseType to return null.
        //
        internal sealed override QTypeDefRefOrSpec TypeRefDefOrSpecForBaseType
        {
            get
            {
                Handle baseType = _typeDefinition.BaseType;
                if (baseType.IsNil)
                    return QTypeDefRefOrSpec.Null;
                return new QTypeDefRefOrSpec(_reader, baseType);
            }
        }

        //
        // Returns the *directly implemented* interfaces as typedefs, specs or refs. ImplementedInterfaces will take care of the transitive closure and
        // insertion of the TypeContext.
        //
        internal sealed override QTypeDefRefOrSpec[] TypeRefDefOrSpecsForDirectlyImplementedInterfaces
        {
            get
            {
                LowLevelList<QTypeDefRefOrSpec> directlyImplementedInterfaces = new LowLevelList<QTypeDefRefOrSpec>();
                foreach (InterfaceImplementationHandle ifcHandle in _typeDefinition.GetInterfaceImplementations())
                {
                    InterfaceImplementation interfaceImp = _reader.GetInterfaceImplementation(ifcHandle);
                    directlyImplementedInterfaces.Add(new QTypeDefRefOrSpec(_reader, interfaceImp.Interface));
                }
                return directlyImplementedInterfaces.ToArray();
            }
        }

        internal MetadataReader Reader
        {
            get
            {
                return _reader;
            }
        }

        internal TypeDefinitionHandle TypeDefinitionHandle
        {
            get
            {
                return _typeDefinitionHandle;
            }
        }

        internal EventDefinitionHandleCollection DeclaredEventHandles
        {
            get
            {
                return _typeDefinition.GetEvents();
            }
        }

        internal FieldDefinitionHandleCollection DeclaredFieldHandles
        {
            get
            {
                return _typeDefinition.GetFields();
            }
        }

        internal MethodDefinitionHandleCollection DeclaredMethodAndConstructorHandles
        {
            get
            {
                return _typeDefinition.GetMethods();
            }
        }

        internal PropertyDefinitionHandleCollection DeclaredPropertyHandles
        {
            get
            {
                return _typeDefinition.GetProperties();
            }
        }

        public bool Equals(EcmaFormatRuntimeNamedTypeInfo other)
        {
            // RuntimeTypeInfo.Equals(object) is the one that encapsulates our unification strategy so defer to him.
            object otherAsObject = other;
            return base.Equals(otherAsObject);
        }

#if ENABLE_REFLECTION_TRACE
        internal sealed override string TraceableTypeName
        {
            get
            {
                MetadataReader reader = Reader;

                String s = "";
                TypeDefinitionHandle typeDefinitionHandle = TypeDefinitionHandle;
                do
                {
                    TypeDefinition typeDefinition = reader.GetTypeDefinition(typeDefinitionHandle);
                    String name = typeDefinition.Name.GetString(reader);
                    if (s == "")
                        s = name;
                    else
                        s = name + "+" + s;
                    typeDefinitionHandle = typeDefinition.GetDeclaringType();
                }
                while (!typeDefinitionHandle.IsNil);

                if (!_typeDefinition.Namespace.IsNil)
                {
                    s = _typeDefinition.Namespace.GetString(reader) + "." + s;
                }
                return s;
            }
        }
#endif

        internal sealed override QTypeDefRefOrSpec TypeDefinitionQHandle
        {
            get
            {
                return new QTypeDefRefOrSpec(_reader, _typeDefinitionHandle, true);
            }
        }

        private readonly MetadataReader _reader;
        private readonly TypeDefinitionHandle _typeDefinitionHandle;
        private readonly TypeDefinition _typeDefinition;
    }
}
