
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestsGeneratorLib
{
   
    public static class TestsGenerator
    {
        private static string DirPath { get; set; } 
        /// <summary>
        /// Generates test classes using Dataflow API:. A 3 block conveyor belt is used :
        /// 1. parallel loading of sources into memory
        /// 2. generating test classes in multi-threaded mode
        /// 3. parallel writing of results to disk
        /// Execution Dataflow Block Options are used to configure the number of running threads in a pipeline block.
        /// First, all dataflow blocks are described, after which they must be linked using the method LinkTo.
        /// To terminate the pipeline, call the method Completion.
        /// </summary>
        /// <param name="classPaths"> a list of files for classes from which test classes should be generated </param>
        /// <param name="testPath"> path to the folder for writing the created files</param>
        /// <param name="filesToLoadThreadAmount"> limit on the number of files uploaded at a time </param>
        /// <param name="testToGenerateThreadAmount"> limit on the max number of simultaneously processed tasks </param>
        /// <param name="filesToWriteThreadAmount"> limitation on simultaneously recorded files </param>
        /// <returns>
        /// files with test classes (one test class per file, regardless of how the tested classes were located in the source files);
        /// all generated test classes are compiled when included in a separate project, in which there is a link to the project with the tested classes;
        /// all generated tests fail.
        /// </returns>
        public static Task GenerateCLasses(IEnumerable<string> classPaths, string testPath, int filesToLoadThreadAmount, int testToGenerateThreadAmount, int filesToWriteThreadAmount)
        {

            if (!Directory.Exists(DirPath))
            {
                Console.WriteLine(new FileNotFoundException().Message);
                return Task.CompletedTask;
            }

            DirPath = testPath;
            var maxFilesToLoad = new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = filesToLoadThreadAmount };
            var maxTestToGenerate = new ExecutionDataflowBlockOptions() {MaxDegreeOfParallelism = testToGenerateThreadAmount};
            var maxFilesToWrite = new ExecutionDataflowBlockOptions() {MaxDegreeOfParallelism = filesToWriteThreadAmount};

            // Create a dataflow block that takes a  path as input and return classes as text (string str)
            var loadClasses = new TransformBlock<string, string> (GetTextFromFile,maxFilesToLoad); // add lambda and try/catch
            
            // Create a dataflow block that takes a text class and return a generated Test
            var generateTests = new TransformBlock<string, string[]>(GetTestFromText, maxTestToGenerate); // add lambda and try/catch
            
            // Create a dataflow block that save generated Test on disk 
            var writeTests = new ActionBlock<string[]>(WriteTests, maxFilesToWrite); // add lambda and try/catch
            
            //
            // Connect the dataflow blocks to form a pipeline.
            //
            
            var linkOption = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };
            
            loadClasses.LinkTo(generateTests, linkOption);
            generateTests.LinkTo(writeTests, linkOption);

            foreach (var path in classPaths)
            {
                loadClasses.Post(path);
            }
            
            // Mark the head of the pipeline as complete. 
            loadClasses.Completion.Wait(); // пока wait ибо классы просто не успевают генерится 
            return writeTests.Completion;
        }

        /// <summary>
        /// Reading a file asynchronously via a buffer
        /// </summary>
        /// <param name="path"> file path of the stored classes </param>
        /// <returns> class as text </returns>
        private static async Task<string> GetTextFromFile(string path)
        {
            using var sourceStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);

            var sb = new StringBuilder();

            var buffer = new byte[0x1000];
            int numRead;
            
            
            while ((numRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                var text = Encoding.UTF8.GetString(buffer, 0, numRead);
                sb.Append(text);
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// Asynchronous file write via streams
        /// </summary>
        /// <param name="tests"> array of string representation of the generated tests </param>
        /// <returns> completed task </returns>
        private static async Task WriteTests(string[] tests)
        {
            
            foreach (var test in tests)
            {
                var tree = CSharpSyntaxTree.ParseText(test);
                var fileName =tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First().Identifier.Text;
                var filePath = Path.Combine( DirPath,fileName+".cs");
                
                using var outputFile = new StreamWriter(filePath);
                await outputFile.WriteAsync(test);
                
                // var encodedText = Encoding.Unicode.GetBytes(test);

                //
                // using var sourceStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                //     bufferSize: 4096, useAsync: true);
                //
                // await sourceStream.WriteAsync(encodedText, 0, encodedText.Length); 
                // mmm работает через жопу, одобряю такую документацию 
            }
        }
        
        /// <summary>
        /// Generating classes directly.
        /// 1. Generating classes declaration from the text
        /// 2. Based on classes declaration generate Tests
        /// 3. Store Tests on thread-safe BlockingCollection. Used when some threads fill a collection and others
        /// retrieve items from it. If at the moment of requesting the next element the collection is empty,
        /// then the reading side goes into the state of waiting for a new element (polling). 
        /// </summary>
        /// <param name="text"></param>
        /// <returns> array of generated tests</returns>
        private static string[] GetTestFromText(string text)
        {
            var classes = GetClassesFromText(text);
            var tests = new BlockingCollection<string>();
            foreach (var classDeclaration in classes)
            {
               tests.Add(CreateTest(classDeclaration));
            }
            return tests.ToArray();
        }

        /// <summary>
        /// https://blog.zwezdin.com/2013/code-generating-with-roslyn/
        /// We create a test in the form of a tree where is the root namespace.
        /// </summary>
        /// <param name="classDeclaration"> generated test </param>
        private static string CreateTest(BaseTypeDeclarationSyntax classDeclaration)
        {
            // add methods
            var methods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
            var members = new List<MethodDeclarationSyntax>();
            
            foreach (var method in methods)
            {
                var methodBody = SyntaxFactory.ParseStatement("Assert.Fail(\"autogenerated\");");
                
                var member = SyntaxFactory
                    .MethodDeclaration(SyntaxFactory.ParseName("void"), method.Identifier.Text + "Test")
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword)).AddTypeParameterListParameters()
                    .WithBody(SyntaxFactory.Block(methodBody))
                    .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Test")))));
                members.Add(member);
            }
            
            // add class declaration
            var @class = SyntaxFactory.ClassDeclaration(classDeclaration.Identifier.Text + "Test")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("TestFixture"))))).
                AddMembers(members.ToArray());
            
            
            // add namespace declaration 
            string classNamespaceName = null;
            
            if (!SyntaxNodeHelper.TryGetParentSyntax(classDeclaration, out NamespaceDeclarationSyntax namespaceDeclarationSyntax))
            {
                classNamespaceName = namespaceDeclarationSyntax.Name.ToString();
            }
            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(classNamespaceName ?? "Kra" + ".Test")).AddMembers(@class);

            var test = SyntaxFactory.CompilationUnit()
                .AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Text")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("NUnit.Framework"))
                )
                .AddMembers(@namespace)
                .NormalizeWhitespace()
                .ToFullString();
            
            return test;
        }

        /// <summary>
        /// Using Roslyn, we generate classes stored in the text
        /// </summary>
        /// <param name="text"> file as text </param>
        /// <returns> Collection of class declaration </returns>
        private static IEnumerable<ClassDeclarationSyntax> GetClassesFromText(string text)
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            return tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
        }
        
        

    }
    
}