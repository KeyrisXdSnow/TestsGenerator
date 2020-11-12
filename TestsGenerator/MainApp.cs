using System.Collections.Generic;
using TestsGeneratorLib;

namespace TestsGenerator
{
    internal class MainApp
    {
        public static void Main(string[] args)
        {
            Stack<string> stack = new Stack<string>() ;

            for (int i = 0; i < 10; i++)
            {
                stack.Push(i.ToString());
            }
            FileReader reader = new FileReader(5);
            reader.LoadClasses(stack);
            


        }
    }
}