﻿extern alias analysis;
// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using analysis::Microsoft.PythonTools.Interpreter;
using analysis::Microsoft.PythonTools.Interpreter.Ast;
using analysis::Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class AstAnalysisTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: true);
        }

        #region Test cases

        [TestMethod, Priority(0)]
        public void AstClasses() {
            var mod = Parse("Classes.py", PythonLanguageVersion.V35);
            AssertUtil.ContainsExactly(mod.GetMemberNames(null),
                "C1", "C2", "C3", "C4",
                "D", "E",
                "F1",
                "f"
            );

            Assert.IsInstanceOfType(mod.GetMember(null, "C1"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "C2"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "C3"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "C4"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "D"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "E"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "F1"), typeof(AstPythonType));
            Assert.IsInstanceOfType(mod.GetMember(null, "f"), typeof(AstPythonFunction));

            var F1 = (IMemberContainer)mod.GetMember(null, "F1");
            AssertUtil.ContainsExactly(F1.GetMemberNames(null),
                "F2", "F3", "__class__"
            );

            Assert.IsInstanceOfType(F1.GetMember(null, "F2"), typeof(AstPythonType));
            Assert.IsInstanceOfType(F1.GetMember(null, "F3"), typeof(AstPythonType));
            Assert.IsInstanceOfType(F1.GetMember(null, "__class__"), typeof(AstPythonType));
        }

        [TestMethod, Priority(0)]
        public void AstFunctions() {
            var mod = Parse("Functions.py", PythonLanguageVersion.V35);
            AssertUtil.ContainsExactly(mod.GetMemberNames(null),
                "f", "g", "h",
                "C"
            );

            Assert.IsInstanceOfType(mod.GetMember(null, "f"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(mod.GetMember(null, "g"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(mod.GetMember(null, "h"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(mod.GetMember(null, "C"), typeof(AstPythonType));

            var f = (IPythonFunction)mod.GetMember(null, "f");
            Assert.AreEqual("f", f.Documentation);

            var C = (IMemberContainer)mod.GetMember(null, "C");
            AssertUtil.ContainsExactly(C.GetMemberNames(null),
                "i", "j", "C2", "__class__"
            );

            Assert.IsInstanceOfType(C.GetMember(null, "i"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(C.GetMember(null, "j"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(C.GetMember(null, "C2"), typeof(AstPythonType));
            Assert.IsInstanceOfType(C.GetMember(null, "__class__"), typeof(AstPythonType));

            var C2 = (IMemberContainer)C.GetMember(null, "C2");
            AssertUtil.ContainsExactly(C2.GetMemberNames(null),
                "k", "__class__"
            );

            Assert.IsInstanceOfType(C2.GetMember(null, "k"), typeof(AstPythonFunction));
            Assert.IsInstanceOfType(C2.GetMember(null, "__class__"), typeof(AstPythonType));
        }


        private static IPythonModule Parse(string path, PythonLanguageVersion version) {
            var interpreter = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion()).CreateInterpreter();
            if (!Path.IsPathRooted(path)) {
                path = TestData.GetPath(Path.Combine("TestData", "AstAnalysis", path));
            }
            return AstPythonModule.FromFile(interpreter, path, version);
        }

        #endregion

        #region Black-box sanity tests
        // "Do we crash?"

        [TestMethod, TestCategory("10s"), Priority(1)]
        public void FullStdLibV35() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V35);
            FullStdLibTest(v);
        }

        [TestMethod, TestCategory("10s"), Priority(1)]
        public void FullStdLibV36() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V36);
            FullStdLibTest(v);
        }

        [TestMethod, TestCategory("10s"), Priority(1)]
        public void FullStdLibV27() {
            var v = PythonPaths.Versions.FirstOrDefault(pv => pv.Version == PythonLanguageVersion.V27);
            FullStdLibTest(v);
        }

        private static void FullStdLibTest(PythonVersion v) {
            v.AssertInstalled();
            var modules = ModulePath.GetModulesInLib(v.PrefixPath).ToList();
            var paths = modules.Select(m => m.LibraryPath).Distinct().ToArray();

            bool anySuccess = false;

            using (var analyzer = new PythonAnalysis(v.Version)) {
                analyzer.SetSearchPaths(paths);

                foreach (var modName in modules) {
                    if (modName.IsCompiled || modName.IsNativeExtension) {
                        continue;
                    }
                    var mod = analyzer.Analyzer.Interpreter.ImportModule(modName.ModuleName);
                    if (mod == null) {
                        Trace.TraceWarning("failed to import {0} from {1}".FormatInvariant(modName.ModuleName, modName.SourceFile));
                    } else {
                        anySuccess = true;
                        mod.GetMemberNames(analyzer.ModuleContext).ToList();
                    }
                }
            }
            Assert.IsTrue(anySuccess, "failed to import any modules at all");
        }

        #endregion
    }
}
