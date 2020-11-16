using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestsGeneratorLib
{
    public class NUnitTestsGenerator
    { 
        public NUnitTestsGenerator()
        {
            
        }

        private static readonly List<UsingDirectiveSyntax> DefaultLoadDirectiveList = new List<UsingDirectiveSyntax>()
        {
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Text")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("NUnit.Framework"))
        };

        private static readonly List<UsingDirectiveSyntax> AdditionalLoadDirectiveList =
            new List<UsingDirectiveSyntax>()
            {
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Moq")),
            };

        private const string Namespace = "    ";

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
        public Task GenerateCLasses(IEnumerable<string> classPaths, string testPath, int filesToLoadThreadAmount,
            int testToGenerateThreadAmount, int filesToWriteThreadAmount)
        {
            if (!Directory.Exists(testPath))
            {
                throw new FileNotFoundException("Path " +"\""+ testPath + "\" " + "Invalid ");
            }
            
            DirPath = testPath;
            
            var maxFilesToLoad = new ExecutionDataflowBlockOptions() {MaxDegreeOfParallelism = filesToLoadThreadAmount};
            var maxTestToGenerate = new ExecutionDataflowBlockOptions()
                {MaxDegreeOfParallelism = testToGenerateThreadAmount};
            var maxFilesToWrite = new ExecutionDataflowBlockOptions()
                {MaxDegreeOfParallelism = filesToWriteThreadAmount};

            // Create a dataflow block that takes a  path as input and return classes as text (string str)
            var loadClasses =
                new TransformBlock<string, string>(GetTextFromFile, maxFilesToLoad); // add lambda and try/catch

            // Create a dataflow block that takes a text class and return a generated Test
            var generateTests =
                new TransformBlock<string, string[]>(GetTestFromText, maxTestToGenerate); // add lambda and try/catch

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
                try
                {
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("Path " +"\""+ path + "\" " + "Invalid ");
                    }
                    
                    loadClasses.Post(path);
                }
                catch (FileNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            
            loadClasses.Complete();
         
             
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
                var fileName = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First().Identifier
                    .Text;
                var filePath = Path.Combine(DirPath, fileName + ".cs");
                //
                // using var outputFile = new StreamWriter(filePath);
                // await outputFile.WriteAsync(test);

                using var outputFile = new StreamWriter(filePath);
                await outputFile.WriteAsync(test);

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
        /// Using Roslyn, we generate classes stored in the text
        /// </summary>
        /// <param name="text"> file as text </param>
        /// <returns> Collection of class declaration </returns>
        private static IEnumerable<ClassDeclarationSyntax> GetClassesFromText(string text)
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            return tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(@class => @class.Modifiers.Any(SyntaxKind.PublicKeyword)).ToArray();
        }

        /// <summary>
        /// https://blog.zwezdin.com/2013/code-generating-with-roslyn/
        /// We create a test in the form of a tree where is the root namespace.
        /// </summary>
        /// <param name="classDeclaration"> generated test </param>
        private static string CreateTest(TypeDeclarationSyntax classDeclaration)
        {
            // create unit
            // add using
            var unit = SyntaxFactory.CompilationUnit();
            unit = DefaultLoadDirectiveList.Aggregate(unit,
                (current, loadDirective) => current.AddUsings(loadDirective));
            
            bool isStatic = classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword);

            // create class
            // add private class field
            var @class = SyntaxFactory.ClassDeclaration(
                    SyntaxFactory.Identifier(
                        SyntaxFactory.TriviaList(),
                        classDeclaration.Identifier.Text + "Test",
                        SyntaxFactory.TriviaList(
                            SyntaxFactory.CarriageReturnLineFeed)
                    )
                )
                .WithAttributeLists(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AttributeList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Attribute(
                                        SyntaxFactory.IdentifierName("TestFixture"))))
                            .WithOpenBracketToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(
                                        new[]
                                        {
                                            SyntaxFactory.CarriageReturnLineFeed,
                                            SyntaxFactory.Whitespace(Namespace)
                                        }),
                                    SyntaxKind.OpenBracketToken,
                                    SyntaxFactory.TriviaList()))
                            .WithCloseBracketToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.CloseBracketToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.CarriageReturnLineFeed)))))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(
                                SyntaxFactory.Whitespace(Namespace)
                            ),
                            SyntaxKind.PublicKeyword,
                            SyntaxFactory.TriviaList(
                                SyntaxFactory.Space)
                        )
                    )
                )
                .WithKeyword(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.ClassKeyword,
                        SyntaxFactory.TriviaList(
                            SyntaxFactory.Space)
                    )
                )
                .WithOpenBraceToken(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(
                            SyntaxFactory.Whitespace(Namespace)
                        ),
                        SyntaxKind.OpenBraceToken,
                        SyntaxFactory.TriviaList(
                            SyntaxFactory.CarriageReturnLineFeed)
                    )
                )
                .WithCloseBraceToken(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(
                            new[]
                            {
                                SyntaxFactory.Whitespace(Namespace),
                            }),
                        SyntaxKind.CloseBraceToken,
                        SyntaxFactory.TriviaList()
                    )
                );

            if (!isStatic)
            {
                @class = @class.AddMembers(
                    SyntaxFactory.FieldDeclaration(
                            SyntaxFactory.VariableDeclaration(
                                    SyntaxFactory.IdentifierName(
                                        SyntaxFactory.Identifier(
                                            SyntaxFactory.TriviaList(),
                                            classDeclaration.Identifier.Text,
                                            SyntaxFactory.TriviaList(
                                                SyntaxFactory.Space)
                                        )
                                    )
                                )
                                .WithVariables(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.VariableDeclarator(
                                            SyntaxFactory.Identifier(
                                                "_" +
                                                classDeclaration.Identifier.Text[0].ToString().ToLower() +
                                                classDeclaration.Identifier.Text.Substring(1))
                                        )
                                    )
                                )
                        )
                        .WithModifiers(
                            SyntaxFactory.TokenList(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.Whitespace(Namespace + Namespace)),
                                    SyntaxKind.PrivateKeyword,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.Space)
                                )
                            )
                        )
                        .WithSemicolonToken(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(),
                                SyntaxKind.SemicolonToken,
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(Namespace),
                                    SyntaxFactory.CarriageReturnLineFeed)
                            )
                        )
                );

                // add dependency injection 
                var holder = GetDepInjection(classDeclaration);
                if (holder != null)
                {
                    // add fileds
                    @class = @class.AddMembers(holder.FieldDeclarations.ToArray());
                    // add SetUp method
                    @class = @class.AddMembers(holder.MethodDeclarations);

                    // add using class namespace
                    unit = AdditionalLoadDirectiveList.Aggregate(unit,
                        (current, loadDirective) => current.AddUsings(loadDirective));
                }
            }

            // add methods
            @class = @class.AddMembers(AddTestMethods(classDeclaration).ToArray());

            // add namespace declaration 
            string classNamespaceName = null;

            if (SyntaxNodeHelper.TryGetParentSyntax(classDeclaration,
                out NamespaceDeclarationSyntax namespaceDeclarationSyntax))
            {
                // namespace exist
                classNamespaceName = namespaceDeclarationSyntax.Name.ToString();
                unit = unit.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(classNamespaceName)));
            }

            // add class to namespace
            var @namespace = SyntaxFactory.NamespaceDeclaration(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName(classNamespaceName ?? "Kra"),
                        SyntaxFactory.IdentifierName(
                            SyntaxFactory.Identifier(
                                SyntaxFactory.TriviaList(),
                                "Test",
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.CarriageReturnLineFeed)
                            )
                        )
                    )
                )
                .WithNamespaceKeyword(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(
                            new[]
                            {
                                SyntaxFactory.CarriageReturnLineFeed,
                                SyntaxFactory.CarriageReturnLineFeed
                            }),
                        SyntaxKind.NamespaceKeyword,
                        SyntaxFactory.TriviaList(
                            SyntaxFactory.Space)))
                .WithOpenBraceToken(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.OpenBraceToken,
                        SyntaxFactory.TriviaList(
                            SyntaxFactory.CarriageReturnLineFeed)
                    )
                )
                .WithCloseBraceToken(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(
                            SyntaxFactory.CarriageReturnLineFeed),
                        SyntaxKind.CloseBraceToken,
                        SyntaxFactory.TriviaList()
                    )
                ).AddMembers(@class);


            var test = unit.NormalizeWhitespace().AddMembers(@namespace).ToFullString();

            return test;
        }

        /// <summary>
        /// Check all constructors and find dependency injection.
        /// Exist:
        ///     - create private field
        ///     - create single setUpMethod
        ///     - add to setUpMethod body needed code.
        /// Create code using Roslyn templates 
        /// </summary>
        /// <param name="classDeclaration"></param>
        /// <returns> container storing the created SetUpMethod and created fields</returns>
        private static DeclarationContainer GetDepInjection(TypeDeclarationSyntax classDeclaration)
        {
            var constructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>();
            var regex = new Regex("I[A-Z]{1}.+");

            var fieldList = new List<FieldDeclarationSyntax>();
            var memberList = new List<ExpressionStatementSyntax>();

            foreach (var constructor in constructors)
            {
                var dependencyParam = constructor.ParameterList.Parameters.Where(parameter =>
                    parameter.Type != null && regex.IsMatch(parameter.Type.ToFullString())).ToList();

                if (dependencyParam.Count == 0) continue;


                foreach (var parameter in dependencyParam)
                {
                    fieldList.Add(
                        SyntaxFactory.FieldDeclaration(
                                SyntaxFactory.VariableDeclaration(
                                        SyntaxFactory.GenericName(
                                                SyntaxFactory.Identifier("Mock"))
                                            .WithTypeArgumentList(
                                                SyntaxFactory.TypeArgumentList(
                                                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                        SyntaxFactory.IdentifierName(parameter.Type.ToFullString()
                                                        )
                                                    )
                                                )
                                            )
                                    )
                                    .WithVariables(
                                        SyntaxFactory.SingletonSeparatedList(
                                            SyntaxFactory.VariableDeclarator(
                                                SyntaxFactory.Identifier("_" + parameter.Identifier.Text)
                                            )
                                        )
                                    )
                            )
                            .WithModifiers(
                                SyntaxFactory.TokenList(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.Whitespace(Namespace + Namespace)
                                        ),
                                        SyntaxKind.PrivateKeyword,
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.Space
                                        )
                                    )
                                )
                            )
                            .WithSemicolonToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.SemicolonToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.LineFeed, SyntaxFactory.LineFeed)
                                )
                            )
                    );

                    memberList.Add(
                        SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        SyntaxFactory.IdentifierName(
                                            SyntaxFactory.Identifier(
                                                SyntaxFactory.TriviaList(
                                                    SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)
                                                ),
                                                "_" + parameter.Identifier.Text,
                                                SyntaxFactory.TriviaList(
                                                    SyntaxFactory.Space
                                                )
                                            )
                                        ),
                                        SyntaxFactory.ObjectCreationExpression(
                                                SyntaxFactory.GenericName(
                                                        SyntaxFactory.Identifier("Mock")
                                                    )
                                                    .WithTypeArgumentList(
                                                        SyntaxFactory.TypeArgumentList(
                                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                                SyntaxFactory.IdentifierName(parameter.Type.ToString())
                                                            )
                                                        )
                                                    )
                                            )
                                            .WithNewKeyword(
                                                SyntaxFactory.Token(
                                                    SyntaxFactory.TriviaList(),
                                                    SyntaxKind.NewKeyword,
                                                    SyntaxFactory.TriviaList(
                                                        SyntaxFactory.Space
                                                    )
                                                )
                                            )
                                            .WithArgumentList(
                                                SyntaxFactory.ArgumentList()
                                            )
                                    )
                                    .WithOperatorToken(
                                        SyntaxFactory.Token(
                                            SyntaxFactory.TriviaList(),
                                            SyntaxKind.EqualsToken,
                                            SyntaxFactory.TriviaList(
                                                SyntaxFactory.Space
                                            )
                                        )
                                    )
                            )
                            .WithSemicolonToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.SemicolonToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.LineFeed
                                    )
                                )
                            )
                    );

                    memberList.Add(
                        SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        SyntaxFactory.IdentifierName(
                                            SyntaxFactory.Identifier(
                                                SyntaxFactory.TriviaList(
                                                    SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)
                                                ),
                                                "_" + 
                                                classDeclaration.Identifier.Text[0].ToString().ToLower() +
                                                classDeclaration.Identifier.Text.Substring(1),
                                                SyntaxFactory.TriviaList(
                                                    SyntaxFactory.Space
                                                )
                                            )
                                        ),
                                        SyntaxFactory.ObjectCreationExpression(
                                                SyntaxFactory.IdentifierName(classDeclaration.Identifier.Text)
                                            )
                                            .WithNewKeyword(
                                                SyntaxFactory.Token(
                                                    SyntaxFactory.TriviaList(),
                                                    SyntaxKind.NewKeyword,
                                                    SyntaxFactory.TriviaList(
                                                        SyntaxFactory.Space
                                                    )
                                                )
                                            )
                                            .WithArgumentList(
                                                SyntaxFactory.ArgumentList(
                                                    SyntaxFactory.SingletonSeparatedList(
                                                        SyntaxFactory.Argument(
                                                            SyntaxFactory.MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                SyntaxFactory.IdentifierName(
                                                                    "_" + parameter.Identifier.Text),
                                                                SyntaxFactory.IdentifierName("Object")
                                                            )
                                                        )
                                                    )
                                                )
                                            )
                                    )
                                    .WithOperatorToken(
                                        SyntaxFactory.Token(
                                            SyntaxFactory.TriviaList(),
                                            SyntaxKind.EqualsToken,
                                            SyntaxFactory.TriviaList(
                                                SyntaxFactory.Space
                                            )
                                        )
                                    )
                            )
                            .WithSemicolonToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.SemicolonToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.LineFeed
                                    )
                                )
                            )
                    );
                }
            }

            if (fieldList.Count == 0) return null;

            var setUpMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.VoidKeyword,
                            SyntaxFactory.TriviaList(
                                SyntaxFactory.Space
                            )
                        )
                    ),
                    SyntaxFactory.Identifier("SetUp")
                )
                .WithAttributeLists(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AttributeList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Attribute(
                                        SyntaxFactory.IdentifierName("SetUp")
                                    )
                                )
                            )
                            .WithOpenBracketToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.Whitespace(Namespace + Namespace)
                                    ),
                                    SyntaxKind.OpenBracketToken,
                                    SyntaxFactory.TriviaList()
                                )
                            )
                            .WithCloseBracketToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.CloseBracketToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.LineFeed
                                    )
                                )
                            )
                    )
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(
                                SyntaxFactory.Whitespace(Namespace + Namespace)
                            ),
                            SyntaxKind.PublicKeyword,
                            SyntaxFactory.TriviaList(
                                SyntaxFactory.Space
                            )
                        )
                    )
                )
                .WithParameterList(
                    SyntaxFactory.ParameterList()
                        .WithCloseParenToken(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(),
                                SyntaxKind.CloseParenToken,
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.LineFeed
                                )
                            )
                        )
                )
                .WithBody(
                    SyntaxFactory.Block(memberList)
                        .WithOpenBraceToken(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(Namespace + Namespace)
                                ),
                                SyntaxKind.OpenBraceToken,
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.LineFeed
                                )
                            )
                        )
                        .WithCloseBraceToken(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(Namespace + Namespace)
                                ),
                                SyntaxKind.CloseBraceToken,
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(Namespace + Namespace),
                                    SyntaxFactory.LineFeed,
                                    SyntaxFactory.Whitespace(""),
                                    SyntaxFactory.LineFeed
                                )
                            )
                        )
                );

            return new DeclarationContainer(fieldList, setUpMethod);
        }

        /// <summary>
        /// creating tests methods for all public methods using a template Arrange/Act/Assert
        /// no template validation of void methods
        /// </summary>
        /// <param name="classDeclaration"></param>
        /// <returns> list with created methods </returns>
        private static List<MethodDeclarationSyntax> AddTestMethods(BaseTypeDeclarationSyntax classDeclaration)
        {
            var methods = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Where(@method => @method.Modifiers.Any(SyntaxKind.PublicKeyword));

            var methodList = new List<MethodDeclarationSyntax>();

            foreach (var method in methods)
            {
                var arrange = new List<LocalDeclarationStatementSyntax>();
                var paramList = new List<SyntaxNodeOrToken>();

                foreach (var parameter in method.ParameterList.Parameters)
                {
                    arrange.Add(SyntaxFactory.LocalDeclarationStatement(
                            SyntaxFactory.VariableDeclaration(
                                    SyntaxFactory.IdentifierName(
                                        SyntaxFactory.Identifier(
                                            SyntaxFactory.TriviaList(
                                                SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)),
                                            parameter.Type.ToString(),
                                            SyntaxFactory.TriviaList(
                                                SyntaxFactory.Space)
                                        )
                                    )
                                )
                                .WithVariables(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.VariableDeclarator(
                                                SyntaxFactory.Identifier(
                                                    SyntaxFactory.TriviaList(),
                                                    parameter.Identifier.Text,
                                                    SyntaxFactory.TriviaList(
                                                        SyntaxFactory.Space)
                                                )
                                            )
                                            .WithInitializer(
                                                SyntaxFactory.EqualsValueClause(
                                                        SyntaxFactory.LiteralExpression(
                                                            SyntaxKind.DefaultLiteralExpression,
                                                            SyntaxFactory.Token(SyntaxKind.DefaultKeyword)))
                                                    .WithEqualsToken(
                                                        SyntaxFactory.Token(
                                                            SyntaxFactory.TriviaList(),
                                                            SyntaxKind.EqualsToken,
                                                            SyntaxFactory.TriviaList(
                                                                SyntaxFactory.Space)
                                                        )
                                                    )
                                            )
                                    )
                                )
                        )
                        .WithSemicolonToken(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(),
                                SyntaxKind.SemicolonToken,
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.LineFeed
                                )
                            )
                        )
                    );

                    paramList.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier.Text)));

                    if (!parameter.Equals(method.ParameterList.Parameters.Last()))
                    {
                        paramList.Add(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.CommaToken,
                            SyntaxFactory.TriviaList(SyntaxFactory.Space)));
                    }
                }


                var member = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(),
                                SyntaxKind.VoidKeyword,
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Space
                                )
                            )
                        ),
                        SyntaxFactory.Identifier(method.Identifier.Text + "Test")
                    )
                    .WithAttributeLists(
                        SyntaxFactory.SingletonList(
                            SyntaxFactory.AttributeList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Attribute(
                                            SyntaxFactory.IdentifierName("Test")
                                        )
                                    )
                                )
                                .WithOpenBracketToken(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.Whitespace(Namespace + Namespace)
                                        ),
                                        SyntaxKind.OpenBracketToken,
                                        SyntaxFactory.TriviaList()
                                    )
                                )
                                .WithCloseBracketToken(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.CloseBracketToken,
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.LineFeed
                                        )
                                    )
                                )
                        )
                    )
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(Namespace + Namespace)
                                ),
                                SyntaxKind.PublicKeyword,
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Space
                                )
                            )
                        )
                    )
                    .WithParameterList(
                        SyntaxFactory.ParameterList()
                            .WithCloseParenToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(),
                                    SyntaxKind.CloseParenToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.LineFeed
                                    )
                                )
                            )
                    )
                    .WithBody(
                        SyntaxFactory.Block(arrange)
                            .WithOpenBraceToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.Whitespace(Namespace + Namespace)
                                    ),
                                    SyntaxKind.OpenBraceToken,
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.LineFeed
                                    )
                                )
                            )
                            .WithCloseBraceToken(
                                SyntaxFactory.Token(
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.Whitespace(Namespace + Namespace)
                                    ),
                                    SyntaxKind.CloseBraceToken,
                                    SyntaxFactory.TriviaList(
                                        new[]
                                        {
                                            SyntaxFactory.LineFeed,
                                            SyntaxFactory.Whitespace(""),
                                            SyntaxFactory.LineFeed
                                        }
                                    )
                                )
                            )
                    );
                
                var fieldName = classDeclaration.Identifier.Text;
                if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
                    fieldName = "_" + fieldName[0].ToString().ToLower() + fieldName.Substring(1);
                
                if (!method.ReturnType.ToString().Equals("void"))
                {
                    // act
                    member = member.AddBodyStatements(
                            SyntaxFactory.LocalDeclarationStatement(
                                SyntaxFactory.VariableDeclaration(
                                        SyntaxFactory.IdentifierName(
                                            SyntaxFactory.Identifier(
                                                SyntaxFactory.TriviaList(
                                                    new[]
                                                    {
                                                        SyntaxFactory.LineFeed,
                                                        SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)
                                                    }
                                                ),
                                                method.ReturnType.ToString(),
                                                SyntaxFactory.TriviaList(
                                                    SyntaxFactory.Space)
                                            )
                                        )
                                    )
                                    .WithVariables(
                                        SyntaxFactory.SingletonSeparatedList(
                                            SyntaxFactory.VariableDeclarator(
                                                    SyntaxFactory.Identifier("actual"))
                                                .WithInitializer(
                                                    SyntaxFactory.EqualsValueClause(
                                                        SyntaxFactory.InvocationExpression(
                                                                SyntaxFactory.MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    SyntaxFactory.IdentifierName(
                                                                        fieldName),
                                                                    SyntaxFactory.IdentifierName(method.Identifier.Text)
                                                                )
                                                            )
                                                            .WithArgumentList(
                                                                SyntaxFactory.ArgumentList(
                                                                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                                        paramList.ToArray()
                                                                    )
                                                                )
                                                            )
                                                    )
                                                )
                                        )
                                    )
                            )
                        )
                        .AddBodyStatements( // assert
                            SyntaxFactory.LocalDeclarationStatement(
                                    SyntaxFactory.VariableDeclaration(
                                            SyntaxFactory.IdentifierName(
                                                SyntaxFactory.Identifier(
                                                    SyntaxFactory.TriviaList(
                                                        new[]
                                                        {
                                                            SyntaxFactory.LineFeed,
                                                            SyntaxFactory.LineFeed,
                                                            SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)
                                                        }
                                                    ),
                                                    method.ReturnType.ToString(),
                                                    SyntaxFactory.TriviaList(
                                                        SyntaxFactory.Space)
                                                )
                                            )
                                        )
                                        .WithVariables(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.VariableDeclarator(
                                                        SyntaxFactory.Identifier(
                                                            SyntaxFactory.TriviaList(),
                                                            "expected",
                                                            SyntaxFactory.TriviaList(
                                                                SyntaxFactory.Space
                                                            )
                                                        )
                                                    )
                                                    .WithInitializer(
                                                        SyntaxFactory.EqualsValueClause(
                                                            SyntaxFactory.LiteralExpression(
                                                                SyntaxKind.DefaultLiteralExpression,
                                                                SyntaxFactory.Token(SyntaxKind.DefaultKeyword)
                                                                )
                                                            )
                                                            .WithEqualsToken(
                                                                SyntaxFactory.Token(
                                                                    SyntaxFactory.TriviaList(),
                                                                    SyntaxKind.EqualsToken,
                                                                    SyntaxFactory.TriviaList(
                                                                        SyntaxFactory.Space
                                                                    )
                                                                )
                                                            )
                                                    )
                                            )
                                        )
                                )
                                .WithSemicolonToken(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.SemicolonToken,
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.LineFeed
                                        )
                                    )
                                )
                        )
                        .AddBodyStatements(
                            SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName(
                                                    SyntaxFactory.Identifier(
                                                        SyntaxFactory.TriviaList(
                                                            SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)
                                                        ),
                                                        "Assert",
                                                        SyntaxFactory.TriviaList()
                                                    )
                                                ),
                                                SyntaxFactory.IdentifierName("That")
                                            )
                                        )
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                    new SyntaxNodeOrToken[]
                                                    {
                                                        SyntaxFactory.Argument(
                                                            SyntaxFactory.IdentifierName("actual")
                                                        ),
                                                        SyntaxFactory.Token(
                                                            SyntaxFactory.TriviaList(),
                                                            SyntaxKind.CommaToken,
                                                            SyntaxFactory.TriviaList(
                                                                SyntaxFactory.Space
                                                            )
                                                        ),
                                                        SyntaxFactory.Argument(
                                                            SyntaxFactory.InvocationExpression(
                                                                    SyntaxFactory.MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        SyntaxFactory.IdentifierName("Is"),
                                                                        SyntaxFactory.IdentifierName("EqualTo")
                                                                    )
                                                                )
                                                                .WithArgumentList(
                                                                    SyntaxFactory.ArgumentList(
                                                                        SyntaxFactory
                                                                            .SingletonSeparatedList(
                                                                                SyntaxFactory.Argument(
                                                                                    SyntaxFactory.IdentifierName(
                                                                                        "expected")
                                                                                )
                                                                            )
                                                                    )
                                                                )
                                                        )
                                                    }
                                                )
                                            )
                                        )
                                )
                                .WithSemicolonToken(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.SemicolonToken,
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.LineFeed
                                        )
                                    )
                                )
                        )
                        .AddBodyStatements(
                            SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName(
                                                    SyntaxFactory.Identifier(
                                                        SyntaxFactory.TriviaList(
                                                            SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)
                                                        ),
                                                        "Assert",
                                                        SyntaxFactory.TriviaList()
                                                    )
                                                ),
                                                SyntaxFactory.IdentifierName("Fail")
                                            )
                                        )
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SingletonSeparatedList(
                                                    SyntaxFactory.Argument(
                                                        SyntaxFactory.LiteralExpression(
                                                            SyntaxKind.StringLiteralExpression,
                                                            SyntaxFactory.Literal("autogenerated")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                )
                                .WithSemicolonToken(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.SemicolonToken,
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.LineFeed
                                        )
                                    )
                                )
                        );
                }
                else
                {
                    member = member.AddBodyStatements(
                            SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName(
                                                    SyntaxFactory.Identifier(
                                                        SyntaxFactory.TriviaList(
                                                            new[]
                                                            {
                                                                SyntaxFactory.CarriageReturnLineFeed,
                                                                SyntaxFactory.Whitespace(
                                                                    Namespace + Namespace + Namespace)
                                                            }),
                                                        fieldName,
                                                        SyntaxFactory.TriviaList())),
                                                SyntaxFactory.IdentifierName(method.Identifier.Text)))
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                    paramList.ToArray()
                                                )
                                            )
                                        )
                                )
                                .WithSemicolonToken(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.SemicolonToken,
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.CarriageReturnLineFeed)
                                    )
                                )
                        )
                        .AddBodyStatements(
                            SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName(
                                                    SyntaxFactory.Identifier(
                                                        SyntaxFactory.TriviaList(
                                                            SyntaxFactory.Whitespace(Namespace + Namespace + Namespace)
                                                        ),
                                                        "Assert",
                                                        SyntaxFactory.TriviaList()
                                                    )
                                                ),
                                                SyntaxFactory.IdentifierName("Fail")
                                            )
                                        )
                                        .WithArgumentList(
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SingletonSeparatedList(
                                                    SyntaxFactory.Argument(
                                                        SyntaxFactory.LiteralExpression(
                                                            SyntaxKind.StringLiteralExpression,
                                                            SyntaxFactory.Literal("autogenerated")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                )
                                .WithSemicolonToken(
                                    SyntaxFactory.Token(
                                        SyntaxFactory.TriviaList(),
                                        SyntaxKind.SemicolonToken,
                                        SyntaxFactory.TriviaList(
                                            SyntaxFactory.LineFeed
                                        )
                                    )
                                )
                        );
                }

                methodList.Add(member);
            }

            return methodList;
        }
    }
}