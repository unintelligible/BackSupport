using System;
using System.IO;
using System.Text.RegularExpressions;
using BackSupport;
using Jint;
using NUnit.Framework;

namespace BackSupportTests
{
    [TestFixture]
    public class JsGenerationTests
    {
        TestFileUtils _testFileUtils;
        GeneratorOptions _options;
        Generator _generator;
        string _runtime;

        [SetUp]
        public void SetUp()
        {
            _testFileUtils = new TestFileUtils();
            _options = new GeneratorOptions();
            _generator = new Generator(_options, _testFileUtils);
            _generator.AddFilter(typeof(TestObjects.User).Assembly, new Regex(typeof(TestObjects.User).FullName));
            _options.OutputFile = "C:\\temp\\ignored.txt";
            _runtime = File.ReadAllText(".\\BackSupport.Runtime.js");
        }

        [Test]
        public void ShouldGenerateFieldsWithCorrectTypes()
        {
            _options.EntityJsBaseClass = null;
            _generator.Generate();
            Console.Write(_testFileUtils.WrittenContents);
            var engine = new JintEngine();
            engine.Run(_runtime);
            engine.Run(_testFileUtils.WrittenContents);
            engine.Run("var x = new BackSupportTests.TestObjects.User();");
            var x = engine.Run("return x.fields['FullName']['type'];");
            Assert.AreEqual("System.String", x);
            x = engine.Run("return x.fields['Age']['type'];");
            Assert.AreEqual("System.Int32", x);
            x = engine.Run("return x.fields['CustomerType']['type'];");
            Assert.AreEqual("System.String", x);
            x = engine.Run("return x.fields['FullName']['type'];");
            Assert.AreEqual("System.String", x);
            x = engine.Run("return x.fields['OptionalField']['type'];");
            Assert.AreEqual("System.String", x);
            x = engine.Run("return x.fields['DateOfBirth']['type'];");
            Assert.AreEqual("System.DateTime", x);
        }

        [Test]
        public void ShouldGenerateFieldsWithCorrectValidations()
        {
            _options.EntityJsBaseClass = null;
            _generator.Generate();
            Console.Write(_testFileUtils.WrittenContents);
            var engine = new JintEngine();
            engine.Run(_runtime);
            engine.Run(_testFileUtils.WrittenContents);
            engine.Run("var x = new BackSupportTests.TestObjects.User();");
            // fullname
            var x = engine.Run("return x.fields['FullName']['validations'].length;");
            Assert.AreEqual(1, engine.Run("return x.fields['FullName']['validations'].length;"));
            //Assert.AreEqual("BackSupport.Validate.Required", engine.Run("return x.fields['Name']['validations'][0];"));
            // age
            Assert.AreEqual(2, engine.Run("return x.fields['Age']['validations'].length;"));
            //Assert.AreEqual("BackSupport.Validate.Required", engine.Run("return x.fields['Age']['validations'][0];"));
            //Assert.AreEqual("BackSupport.Validate.Range", engine.Run("return x.fields['Age']['validations'][1];"));
            // customer type
            Assert.AreEqual(1, engine.Run("return x.fields['CustomerType']['validations'].length;"));
            //Assert.AreEqual("BackSupport.Validate.Regex", engine.Run("return x.fields['CustomerType']['validations'][0];"));
            // fullname
            Assert.AreEqual(1, engine.Run("return x.fields['FullName']['validations'].length;"));
            //Assert.AreEqual("BackSupport.Validate.StringLength", engine.Run("return x.fields['FullName']['validations'][0];"));
            //optional field
            Assert.AreEqual(0, engine.Run("return x.fields['OptionalField']['validations'].length;"));
            // date of birth
        }
    }

    public class TestFileUtils : IFileUtils
    {
        public string WrittenFilename { get; private set; }
        public string WrittenContents { get; private set; }
        public void WriteFile(string filename, string contents)
        {
            WrittenContents = contents;
            WrittenFilename = filename;
        }
    }

}
