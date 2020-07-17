// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;

using System.Reflection.Metadata;
using System.Reflection.Runtime.General;

namespace Internal.Reflection.Execution.PayForPlayExperience
{
    internal static partial class DiagnosticMappingTables
    {
        static partial void TryGetFullNameFromTypeDefEcma(QTypeDefinition qTypeDefinition, List<int> genericParameterOffsets, ref string result)
        {
            result = null;
            if (!qTypeDefinition.IsEcmaFormatMetadataBased)
                return;

            MetadataReader reader = qTypeDefinition.EcmaFormatReader;
            TypeDefinition typeDefinition = reader.GetTypeDefinition(qTypeDefinition.EcmaFormatHandle);

            result = reader.GetString(typeDefinition.Name);

            TypeDefinitionHandle enclosingTypeHandle = typeDefinition.GetDeclaringType();
            if (!enclosingTypeHandle.IsNil)
            {
                String containingTypeName = null;
                TryGetFullNameFromTypeDefEcma(new QTypeDefinition(reader, enclosingTypeHandle), genericParameterOffsets, ref containingTypeName);
                result = containingTypeName + "." + result;
            }
            else
            {
                if (!typeDefinition.Namespace.IsNil)
                {
                    string namespaceName = reader.GetString(typeDefinition.Namespace);
                    result = namespaceName + "." + result;
                }
            }

            result = ConvertBackTickNameToNameWithReducerInputFormat(result, genericParameterOffsets);
        }
    }
}
