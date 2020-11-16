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

            //var genPath = Path.GetFullPath(@"..\..\..\..\TestsGenerator\Tests\GenClasses\");
            const string testPath = @"..\..\Classes\\GeneratedTests\\";
            var collection = new List<string>
            {
                @"..\..\Classes\\ClassFormatter.cs",
                @"..\..\Classes\\ConstructorFormatter.cs",
                @"..\..\Classes\\\FieldFormatter.cs",
                @"..\..\Classes\\MethodFormatter.cs",
                @"..\..\Classes\\PropertiesFormatter.cs"
            };
            var generator = new NUnitTestsGenerator();
            try
            {
                var task = generator.GenerateCLasses(collection, testPath, 1, 1,1);
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