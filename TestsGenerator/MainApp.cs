using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestsGeneratorLib;

namespace TestsGenerator
{
    internal static class MainApp
    {
        public static void Main()
        {

            var path = Path.GetFullPath(@"..\..\..\..\TestsGenerator\Classes\GeneratedTests\");
            const string testPath = @"..\..\Classes\\GeneratedTests\\";
            var collection = new List<string>
            {
                @"..\..\Classes\\ClassFormatter.cs",
                @"..\..\Classes\\ConstructorFormatter.cs",
                @"..\..\Classes\\\FieldFormatter.cs",
                @"..\..\Classes\\MethodFormatter.cs",
                @"..\..\Classes\\PropertiesFormatter.cs"
            };
            Task task = null;
            var generator = new NUnitTestsGenerator();
            try
            {
                task = generator.GenerateCLasses(collection, testPath, 6, 2,6);
                task?.Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            
            Console.WriteLine("Finish");
        }
    }
}