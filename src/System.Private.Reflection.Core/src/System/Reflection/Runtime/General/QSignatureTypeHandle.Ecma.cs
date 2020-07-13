// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Collection of "qualified handle" tuples.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Runtime.TypeInfos;
using Internal.Runtime.TypeLoader;

namespace System.Reflection.Runtime.General
{
    public partial struct QSignatureTypeHandle
    {
        public QSignatureTypeHandle(MetadataReader reader, BlobReader blobReader)
        {
            _reader = reader;
            _blobReader = blobReader;
            _handle = default(global::Internal.Metadata.NativeFormat.Handle);
        }

        private RuntimeTypeInfo TryResolveSignature(TypeContext typeContext, ref Exception exception)
        {
            ReflectionTypeProvider typeProvider = new ReflectionTypeProvider(throwOnError: false);
            SignatureDecoder<RuntimeTypeInfo, TypeContext> signatureDecoder = new SignatureDecoder<RuntimeTypeInfo, TypeContext>(typeProvider, (MetadataReader)Reader, typeContext);
            BlobReader localCopyOfReader = _blobReader;
            RuntimeTypeInfo result = signatureDecoder.DecodeType(ref localCopyOfReader, false);
            exception = typeProvider.ExceptionResult;
            return result;
        }
    }
}
