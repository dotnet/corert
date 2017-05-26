using System;
using System.Collections.Generic;
using System.Text;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Xunit;

namespace ILVerify.Tests
{
    
    public class BasicArithmeticTests
    {
        [Fact]
        public void SimpleAddTest()
        {
            var module = GetModuleForTestAssembly("BasicArithmeticTests.dll");
            var type = module.GetType(String.Empty , "BasicArithmeticTestsType");

            var int32Type = module.Context.GetWellKnownType(WellKnownType.Int32);

            var methodSignature = new Internal.TypeSystem.MethodSignature(Internal.TypeSystem.MethodSignatureFlags.None, 0, int32Type, new TypeDesc[] { int32Type , int32Type });
            var mm = type.GetMethod("SimpleAdd", methodSignature);


            foreach (var methodHandle in module.MetadataReader.MethodDefinitions)
            {
                var method = (EcmaMethod)module.GetMethod(methodHandle);

                var methodIL = EcmaMethodIL.Create(method);
                if (methodIL == null)
                    continue;

                var importer = new ILImporter(method, methodIL);
                importer.ReportVerificationError = new Action<VerificationErrorArgs>((err) =>
                {
                    
                });

                importer.Verify();
            }
        }


        public EcmaModule GetModuleForTestAssembly(string AssemblyName)
        {
            var _typeSystemContext = new SimpleTypeSystemContext();
            _typeSystemContext.InputFilePaths = new Dictionary<String, String> { { "CoreTestAssembly",
                @"..\..\..\..\..\..\bin\Product\Windows_NT.x64.Debug\CoreTestAssembly\\CoreTestAssembly.dll" } };
            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModuleForSimpleName("CoreTestAssembly"));

            return _typeSystemContext.GetModuleFromPath(@"..\..\..\ILTests\" + AssemblyName); 
        }
    }
}
