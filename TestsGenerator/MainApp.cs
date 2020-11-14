using System;
using System.Collections.Generic;

namespace TestsGenerator
{
    internal static class MainApp
    {
        public static void Main()
        {
            const string testPath = "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\Tests\\";
            var collection = new List<string>
            {
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\ClassFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\ConstructorFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\FieldFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\MethodFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\PropertiesFormatter.cs"
            };
            TestsGeneratorLib.TestsGenerator.GenerateCLasses(collection,testPath,1,1,1);
            Console.WriteLine("Finish");
        }
    }
}