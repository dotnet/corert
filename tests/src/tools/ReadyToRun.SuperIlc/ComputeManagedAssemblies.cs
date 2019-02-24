// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

class ComputeManagedAssemblies
{
    public static IEnumerable<string> GetManagedAssembliesInFolder(string folder)
    {
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            if (IsManaged(file))
            {
                yield return file;
            }
        }
    }

    public static bool IsManaged(string file)
    {
        // Only files named *.dll and *.exe are considered as possible assemblies
        if (!Path.HasExtension(file) || (Path.GetExtension(file) != ".dll" && Path.GetExtension(file) != ".exe"))
            return false;

        try
        {
            using (FileStream moduleStream = File.OpenRead(file))
            using (var module = new PEReader(moduleStream))
            {
                if (module.HasMetadata)
                {
                    MetadataReader moduleMetadataReader = module.GetMetadataReader();
                    if (moduleMetadataReader.IsAssembly)
                    {
                        string culture = moduleMetadataReader.GetString(moduleMetadataReader.GetAssemblyDefinition().Culture);

                        if (culture == "" || culture.Equals("neutral", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (BadImageFormatException)
        {
        }

        return false;
    }
}