// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Internal.TypeSystem
{
    public abstract partial class ModuleDesc : TypeSystemEntity
    {
        public override TypeSystemContext Context
        {
            get;
        }

        public ModuleDesc(TypeSystemContext context)
        {
            Context = context;
        }

        /// <summary>
        /// Gets a type in this module with the specified name.
        /// </summary>
        public abstract MetadataType GetType(string nameSpace, string name, bool throwIfNotFound = true);

        /// <summary>
        /// Gets the global &lt;Module&gt; type.
        /// </summary>
        public abstract MetadataType GetGlobalModuleType();

        /// <summary>
        /// Retrieves a collection of all types defined in the current module. This includes nested types.
        /// </summary>
        public abstract IEnumerable<MetadataType> GetAllTypes();
    }
}
