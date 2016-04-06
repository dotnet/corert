// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Text;
using global::System.Threading;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;
using Internal.Reflection.Tracing;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.Types
{
    //
    // The runtime's implementation of Types for named types (i.e. types with a TypeDefinitionHandle.)
    //
    internal partial class RuntimeInspectionOnlyNamedType : RuntimeType
    {
        protected RuntimeInspectionOnlyNamedType(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle)
            : base()
        {
#if DEBUG
            if (!(this.InternalViolatesTypeIdentityRules))
            {
                RuntimeTypeHandle runtimeTypeHandle;
                if (ReflectionCoreExecution.ExecutionEnvironment.TryGetNamedTypeForMetadata(reader, typeDefinitionHandle, out runtimeTypeHandle))
                    Debug.Assert(false, "Type identity violation: You must use a RuntimeEENamedType to represent this type as RH has generated an EEType for it.");
            }
#endif
            _reader = reader;
            _typeDefinitionHandle = typeDefinitionHandle;
            _typeDefinition = _typeDefinitionHandle.GetTypeDefinition(_reader);
        }

        public sealed override bool Equals(Object obj)
        {
            return InternalIsEqual(obj);  // Do not change this - see comments in RuntimeType.cs regarding Equals()
        }

        public sealed override int GetHashCode()
        {
            return _typeDefinitionHandle.GetHashCode();
        }

        public sealed override Type DeclaringType
        {
            get
            {
                RuntimeType declaringType = null;
                TypeDefinitionHandle enclosingTypeDefHandle = _typeDefinition.EnclosingType;
                if (!enclosingTypeDefHandle.IsNull(_reader))
                {
                    declaringType = this.GetReflectionDomain().ResolveTypeDefinition(_reader, enclosingTypeDefHandle);
                }
                return declaringType;
            }
        }

        public sealed override Type GetGenericTypeDefinition()
        {
            if (_typeDefinition.GenericParameters.GetEnumerator().MoveNext())
                return this;
            return base.GetGenericTypeDefinition();
        }

        public sealed override String Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_Namespace(this);
#endif

                return EscapeIdentifier(NamespaceChain.NameSpace);
            }
        }

        public sealed override String ToString()
        {
            StringBuilder sb = null;

            foreach (GenericParameterHandle genericParameterHandle in _typeDefinition.GenericParameters)
            {
                if (sb == null)
                {
                    sb = new StringBuilder(this.FullName);
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
                return this.FullName;
            }
            else
            {
                return sb.Append(']').ToString();
            }
        }

        public sealed override String InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure)
        {
            ConstantStringValueHandle nameHandle = _typeDefinition.Name;
            String name = nameHandle.GetString(_reader);

            return EscapeIdentifier(name);
        }

        public sealed override String InternalFullNameOfAssembly
        {
            get
            {
                NamespaceChain namespaceChain = this.NamespaceChain;
                ScopeDefinitionHandle scopeDefinitionHandle = namespaceChain.DefiningScope;
                return scopeDefinitionHandle.ToRuntimeAssemblyName(_reader).FullName;
            }
        }

        public sealed override bool InternalIsGenericTypeDefinition
        {
            get
            {
                return _typeDefinition.GenericParameters.GetEnumerator().MoveNext();
            }
        }

        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public sealed override bool InternalIsOpen
        {
            get
            {
                return this.InternalIsGenericTypeDefinition;
            }
        }

        internal RuntimeTypeInfo GetInspectionOnlyNamedRuntimeTypeInfo()
        {
            return RuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(_reader, _typeDefinitionHandle);
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

        private static char[] charsToEscape = new char[] { '\\', '[', ']', '+', '*', '&', ',' };
        // Escape identifiers as described in "Specifying Fully Qualified Type Names" on msdn.
        // Current link is http://msdn.microsoft.com/en-us/library/yfsftwz6(v=vs.110).aspx
        private static string EscapeIdentifier(string identifier)
        {
            // Some characters in a type name need to be escaped
            if (identifier != null && identifier.IndexOfAny(charsToEscape) != -1)
            {
                StringBuilder sbEscapedName = new StringBuilder(identifier);
                sbEscapedName.Replace("\\", "\\\\");
                sbEscapedName.Replace("+", "\\+");
                sbEscapedName.Replace("[", "\\[");
                sbEscapedName.Replace("]", "\\]");
                sbEscapedName.Replace("*", "\\*");
                sbEscapedName.Replace("&", "\\&");
                sbEscapedName.Replace(",", "\\,");
                identifier = sbEscapedName.ToString();
            }
            return identifier;
        }
    }
}



