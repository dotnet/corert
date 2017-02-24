// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

/*============================================================
**
** 
** 
**
**
** Purpose: Interface for resource grovelers
**
** 
===========================================================*/

namespace System.Resources
{
    internal interface IResourceGroveler
    {
        ResourceSet GrovelForResourceSet(CultureInfo culture, Dictionary<String, ResourceSet> localResourceSets, bool tryParents,
            bool createIfNotExists);
    }
}