using System;
using System.Collections.Generic;
using System.Text;
using Internal.IL;
using Internal.TypeSystem.Ecma;
using Xunit;

namespace ILVerify.Tests
{
    
    public class Test01
    {
        [Fact]
        public void TestMethod1()
        {
            var _typeSystemContext = new SimpleTypeSystemContext();
            _typeSystemContext.InputFilePaths = new Dictionary<String, String> { { "CoreTestAssembly",
                @"C:\Users\gergo\Source\Repos\gregkalaposcorert\bin\Product\Windows_NT.x64.Debug\CoreTestAssembly\CoreTestAssembly.dll" } };
            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModuleForSimpleName("CoreTestAssembly"));


            var module = _typeSystemContext.GetModuleFromPath(@"C:\Users\gergo\Source\Repos\gregkalaposcorert\src\ILVerify\tests\ILTests\IlVerifyTestAssembly.dll");
            
            foreach (var methodHandle in module.MetadataReader.MethodDefinitions)
            {
                var method = (EcmaMethod)module.GetMethod(methodHandle);

                var methodIL = EcmaMethodIL.Create(method);
                if (methodIL == null)
                    continue;

                var importer = new ILImporter(method, methodIL);
                importer.Verify();
            }
            

            ILVerify.VerifierError vv = new ILVerify.VerifierError();
            vv = VerifierError.CallAbstract;
            
            Assert.Equal(VerifierError.CallAbstract, vv);
        }



    }
}
