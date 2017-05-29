// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Internal.IL;
using Internal.TypeSystem.Ecma;

namespace ILVerify.Tests
{
    /// <summary>
    /// Helper method to load the binaries generated based on the il code, which drive the tests
    /// </summary>
    public static class TestDataLoader
    {
        /// <summary>
        /// The folder with the binaries which are compiled from the test driver IL Code
        /// </summary>
        public static string TESTASSEMBLYPATH = @"..\..\..\ILTests\";

        public static EcmaModule GetModuleForTestAssembly(string assemblyName)
        {
            var _typeSystemContext = new SimpleTypeSystemContext();
            var coreAssembly = typeof(Object).Assembly;

            _typeSystemContext.InputFilePaths = new Dictionary<string, string>
            {
                { coreAssembly.GetName().Name, coreAssembly.Location }
            };

            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModuleForSimpleName(coreAssembly.GetName().Name));
            return _typeSystemContext.GetModuleFromPath(TESTASSEMBLYPATH + assemblyName);
        }      
    }
}
