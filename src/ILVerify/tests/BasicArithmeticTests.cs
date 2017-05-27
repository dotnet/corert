using System;
using System.Collections.Generic;
using System.Text;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Xunit;

namespace ILVerify.Tests
{
    /// <summary>
    /// Tests for BasicArithmeticTests.il
    /// </summary>
    public class BasicArithmeticTests
    {
        public static EcmaModule s_testModule = TestDataLoader.GetModuleForTestAssembly("BasicArithmeticTests.dll");

        [Fact]
        public void SimpleAddTest()
        {
            ILImporter importer = TestDataLoader.GetILImporterForMethod(s_testModule, "[BasicArithmeticTests]BasicArithmeticTestsType.ValidSimpleAdd");
            
            var verifierErrors = new List<VerifierError>();
            importer.ReportVerificationError = new Action<VerificationErrorArgs>((err) =>
            {
                verifierErrors.Add(err.Code);
            });

            importer.Verify();
            Assert.Equal(0, verifierErrors.Count);
        }
        
        [Fact]    
        public void InvalidSimpleAddTest()
        {
            ILImporter importer = TestDataLoader.GetILImporterForMethod(s_testModule, "[BasicArithmeticTests]BasicArithmeticTestsType.InvalidSimpleAdd");

            var verifierErrors = new List<VerifierError>();
            importer.ReportVerificationError = new Action<VerificationErrorArgs>((err) =>
            {
                verifierErrors.Add(err.Code);
            });

            Assert.Throws<LocalVerificationException>(() => importer.Verify());            
            Assert.Equal(1, verifierErrors.Count);
            Assert.Equal(VerifierError.ExpectedNumericType, verifierErrors[0]);            
        }
    }
}
