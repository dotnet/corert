// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Text;

using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using System.Reflection.Metadata;

using System.Reflection.Runtime.General;

namespace Internal.Runtime.TypeLoader
{
    /// <summary>
    /// Extension methods that provide low level ToString() equivalents for some of the core types.
    /// Calling regular ToString() on these types goes through a lot of the CultureInfo machinery
    /// which is not low level enough for the type loader purposes.
    /// </summary>
    internal static partial class LowLevelStringConverter
    {
        private static string EcmaMetadataFullName(QTypeDefinition qTypeDefinition)
        {
            if (!qTypeDefinition.IsEcmaFormatMetadataBased)
                return null;
            MetadataReader reader = qTypeDefinition.EcmaFormatReader;
            TypeDefinition typeDefinition = reader.GetTypeDefinition(qTypeDefinition.EcmaFormatHandle);

            string result = reader.GetString(typeDefinition.Name);

            TypeDefinitionHandle enclosingTypeHandle = typeDefinition.GetDeclaringType();
            if (!enclosingTypeHandle.IsNil)
            {
                String containingTypeName = EcmaMetadataFullName(new QTypeDefinition(reader, enclosingTypeHandle));
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

            return result;
        }
    }
}
