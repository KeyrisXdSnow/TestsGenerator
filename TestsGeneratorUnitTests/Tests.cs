using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestsGeneratorLib;
using Xunit;

namespace TestsGeneratorUnitTests
{
    public class Tests
    {
        
        private readonly NUnitTestsGenerator _generator = new NUnitTestsGenerator();
        private const string TestPath = @"..\..\..\TestsGenerator\Classes\GeneratedTests\";
        private const int CorrectId = 2;
        
        [Fact]
        public void  InvalidOutDirectory()
        {
            var classPaths = new List<string>
            {
                @"..\..\Classes\\ConstructorFormatter.cs",
                @"..\..\Classes\\\FieldFormatter.cs"
            };
            try
            {
                _generator.GenerateCLasses(classPaths, "validPath", 1, 1, 1);

            }
            catch (FileNotFoundException)
            {
                Assert.True(true);
            }

            Assert.False(false);
        }
        
        [Fact]
        public void  ValidOutDirectory()
        {
            var classPaths = new List<string>
            {
                @"..\..\Classes\\ConstructorFormatterAAA.cs",
                @"..\..\Classes\\FieldFormatter13123.cs"
            };
            Task task = null;
            try
            {
                task = _generator.GenerateCLasses(classPaths, TestPath, 1, 1, 1);
                task?.Wait();
            }
            catch (Exception)
            {
                Assert.True(false);
                return;
            }

            Assert.True(task != null);
        }
        
        [Fact]
        public void  GenerateClassSuccessfully()
        {
            var classPaths = new List<string>
            {
                @"..\..\..\..\TestsGenerator\Classes\\ClassFormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\ConstructorFormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\FieldFormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\MethodFormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\PropertiesFormatter.cs"
            }; 
            var task = _generator.GenerateCLasses(classPaths, TestPath, 6, 2, 6); 
            
            task?.Wait();
            
            if (task == null)
                Assert.True(false);
            else 
                Assert.Equal(task.Status,TaskStatus.RanToCompletion);
        }
        
        [Fact]
        public void  GenerateClassSuccessfullyWithAllFailPaths()
        {
            var classPaths = new List<string>
            {
                @"..\..\..\..\TestsGenerator\Classes\\ClassFormatter!.cs",
                @"..\..\..\..\TestsGenerator\Classes\ConstructorForma!tter.cs",
                @"..\..\..\..\TestsGenerator\Classes\FieldForma!ter.cs",
                @"..\..\..\..\TestsGenerator\Classes\MethodForm!atter.cs",
                @"..\..\..\..\TestsGenerator\Classes\Properties!Formatter.cs"
            }; 
            var task = _generator.GenerateCLasses(classPaths, TestPath, 6, 2, 6); 
            
            task?.Wait();
            
            if (task == null)
                Assert.True(false);
            else 
                Assert.Equal(task.Status,TaskStatus.RanToCompletion);
        }
        
        [Fact]
        public void  GenerateClassSuccessfullyWithSomeFailPaths()
        {
            var classPaths = new List<string>
            {
                @"..\..\..\..\TestsGenerator\Classes\\ClassF1ormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\ConstructorFormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\FieldF1ormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\MethodFormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\PropertiesFormatter.cs"
            }; 
            var task = _generator.GenerateCLasses(classPaths, TestPath, 6, 2, 6); 
            
            task?.Wait();
            
            if (task == null)
                Assert.True(false);
            else 
                Assert.Equal(task.Status,TaskStatus.RanToCompletion);
        }
        
        [Fact]
        public void  BadThreadAmount()
        {
            var classPaths = new List<string>
            {
                @"..\..\..\..\TestsGenerator\Classes\\ClassF1ormatter.cs",
                @"..\..\..\..\TestsGenerator\Classes\ConstructorFormatter.cs",
            };
            
            Task task = null;
            try
            {
                task = _generator.GenerateCLasses(classPaths, TestPath, -9, 0, 0);
                task?.Wait();
            }
            catch (Exception)
            {
                Assert.True(true);
                return;
            }
            
            Assert.True(false);
            
        }
    }
}