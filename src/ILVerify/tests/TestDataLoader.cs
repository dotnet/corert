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
    static class TestDataLoader
    {
        /// <summary>
        /// The folder which contains the core assembly (containing System.Object, etc.)
        /// </summary>
        static string COREASSEMBLYPATH = @"..\..\..\..\..\..\bin\Product\Windows_NT.x64.Debug\CoreTestAssembly\\";

        /// <summary>
        /// The folter with the binaries which are compiled from the test driver IL Code
        /// </summary>
        static string TESTASSEMBLYPATH = @"..\..\..\ILTests\";

        public static EcmaModule GetModuleForTestAssembly(string assemblyName)
        {
            var _typeSystemContext = new SimpleTypeSystemContext();

            //we use the src\ILCompiler.TypeSystem\tests\CoreTestAssembly project as a "dummy" core dll
            _typeSystemContext.InputFilePaths = new Dictionary<string, string> { { "CoreTestAssembly",
                COREASSEMBLYPATH + "CoreTestAssembly.dll" } };
            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModuleForSimpleName("CoreTestAssembly"));

            return _typeSystemContext.GetModuleFromPath(TESTASSEMBLYPATH + assemblyName);
        }

        public static ILImporter GetMethodDesc(EcmaModule module, string testMethodName)
        {
            EcmaMethod method = null;
            EcmaMethodIL methodIL = null;
            foreach (var methodHandle in module.MetadataReader.MethodDefinitions)
            {
                method = (EcmaMethod)module.GetMethod(methodHandle);

                methodIL = EcmaMethodIL.Create(method);
                if (methodIL == null)
                    continue;

                var methodName = method.ToString();
                if (methodName == testMethodName)
                {
                    break;
                }
            }

            if(method == null || methodIL == null)
            {
                throw new Exception($"Method {testMethodName} not found in module");
            }

            return new ILImporter(method, methodIL);
        }
    }
}
