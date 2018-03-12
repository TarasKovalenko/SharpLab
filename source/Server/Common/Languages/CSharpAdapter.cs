using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MirrorSharp;
using MirrorSharp.Advanced;
using SharpLab.Runtime;
using SharpLab.Server.Common.Internal;
using SharpLab.Server.Compilation.Internal;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace SharpLab.Server.Common.Languages {
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
    public class CSharpAdapter : ILanguageAdapter {
        private static readonly LanguageVersion MaxLanguageVersion = Enum
            .GetValues(typeof (LanguageVersion))
            .Cast<LanguageVersion>()
            .Max();
        private static readonly ImmutableArray<string> ReleasePreprocessorSymbols = ImmutableArray.Create("__DEMO_EXPERIMENTAL__");
        private static readonly ImmutableArray<string> DebugPreprocessorSymbols = ReleasePreprocessorSymbols.Add("DEBUG");

        private readonly ImmutableList<MetadataReference> _references;
        private readonly IReadOnlyDictionary<string, string> _features;

        public CSharpAdapter(IAssemblyReferenceCollector referenceCollector, IFeatureDiscovery featureDiscovery) {
            var referencedAssemblies = referenceCollector.SlowGetAllReferencedAssembliesRecursive(
                // Essential
                NetFrameworkRuntime.AssemblyOfValueTask,
                NetFrameworkRuntime.AssemblyOfValueTuple,
                NetFrameworkRuntime.AssemblyOfSpan,
                typeof(Binder).Assembly,

                // Runtime
                typeof(JitGenericAttribute).Assembly,

                // Requested
                typeof(XDocument).Assembly, // System.Xml.Linq
                typeof(IDataReader).Assembly, // System.Data
                typeof(HttpUtility).Assembly // System.Web
            ).ToImmutableList();

            var referencedAssembliesTaskSource = new ReferencedAssembliesLoadTaskSource();
            referencedAssembliesTaskSource.Complete(referencedAssemblies);
            ReferencedAssembliesTask = referencedAssembliesTaskSource.Task;

            _references = referencedAssemblies
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                .ToImmutableList();
            _features = featureDiscovery.SlowDiscoverAll().ToDictionary(f => f, f => (string)null);
        }

        public string LanguageName => LanguageNames.CSharp;
        public ReferencedAssembliesLoadTask ReferencedAssembliesTask { get; }

        public void SlowSetup(MirrorSharpOptions options) {
            // ReSharper disable HeapView.ObjectAllocation.Evident

            options.CSharp.ParseOptions = new CSharpParseOptions(
                MaxLanguageVersion,
                preprocessorSymbols: DebugPreprocessorSymbols,
                documentationMode: DocumentationMode.Diagnose
            ).WithFeatures(_features);
            options.CSharp.CompilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic> {
                    // CS1591: Missing XML comment for publicly visible type or member
                    { "CS1591", ReportDiagnostic.Suppress }
                }
            );
            options.CSharp.MetadataReferences = _references;

            // ReSharper restore HeapView.ObjectAllocation.Evident
        }

        public void SetOptimize([NotNull] IWorkSession session, [NotNull] string optimize) {
            var project = session.Roslyn.Project;
            var parseOptions = ((CSharpParseOptions)project.ParseOptions);
            var compilationOptions = ((CSharpCompilationOptions)project.CompilationOptions);
            session.Roslyn.Project = project
                .WithParseOptions(parseOptions.WithPreprocessorSymbols(optimize == Optimize.Debug ? DebugPreprocessorSymbols : ReleasePreprocessorSymbols))
                .WithCompilationOptions(compilationOptions.WithOptimizationLevel(optimize == Optimize.Debug ? OptimizationLevel.Debug : OptimizationLevel.Release));
        }

        public void SetOptionsForTarget(IWorkSession session, string target) {
            var outputKind = target != TargetNames.Run ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication;
            var allowUnsafe = target != TargetNames.Run;

            var project = session.Roslyn.Project;
            var options = ((CSharpCompilationOptions)project.CompilationOptions);
            session.Roslyn.Project = project.WithCompilationOptions(
                options.WithOutputKind(outputKind).WithAllowUnsafe(allowUnsafe)
            );
        }

        public ImmutableArray<int> GetMethodParameterLines(IWorkSession session, int lineInMethod, int columnInMethod) {
            var declaration = RoslynAdapterHelper.FindSyntaxNodeInSession(session, lineInMethod, columnInMethod)
                ?.AncestorsAndSelf()
                .OfType<MemberDeclarationSyntax>()
                .FirstOrDefault();

            if (!(declaration is BaseMethodDeclarationSyntax method))
                return ImmutableArray<int>.Empty;

            var parameters = method.ParameterList.Parameters;
            var results = new int[parameters.Count];
            for (int i = 0; i < parameters.Count; i++) {
                results[i] = parameters[i].GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            }
            return ImmutableArray.Create(results);
        }
    }
}
