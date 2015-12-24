// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Core.NonPortable;
using global::Internal.Reflection.Extensibility;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.ParameterInfos
{
    //
    // Abstract base for all ParameterInfo objects created by the Runtime.
    //
    internal abstract class RuntimeParameterInfo : ExtensibleParameterInfo
    {
        protected RuntimeParameterInfo(MemberInfo member, int position)
        {
            _member = member;
            _position = position;
        }

        public abstract override ParameterAttributes Attributes { get; }
        public abstract override IEnumerable<CustomAttributeData> CustomAttributes { get; }
        public abstract override Object DefaultValue { get; }

        public sealed override bool Equals(Object obj)
        {
            RuntimeParameterInfo other = obj as RuntimeParameterInfo;
            if (other == null)
                return false;
            if (this._position != other._position)
                return false;
            if (!(this._member.Equals(other._member)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _member.GetHashCode();
        }

        public abstract override bool HasDefaultValue { get; }

        public sealed override MemberInfo Member
        {
            get
            {
                return _member;
            }
        }

        public abstract override String Name { get; }
        public abstract override Type ParameterType { get; }

        public sealed override int Position
        {
            get
            {
                return _position;
            }
        }

        public sealed override String ToString()
        {
            return this.ParameterTypeString + " " + this.Name;
        }

        // Gets the ToString() output of ParameterType in a pay-to-play-safe way: Other Reflection ToString() methods should always use this rather than
        // "ParameterType.ToString()".
        internal abstract String ParameterTypeString { get; }

        protected ReflectionDomain ReflectionDomain
        {
            get
            {
                return ReflectionCoreExecution.ExecutionDomain; //@TODO: User Reflection Domains not yet supported.
            }
        }

        private MemberInfo _member;
        private int _position;
    }
}

