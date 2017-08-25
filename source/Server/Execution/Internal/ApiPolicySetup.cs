﻿using System;
using System.IO;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualBasic.CompilerServices;
using System.Linq.Expressions;
using System.Reflection;
using AshMind.Extensions;
using SharpLab.Runtime.Internal;
using Unbreakable;
using Unbreakable.Policy;
using Unbreakable.Policy.Rewriters;

namespace SharpLab.Server.Execution {
    using static ApiAccess;

    public static class ApiPolicySetup {
        public static ApiPolicy CreatePolicy() => ApiPolicy.SafeDefault()
            .Namespace("System", Neutral,
                n => n.Type(typeof(Console), Neutral,
                        t => t.Member(nameof(Console.Write), Allowed)
                              .Member(nameof(Console.WriteLine), Allowed)
                              // required by F#'s printf
                              .Getter(nameof(Console.Out), Allowed)
                     ).Type(typeof(STAThreadAttribute), Allowed)
            )
            .Namespace("System.Reflection", Neutral, SetupSystemReflection)
            .Namespace("System.Linq.Expressions", Neutral, SetupSystemLinqExpressions)
            .Namespace("System.IO", Neutral,
                // required by F#'s printf
                n => n.Type(typeof(TextWriter), Neutral)
            )
            .Namespace("SharpLab.Runtime.Internal", Neutral,
                n => n.Type(typeof(Flow), Neutral,
                         t => t.Member(nameof(Flow.ReportException), Allowed, NoGuardRewriter.Default)
                               .Member(nameof(Flow.ReportLineStart), Allowed, NoGuardRewriter.Default)
                               .Member(nameof(Flow.ReportValue), Allowed, NoGuardRewriter.Default)
                     )
            )
            .Namespace("", Neutral,
                n => n.Type(typeof(SharpLabObjectExtensions), Allowed)
            )
            .Namespace("Microsoft.FSharp.Core", Neutral,
                n => n.Type(typeof(CompilationArgumentCountsAttribute), Allowed)
                      .Type(typeof(CompilationMappingAttribute), Allowed)
                      .Type(typeof(EntryPointAttribute), Allowed)
                      .Type(typeof(ExtraTopLevelOperators), Neutral,
                          t => t.Member(nameof(ExtraTopLevelOperators.CreateDictionary), Allowed, CollectedEnumerableArgumentRewriter.Default)
                                .Member(nameof(ExtraTopLevelOperators.CreateSet), Allowed, CollectedEnumerableArgumentRewriter.Default)
                                .Member(nameof(ExtraTopLevelOperators.LazyPattern), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.PrintFormat), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.PrintFormatLine), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.PrintFormatToTextWriter), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.PrintFormatLineToTextWriter), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.PrintFormatToString), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.SpliceExpression), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.SpliceUntypedExpression), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.ToByte), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.ToDouble), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.ToSByte), Allowed)
                                .Member(nameof(ExtraTopLevelOperators.ToSingle), Allowed)
                      )
                      .Type(typeof(ExtraTopLevelOperators.Checked), Allowed)
                      .Type(typeof(FSharpChoice<,>), Allowed)
                      .Type(typeof(FSharpFunc<,>), Allowed)
                      .Type(typeof(FSharpOption<>), Allowed)
                      .Type(typeof(OptimizedClosures.FSharpFunc<,,>), Allowed)
                      .Type(typeof(OptimizedClosures.FSharpFunc<,,,>), Allowed)
                      .Type(typeof(OptimizedClosures.FSharpFunc<,,,,>), Allowed)
                      .Type(typeof(Microsoft.FSharp.Core.Operators), Allowed,
                          t => t.Member("ConsoleError", Denied)
                                .Member("ConsoleIn", Denied)
                                .Member("ConsoleOut", Denied)
                                .Member("Lock", Denied)
                      )
                      .Type(typeof(PrintfFormat<,,,>), Allowed)
                      .Type(typeof(PrintfFormat<,,,,>), Allowed)
                      .Type(typeof(PrintfModule), Neutral,
                          t => t.Member(nameof(PrintfModule.PrintFormat), Allowed)
                                .Member(nameof(PrintfModule.PrintFormatLine), Allowed)
                                .Member(nameof(PrintfModule.PrintFormatToTextWriter), Allowed)
                                .Member(nameof(PrintfModule.PrintFormatLineToTextWriter), Allowed)
                        )
                        .Type(typeof(Unit), Allowed)
            )
            .Namespace("Microsoft.FSharp.Collections", Neutral,
                n => n.Type(typeof(FSharpList<>), Allowed)
            )
            .Namespace("Microsoft.VisualBasic.CompilerServices", Neutral,
                n => n.Type(typeof(StandardModuleAttribute), Allowed)
            );

        private static void SetupSystemLinqExpressions(NamespacePolicy namespacePolicy) {
            ForEachTypeInNamespaceOf<Expression>(type => {
                if (type.IsEnum) {
                    namespacePolicy.Type(type, Allowed);
                    return;
                }

                if (!type.IsSameAsOrSubclassOf<Expression>())
                    return;

                namespacePolicy.Type(type, Allowed, typeRule => {
                    foreach (var method in type.GetMethods()) {
                        if (method.Name.Contains("Compile"))
                            typeRule.Member(method.Name, Denied);
                    }
                });
            });
        }

        private static void SetupSystemReflection(NamespacePolicy namespacePolicy) {
            ForEachTypeInNamespaceOf<MemberInfo>(type => {
                if (type.IsEnum) {
                    namespacePolicy.Type(type, Allowed);
                    return;
                }

                if (!type.IsSameAsOrSubclassOf<MemberInfo>())
                    return;

                namespacePolicy.Type(type, Neutral, typeRule => {
                    foreach (var property in type.GetProperties()) {
                        if (property.Name.Contains("Handle"))
                            continue;
                        typeRule.Getter(property.Name, Allowed);
                    }
                    foreach (var method in type.GetMethods()) {
                        if (method.ReturnType.IsSameAsOrSubclassOf<MemberInfo>())
                            typeRule.Member(method.Name, Allowed);
                    }
                });
            });
        }

        private static void ForEachTypeInNamespaceOf<T>(Action<Type> action) {
            var types = typeof(T).Assembly.GetExportedTypes();
            foreach (var type in types) {
                if (type.Namespace != typeof(T).Namespace)
                    continue;

                action(type);
            }
        }
    }
}
