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

            var systemRuntime = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName("System.Runtime"));
            var systemPrivateCoreLib = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName("System.Private.CoreLib"));
                      
            _typeSystemContext.InputFilePaths = new Dictionary<string, string>
            {
                { "System.Runtime", systemRuntime.Location },
                { "System.Private.CoreLib", systemPrivateCoreLib.Location }
            };

            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModuleForSimpleName("System.Runtime"));
            return _typeSystemContext.GetModuleFromPath(TESTASSEMBLYPATH + assemblyName);
        }      
    }
}
