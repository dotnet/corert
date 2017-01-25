// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Collection of "qualified handle" tuples.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Metadata.NativeFormat;
using Internal.Runtime.TypeLoader;

namespace System.Reflection.Runtime.General
{

    public partial struct QMethodDefinition
    {
        public QMethodDefinition(MetadataReader reader, MethodHandle handle)
        {
            _reader = reader;
            _handle = ((Handle)handle).AsInt();
        }

        public MetadataReader NativeFormatReader { get { return _reader as MetadataReader; } }
        public MethodHandle NativeFormatHandle { get { return _handle.AsHandle().ToMethodHandle(NativeFormatReader); } }

        public bool IsNativeFormatMetadataBased
        {
            get
            {
                return (_reader != null) && _reader is global::Internal.Metadata.NativeFormat.MetadataReader;
            }
        }
    }

    public partial struct QTypeDefinition
    {
        public QTypeDefinition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            _reader = reader;
            _handle = ((Handle)handle).AsInt();
        }

        public MetadataReader NativeFormatReader { get { return _reader as MetadataReader; } }
        public TypeDefinitionHandle NativeFormatHandle { get { return _handle.AsHandle().ToTypeDefinitionHandle(NativeFormatReader); } }

        public bool IsNativeFormatMetadataBased
        {
            get
            {
                return (_reader != null) && _reader is global::Internal.Metadata.NativeFormat.MetadataReader;
            }
        }
    }

    public partial struct QTypeDefRefOrSpec
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
            _handle = handle.ToIntToken();
        }
    
        public bool IsNativeFormatMetadataBased
        {
            get
            {
                return (_reader != null) && Reader is global::Internal.Metadata.NativeFormat.MetadataReader;
            }
        }
    }
}