// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.Assemblies;
using System.Reflection.Runtime.CustomAttributes;

using Internal.LowLevelLinq;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;

using CharSet = System.Runtime.InteropServices.CharSet;
using LayoutKind = System.Runtime.InteropServices.LayoutKind;
using StructLayoutAttribute = System.Runtime.InteropServices.StructLayoutAttribute;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // TypeInfos that represent type definitions (i.e. Foo or Foo<>, but not Foo<int> or arrays/pointers/byrefs.)
    // 
    //
    internal sealed partial class RuntimeNamedTypeInfo : RuntimeTypeInfo, IEquatable<RuntimeNamedTypeInfo>
    {
        private RuntimeNamedTypeInfo(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle, RuntimeTypeHandle typeHandle)
        {
            _reader = reader;
            _typeDefinitionHandle = typeDefinitionHandle;
            _typeDefinition = _typeDefinitionHandle.GetTypeDefinition(reader);
            _typeHandle = typeHandle;
        }

        public sealed override Assembly Assembly
        {
            get
            {
                // If an assembly is split across multiple metadata blobs then the defining scope may
                // not be the canonical scope representing the assembly. We need to look up the assembly
                // by name to ensure we get the right one.

                ScopeDefinitionHandle scopeDefinitionHandle = NamespaceChain.DefiningScope;
                RuntimeAssemblyName runtimeAssemblyName = scopeDefinitionHandle.ToRuntimeAssemblyName(_reader);

                return RuntimeAssembly.GetRuntimeAssembly(runtimeAssemblyName);
            }
        }

        public sealed override bool ContainsGenericParameters
        {
            get
            {
                return IsGenericTypeDefinition;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_CustomAttributes(this);
#endif

                IEnumerable<CustomAttributeData> customAttributes = RuntimeCustomAttributeData.GetCustomAttributes(_reader, _typeDefinition.CustomAttributes);
                foreach (CustomAttributeData cad in customAttributes)
                    yield return cad;
                foreach (CustomAttributeData cad in ReflectionCoreExecution.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _typeDefinitionHandle))
                {
                    yield return cad;
                }
            }
        }

        public sealed override IEnumerable<TypeInfo> DeclaredNestedTypes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaredNestedTypes(this);
#endif

                foreach (TypeDefinitionHandle nestedTypeHandle in _typeDefinition.NestedTypes)
                {
                    yield return nestedTypeHandle.GetNamedType(_reader);
                }
            }
        }

        public bool Equals(RuntimeNamedTypeInfo other)
        {
            // RuntimeTypeInfo.Equals(object) is the one that encapsulates our unification strategy so defer to him.
            object otherAsObject = other;
            return base.Equals(otherAsObject);
        }

        public sealed override Guid GUID
        {
            get
            {
                //
                // Look for a [Guid] attribute. If found, return that.
                // 
                foreach (CustomAttributeHandle cah in _typeDefinition.CustomAttributes)
                {
                    // We can't reference the GuidAttribute class directly as we don't have an official dependency on System.Runtime.InteropServices.
                    // Following age-old CLR tradition, we search for the custom attribute using a name-based search. Since this makes it harder
                    // to be sure we won't run into custom attribute constructors that comply with the GuidAttribute(String) signature, 
                    // we'll check that it does and silently skip the CA if it doesn't match the expected pattern.
                    if (cah.IsCustomAttributeOfType(_reader, "System.Runtime.InteropServices", "GuidAttribute"))
                    {
                        CustomAttribute ca = cah.GetCustomAttribute(_reader);
                        IEnumerator<FixedArgumentHandle> fahEnumerator = ca.FixedArguments.GetEnumerator();
                        if (!fahEnumerator.MoveNext())
                            continue;
                        FixedArgumentHandle guidStringArgumentHandle = fahEnumerator.Current;
                        if (fahEnumerator.MoveNext())
                            continue;
                        FixedArgument guidStringArgument = guidStringArgumentHandle.GetFixedArgument(_reader);
                        String guidString = guidStringArgument.Value.ParseConstantValue(_reader) as String;
                        if (guidString == null)
                            continue;
                        return new Guid(guidString);
                    }
                }

                //
                // If we got here, there was no [Guid] attribute.
                //
                // Ideally, we'd now compute the same GUID the desktop returns - however, that algorithm is complex and has questionable dependencies
                // (in particular, the GUID changes if the language compilers ever change the way it emits metadata tokens into certain unordered lists.
                // We don't even retain that order across the Project N toolchain.)
                //
                // For now, this is a compromise that satisfies our app-compat goals. We ensure that each unique Type receives a different GUID (at least one app
                // uses the GUID as a dictionary key to look up types.) It will not be the same GUID on multiple runs of the app but so far, there's
                // no evidence that's needed.
                //
                return s_namedTypeToGuidTable.GetOrAdd(this).Item1;
            }
        }

        public sealed override bool IsGenericTypeDefinition
        {
            get
            {
                return _typeDefinition.GenericParameters.GetEnumerator().MoveNext();
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

                return NamespaceChain.NameSpace.EscapeTypeNameIdentifier();
            }
        }

        public sealed override string FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_FullName(this);
#endif

                Debug.Assert(!IsConstructedGenericType);
                Debug.Assert(!IsGenericParameter);
                Debug.Assert(!HasElementType);

                string name = Name;

                Type declaringType = this.DeclaringType;
                if (declaringType != null)
                {
                    string declaringTypeFullName = declaringType.FullName;
                    return declaringTypeFullName + "+" + name;
                }

                string ns = Namespace;
                if (ns == null)
                    return name;
                return ns + "." + name;
            }
        }

        public sealed override Type GetGenericTypeDefinition()
        {
            if (_typeDefinition.GenericParameters.GetEnumerator().MoveNext())
                return this.AsType();
            return base.GetGenericTypeDefinition();
        }

        public sealed override StructLayoutAttribute StructLayoutAttribute
        {
            get
            {
                const int DefaultPackingSize = 8;

                // Note: CoreClr checks HasElementType and IsGenericParameter in addition to IsInterface but those properties cannot be true here as this
                // RuntimeTypeInfo subclass is solely for TypeDef types.)
                if (IsInterface)
                    return null;

                TypeAttributes attributes = Attributes;

                LayoutKind layoutKind;
                switch (attributes & TypeAttributes.LayoutMask)
                {
                    case TypeAttributes.ExplicitLayout: layoutKind = LayoutKind.Explicit; break;
                    case TypeAttributes.AutoLayout: layoutKind = LayoutKind.Auto; break;
                    case TypeAttributes.SequentialLayout: layoutKind = LayoutKind.Sequential; break;
                    default: layoutKind = LayoutKind.Auto;  break;
                }

                CharSet charSet;
                switch (attributes & TypeAttributes.StringFormatMask)
                {
                    case TypeAttributes.AnsiClass: charSet = CharSet.Ansi; break;
                    case TypeAttributes.AutoClass: charSet = CharSet.Auto; break;
                    case TypeAttributes.UnicodeClass: charSet = CharSet.Unicode; break;
                    default: charSet = CharSet.None;  break;
                }

                int pack = _typeDefinition.PackingSize;
                int size = unchecked((int)(_typeDefinition.Size));

                // Metadata parameter checking should not have allowed 0 for packing size.
                // The runtime later converts a packing size of 0 to 8 so do the same here
                // because it's more useful from a user perspective. 
                if (pack == 0)
                    pack = DefaultPackingSize;

                return new StructLayoutAttribute(layoutKind)
                {
                    CharSet = charSet,
                    Pack = pack,
                    Size = size,
                };
            }
        }

        public sealed override string ToString()
        {
            StringBuilder sb = null;

            foreach (GenericParameterHandle genericParameterHandle in _typeDefinition.GenericParameters)
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

                sb.Append(genericParameterHandle.GetGenericParameter(_reader).Name.GetString(_reader));
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
            TypeAttributes attr = _typeDefinition.Flags;
            return attr;
        }

        protected sealed override int InternalGetHashCode()
        {
            return _typeDefinitionHandle.GetHashCode();
        }

        //
        // Returns the anchoring typedef that declares the members that this type wants returned by the Declared*** properties.
        // The Declared*** properties will project the anchoring typedef's members by overriding their DeclaringType property with "this"
        // and substituting the value of this.TypeContext into any generic parameters.
        //
        // Default implementation returns null which causes the Declared*** properties to return no members.
        //
        // Note that this does not apply to DeclaredNestedTypes. Nested types and their containers have completely separate generic instantiation environments
        // (despite what C# might lead you to think.) Constructed generic types return the exact same same nested types that its generic type definition does
        // - i.e. their DeclaringTypes refer back to the generic type definition, not the constructed generic type.)
        //
        // Note also that we cannot use this anchoring concept for base types because of generic parameters. Generic parameters return
        // baseclass and interfaces based on its constraints.
        //
        internal sealed override RuntimeNamedTypeInfo AnchoringTypeDefinitionForDeclaredMembers
        {
            get
            {
                return this;
            }
        }

        internal sealed override Type InternalDeclaringType
        {
            get
            {
                RuntimeTypeInfo declaringType = null;
                TypeDefinitionHandle enclosingTypeDefHandle = _typeDefinition.EnclosingType;
                if (!enclosingTypeDefHandle.IsNull(_reader))
                {
                    declaringType = enclosingTypeDefHandle.ResolveTypeDefinition(_reader);
                }
                return declaringType;
            }
        }

        internal sealed override string InternalFullNameOfAssembly
        {
            get
            {
                NamespaceChain namespaceChain = NamespaceChain;
                ScopeDefinitionHandle scopeDefinitionHandle = namespaceChain.DefiningScope;
                return scopeDefinitionHandle.ToRuntimeAssemblyName(_reader).FullName;
            }
        }

        internal sealed override string InternalGetNameIfAvailable(ref Type rootCauseForFailure)
        {
            ConstantStringValueHandle nameHandle = _typeDefinition.Name;
            string name = nameHandle.GetString(_reader);

            return name.EscapeTypeNameIdentifier();
        }

        internal sealed override RuntimeTypeHandle InternalTypeHandleIfAvailable
        {
            get
            {
                return _typeHandle;
            }
        }

        internal sealed override RuntimeTypeInfo[] RuntimeGenericTypeParameters
        {
            get
            {
                LowLevelList<RuntimeTypeInfo> genericTypeParameters = new LowLevelList<RuntimeTypeInfo>();

                foreach (GenericParameterHandle genericParameterHandle in _typeDefinition.GenericParameters)
                {
                    RuntimeTypeInfo genericParameterType = RuntimeGenericParameterTypeInfoForTypes.GetRuntimeGenericParameterTypeInfoForTypes(this, genericParameterHandle);
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
                if (baseType.IsNull(_reader))
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
                foreach (Handle ifcHandle in _typeDefinition.Interfaces)
                    directlyImplementedInterfaces.Add(new QTypeDefRefOrSpec(_reader, ifcHandle));
                return directlyImplementedInterfaces.ToArray();
            }
        }

        //
        // Returns the generic parameter substitutions to use when enumerating declared members, base class and implemented interfaces.
        //
        internal sealed override TypeContext TypeContext
        {
            get
            {
                return new TypeContext(this.RuntimeGenericTypeParameters, null);
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

        internal IEnumerable<MethodHandle> DeclaredConstructorHandles
        {
            get
            {
                foreach (MethodHandle methodHandle in _typeDefinition.Methods)
                {
                    if (methodHandle.IsConstructor(_reader))
                        yield return methodHandle;
                }
            }
        }

        internal IEnumerable<EventHandle> DeclaredEventHandles
        {
            get
            {
                return _typeDefinition.Events;
            }
        }

        internal IEnumerable<FieldHandle> DeclaredFieldHandles
        {
            get
            {
                return _typeDefinition.Fields;
            }
        }

        internal IEnumerable<MethodHandle> DeclaredMethodAndConstructorHandles
        {
            get
            {
                return _typeDefinition.Methods;
            }
        }

        internal IEnumerable<PropertyHandle> DeclaredPropertyHandles
        {
            get
            {
                return _typeDefinition.Properties;
            }
        }

        private readonly MetadataReader _reader;
        private readonly TypeDefinitionHandle _typeDefinitionHandle;
        private readonly TypeDefinition _typeDefinition;
        private readonly RuntimeTypeHandle _typeHandle;

        private NamespaceChain NamespaceChain
        {
            get
            {
                if (_lazyNamespaceChain == null)
                    _lazyNamespaceChain = new NamespaceChain(_reader, _typeDefinition.NamespaceDefinition);
                return _lazyNamespaceChain;
            }
        }

        private volatile NamespaceChain _lazyNamespaceChain;

        private static readonly NamedTypeToGuidTable s_namedTypeToGuidTable = new NamedTypeToGuidTable();
        private sealed class NamedTypeToGuidTable : ConcurrentUnifier<RuntimeNamedTypeInfo, Tuple<Guid>>
        {
            protected sealed override Tuple<Guid> Factory(RuntimeNamedTypeInfo key)
            {
                return new Tuple<Guid>(Guid.NewGuid());
            }
        }
    }
}



