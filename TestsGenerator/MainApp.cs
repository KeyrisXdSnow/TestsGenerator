using System.Collections.Generic;
using TestsGeneratorLib;

namespace TestsGenerator
{
    internal class MainApp
    {
        public static void Main(string[] args)
        {
            var collection = new List<string>
            {
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\ClassFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\ConstructorFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\FieldFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\MethodFormatter.cs",
                "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\PropertiesFormatter.cs"
            };

            // for (int i = 0; i < 10; i++)
            // {
            //     stack.Add(i.ToString());
            // }

            var generator = new TestsGeneratorLib.TestsGenerator(4,4,4);
            generator.GenerateCLasses(collection, "E:\\Sharaga\\SPP\\TestsGenerator\\TestsGenerator\\Classes\\Tests\\");



        }
    }
}