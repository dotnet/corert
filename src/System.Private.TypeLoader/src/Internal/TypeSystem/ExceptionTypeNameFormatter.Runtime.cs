// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    partial class ExceptionTypeNameFormatter
    {
        private string GetTypeName(DefType type)
        {
            if (type is NoMetadata.NoMetadataType)
                return ((NoMetadata.NoMetadataType)type).DiagnosticName;

            return type.Name;
        }

        private string GetTypeNamespace(DefType type)
        {
            if (type is NoMetadata.NoMetadataType)
                return ((NoMetadata.NoMetadataType)type).DiagnosticNamespace;

            return type.Namespace;
        }
    }
}
