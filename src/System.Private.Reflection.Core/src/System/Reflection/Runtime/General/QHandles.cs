// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Collection of "qualified handle" tuples.
//

using global::System;
using global::System.Collections.Generic;
using global::System.Diagnostics;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Core
{
    public struct QScopeDefinition : IEquatable<QScopeDefinition>
    {
        public QScopeDefinition(MetadataReader reader, ScopeDefinitionHandle handle)
        {
            _reader = reader;
            _handle = handle;
        }

        public MetadataReader Reader { get { return _reader; } }
        public ScopeDefinitionHandle Handle { get { return _handle; } }
        public ScopeDefinition ScopeDefinition
        {
            get
            {
                return _handle.GetScopeDefinition(_reader);
            }
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is QScopeDefinition))
                return false;
            return Equals((QScopeDefinition)obj);
        }

        public bool Equals(QScopeDefinition other)
        {
            if (!(this._reader == other._reader))
                return false;
            if (!(this._handle.Equals(other._handle)))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            return _handle.GetHashCode();
        }

        private MetadataReader _reader;
        private ScopeDefinitionHandle _handle;
    }
}

namespace System.Reflection.Runtime.General
{
    internal struct QHandle : IEquatable<QHandle>
    {
        public QHandle(MetadataReader reader, Handle handle)
        {
            _reader = reader;
            _handle = handle;
        }

        public MetadataReader Reader { get { return _reader; } }
        public Handle Handle { get { return _handle; } }

        public override bool Equals(Object obj)
        {
            if (!(obj is QHandle))
                return false;
            return Equals((QHandle)obj);
        }

        public bool Equals(QHandle other)
        {
            if (!(this._reader == other._reader))
                return false;
            if (!(this._handle.Equals(other._handle)))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            return _handle.GetHashCode();
        }

        private MetadataReader _reader;
        private Handle _handle;
    }


    internal struct QTypeDefinition : IEquatable<QTypeDefinition>
    {
        public QTypeDefinition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            _reader = reader;
            _handle = handle;
        }

        public MetadataReader Reader { get { return _reader; } }
        public TypeDefinitionHandle Handle { get { return _handle; } }

        public override bool Equals(Object obj)
        {
            if (!(obj is QTypeDefinition))
                return false;
            return Equals((QTypeDefinition)obj);
        }

        public bool Equals(QTypeDefinition other)
        {
            if (!(this._reader == other._reader))
                return false;
            if (!(this._handle.Equals(other._handle)))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            return _handle.GetHashCode();
        }

        private MetadataReader _reader;
        private TypeDefinitionHandle _handle;
    }


    internal struct QTypeDefRefOrSpec
    {
        public QTypeDefRefOrSpec(MetadataReader reader, Handle handle, bool skipCheck = false)
        {
            if (!skipCheck)
            {
                if (!handle.IsTypeDefRefOrSpecHandle(reader))
                    throw new BadImageFormatException();
            }
            Debug.Assert(handle.IsTypeDefRefOrSpecHandle(reader));
            _reader = reader;
            _handle = handle;
        }

        public MetadataReader Reader { get { return _reader; } }
        public Handle Handle { get { return _handle; } }

        public static readonly QTypeDefRefOrSpec Null = default(QTypeDefRefOrSpec);

        private MetadataReader _reader;
        private Handle _handle;
    }

    internal struct QGenericParameter : IEquatable<QGenericParameter>
    {
        public QGenericParameter(MetadataReader reader, GenericParameterHandle handle)
        {
            _reader = reader;
            _handle = handle;
        }

        public MetadataReader Reader { get { return _reader; } }
        public GenericParameterHandle Handle { get { return _handle; } }

        public override bool Equals(Object obj)
        {
            if (!(obj is QGenericParameter))
                return false;
            return Equals((QGenericParameter)obj);
        }

        public bool Equals(QGenericParameter other)
        {
            if (!(this._reader == other._reader))
                return false;
            if (!(this._handle.Equals(other._handle)))
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            return _handle.GetHashCode();
        }

        private MetadataReader _reader;
        private GenericParameterHandle _handle;
    }
}
