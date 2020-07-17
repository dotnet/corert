// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Collection of "qualified handle" tuples.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Internal.Runtime.TypeLoader;

namespace System.Reflection.Runtime.General
{
    static class HandleHelpers
    {
        public static bool IsTypeDefRefOrSpecHandle(this Handle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                case HandleKind.TypeReference:
                case HandleKind.TypeSpecification:
                    return true;

                default:
                    return false;
            }
        }
    }

    public partial struct QMethodDefinition
    {
        public QMethodDefinition(MetadataReader reader, MethodDefinitionHandle handle)
        {
            _reader = reader;
            _handle = MetadataTokens.GetToken(handle);
        }

        public MetadataReader EcmaFormatReader { get { return _reader as MetadataReader; } }
        public MethodDefinitionHandle EcmaFormatHandle { get { return (MethodDefinitionHandle)MetadataTokens.Handle(_handle); } }

        public bool IsEcmaFormatMetadataBased
        {
            get
            {
                return (_reader != null) && (_reader is MetadataReader);
            }
        }
    }
    
    public partial struct QTypeDefinition
    {
        public QTypeDefinition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            _reader = reader;
            _handle = MetadataTokens.GetToken(handle);
        }

        public MetadataReader EcmaFormatReader { get { return _reader as MetadataReader; } }
        public TypeDefinitionHandle EcmaFormatHandle { get { return (TypeDefinitionHandle)MetadataTokens.Handle(_handle); } }

        public bool IsEcmaFormatMetadataBased
        {
            get
            {
                return (_reader != null) && (_reader is MetadataReader);
            }
        }
    }

    public partial struct QTypeDefRefOrSpec
    {
        public QTypeDefRefOrSpec(MetadataReader reader, Handle handle, bool skipCheck = false)
        {
            if (!skipCheck)
            {
                if (!handle.IsTypeDefRefOrSpecHandle())
                    throw new BadImageFormatException();
            }
            
            Debug.Assert(handle.IsTypeDefRefOrSpecHandle());
            _reader = reader;
            _handle = MetadataTokens.GetToken(handle);
        }        
    }
}
