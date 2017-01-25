// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Collection of "qualified handle" tuples.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Runtime.TypeInfos;

namespace System.Reflection.Runtime.General
{
    public partial struct QSignatureTypeHandle
    {
        public object Reader { get { return _reader; } }
        private object _reader;
#if ECMA_METADATA_SUPPORT
        readonly private global::System.Reflection.Metadata.BlobReader _blobReader;
#endif
        private global::Internal.Metadata.NativeFormat.Handle _handle;

        internal RuntimeTypeInfo Resolve(TypeContext typeContext)
        {
            Exception exception = null;
            RuntimeTypeInfo runtimeType = TryResolve(typeContext, ref exception);
            if (runtimeType == null)
                throw exception;
            return runtimeType;
        }

        internal RuntimeTypeInfo TryResolve(TypeContext typeContext, ref Exception exception)
        {
            if (Reader is global::Internal.Metadata.NativeFormat.MetadataReader)
            {
                return _handle.TryResolve((global::Internal.Metadata.NativeFormat.MetadataReader)Reader, typeContext, ref exception);
            }
            
#if ECMA_METADATA_SUPPORT
            global::System.Reflection.Metadata.MetadataReader ecmaReader = Reader as global::System.Reflection.Metadata.MetadataReader;
            if (ecmaReader != null)
            {
                return TryResolveSignature(typeContext, ref exception);
            }
#endif

            throw new BadImageFormatException();  // Expected TypeRef, Def or Spec with MetadataReader
        }
        
        // 
        // This is a port of the desktop CLR's RuntimeType.FormatTypeName() routine. This routine is used by various Reflection ToString() methods
        // to display the name of a type. Do not use for any other purpose as it inherits some pretty quirky desktop behavior.
        //        
        internal String FormatTypeName(TypeContext typeContext)
        {
            try
            {
                // Though we wrap this in a try-catch as a failsafe, this code must still strive to avoid triggering MissingMetadata exceptions
                // (non-error exceptions are very annoying when debugging.)

                Exception exception = null;
                RuntimeTypeInfo runtimeType = TryResolve(typeContext, ref exception);
                if (runtimeType == null)
                    return ToStringUtils.UnavailableType;

                // Because this runtimeType came from a successful TryResolve() call, it is safe to querying the TypeInfo's of the type and its component parts.
                // If we're wrong, we do have the safety net of a try-catch.
                return runtimeType.FormatTypeName();
            }
            catch (Exception)
            {
                return ToStringUtils.UnavailableType;
            }
        }        
    }
}