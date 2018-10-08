using System;
using System.IO;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.Metadata;
using SharpLab.Server.Common;
using SharpLab.Server.Decompilation.Internal;

namespace SharpLab.Server.Decompilation {
    public class CSharpDecompiler : IDecompiler {
        private static readonly CSharpFormattingOptions FormattingOptions = CreateFormattingOptions();
        private static readonly DecompilerSettings DecompilerSettings = new DecompilerSettings {
            AnonymousMethods = false,
            AnonymousTypes = false,
            YieldReturn = false,
            AsyncAwait = false,
            AutomaticProperties = false,
            ExpressionTrees = false,
            ArrayInitializers = false,
            ObjectOrCollectionInitializers = false,
            UsingStatement = false,
            LiftNullables = false,
            NullPropagation = false,
            DecimalConstants = false,
            AutomaticEvents = false
        };

        private readonly IAssemblyResolver _assemblyResolver;
        private readonly Func<Stream, IDisposableDebugInfoProvider> _debugInfoFactory;

        public CSharpDecompiler(IAssemblyResolver assemblyResolver, Func<Stream, IDisposableDebugInfoProvider> debugInfoFactory) {
            _assemblyResolver = assemblyResolver;
            _debugInfoFactory = debugInfoFactory;
        }

        public void Decompile(CompilationStreamPair streams, TextWriter codeWriter) {
            Argument.NotNull(nameof(streams), streams);
            Argument.NotNull(nameof(codeWriter), codeWriter);

            using (var assemblyFile = new PEFile("", streams.AssemblyStream))
            using (var debugInfo = streams.SymbolStream != null ? _debugInfoFactory(streams.SymbolStream) : null) {
                var decompiler = new ICSharpCode.Decompiler.CSharp.CSharpDecompiler(assemblyFile, _assemblyResolver, DecompilerSettings) {
                    DebugInfoProvider = debugInfo
                };
                var syntaxTree = decompiler.DecompileWholeModuleAsSingleFile();

                new CSharpOutputVisitor(codeWriter, FormattingOptions).VisitSyntaxTree(syntaxTree);
            }
        }

        public string LanguageName => TargetNames.CSharp;

        private static CSharpFormattingOptions CreateFormattingOptions()
        {
            var options = FormattingOptionsFactory.CreateAllman();
            options.IndentationString = "    ";
            return options;
        }
    }
}