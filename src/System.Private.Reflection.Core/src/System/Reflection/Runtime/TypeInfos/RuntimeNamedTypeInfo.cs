// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Collections.Concurrent;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Reflection.Tracing;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // TypeInfos that represent type definitions (i.e. Foo or Foo<>, but not Foo<int> or arrays/pointers/byrefs.)
    // 
    //
    internal sealed partial class RuntimeNamedTypeInfo : RuntimeTypeInfo, IEquatable<RuntimeNamedTypeInfo>
    {
        private RuntimeNamedTypeInfo(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle)
        {
            _reader = reader;
            _typeDefinitionHandle = typeDefinitionHandle;
            _typeDefinition = _typeDefinitionHandle.GetTypeDefinition(reader);
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

                return RuntimeAssembly.GetRuntimeAssembly(this.ReflectionDomain, runtimeAssemblyName);
            }
        }

        public sealed override TypeAttributes Attributes
        {
            get
            {
                TypeAttributes attr = _typeDefinition.Flags;
                return attr;
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

                IEnumerable<CustomAttributeData> customAttributes = RuntimeCustomAttributeData.GetCustomAttributes(this.ReflectionDomain, _reader, _typeDefinition.CustomAttributes);
                foreach (CustomAttributeData cad in customAttributes)
                    yield return cad;
                ExecutionDomain executionDomain = this.ReflectionDomain as ExecutionDomain;
                if (executionDomain != null)
                {
                    foreach (CustomAttributeData cad in executionDomain.ExecutionEnvironment.GetPsuedoCustomAttributes(_reader, _typeDefinitionHandle))
                    {
                        yield return cad;
                    }
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
                    yield return RuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(_reader, nestedTypeHandle);
                }
            }
        }

        public sealed override bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj))
                return true;

            RuntimeNamedTypeInfo other = obj as RuntimeNamedTypeInfo;
            if (!Equals(other))
                return false;
            return true;
        }

        public bool Equals(RuntimeNamedTypeInfo other)
        {
            if (other == null)
                return false;
            if (this._reader != other._reader)
                return false;
            if (!(this._typeDefinitionHandle.Equals(other._typeDefinitionHandle)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _typeDefinitionHandle.GetHashCode();
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
                        String guidString = guidStringArgument.Value.ParseConstantValue(this.ReflectionDomain, _reader) as String;
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
                return _namedTypeToGuidTable.GetOrAdd(this).Item1;
            }
        }

        public sealed override bool IsGenericTypeDefinition
        {
            get
            {
                return _typeDefinition.GenericParameters.GetEnumerator().MoveNext();
            }
        }

        public sealed override bool IsGenericType
        {
            get
            {
                return _typeDefinition.GenericParameters.GetEnumerator().MoveNext();
            }
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

        internal sealed override RuntimeType[] RuntimeGenericTypeParameters
        {
            get
            {
                LowLevelList<RuntimeType> genericTypeParameters = new LowLevelList<RuntimeType>();

                foreach (GenericParameterHandle genericParameterHandle in _typeDefinition.GenericParameters)
                {
                    RuntimeType genericParameterType = RuntimeTypeUnifierEx.GetRuntimeGenericParameterTypeForTypes(this, genericParameterHandle);
                    genericTypeParameters.Add(genericParameterType);
                }

                return genericTypeParameters.ToArray();
            }
        }

        internal sealed override RuntimeType RuntimeType
        {
            get
            {
                if (_lazyType == null)
                {
                    _lazyType = this.ReflectionDomain.ResolveTypeDefinition(_reader, _typeDefinitionHandle);
                }
                return _lazyType;
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

        private MetadataReader _reader;
        private TypeDefinitionHandle _typeDefinitionHandle;
        private TypeDefinition _typeDefinition;

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

        private volatile RuntimeType _lazyType;

        private static NamedTypeToGuidTable _namedTypeToGuidTable = new NamedTypeToGuidTable();
        private sealed class NamedTypeToGuidTable : ConcurrentUnifier<RuntimeNamedTypeInfo, Tuple<Guid>>
        {
            protected sealed override Tuple<Guid> Factory(RuntimeNamedTypeInfo key)
            {
                return new Tuple<Guid>(Guid.NewGuid());
            }
        }
    }
}



