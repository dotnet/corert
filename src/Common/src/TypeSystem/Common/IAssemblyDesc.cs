// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Optional interface a <see cref="ModuleDesc"/> should implement if it represents an assembly.
    /// </summary>
    public interface IAssemblyDesc
    {
        /// <summary>
        /// Gets the assembly name.
        /// </summary>
        AssemblyName GetName();
    }
}
