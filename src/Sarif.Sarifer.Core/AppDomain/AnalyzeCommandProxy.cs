// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Sarif.Driver;
using Microsoft.CodeAnalysis.Sarif.PatternMatcher;
using Microsoft.CodeAnalysis.Sarif.Writers;

namespace Microsoft.CodeAnalysis.Sarif.Sarifer
{
    internal class AnalyzeCommandProxy : MarshalByRefObject
    {
        private ISariferOption option;

        public string ToolName => "Spam";

        public string ToolVersion => "0.1.0";

        public string ToolSemanticVersion => "0.1.0";

        internal ISariferOption ExtensionOption
        {
            get
            {
                this.option ??= SariferOption.Instance;
                return option;
            }
        }

        internal string DoWork(string solutionDirectory, string filePath, string fileText, ITokenSource cancellationTokenSource)
        {
            FileSystem fileSystem = FileSystem.Instance;

            string rulesFolder = Path.Combine(solutionDirectory, Constants.RulesFolderName);

            IEnumerable<string> regexDefinitions = fileSystem.DirectoryGetFiles(rulesFolder, "*.json");

            ISet<Skimmer<AnalyzeContext>> skimmers = this.LoadRuleDefinitions(fileSystem, regexDefinitions);

            if (skimmers?.Any() != true)
            {
                return null;
            }

            var files = new Dictionary<string, string> { { filePath, fileText } };

            return this.AnalyzeCore(files, skimmers, cancellationTokenSource, fileSystem);
        }

        internal string DoWork(string solutionDirectory, IEnumerable<string> targetFiles, ITokenSource cancellationTokenSource)
        {
            targetFiles = targetFiles ?? throw new ArgumentNullException(nameof(targetFiles));

            if (!targetFiles.Any())
            {
                return null;
            }

            FileSystem fileSystem = FileSystem.Instance;

            string rulesFolder = Path.Combine(solutionDirectory, Constants.RulesFolderName);

            IEnumerable<string> regexDefinitions = fileSystem.DirectoryGetFiles(rulesFolder, "*.json");

            ISet<Skimmer<AnalyzeContext>> skimmers = this.LoadRuleDefinitions(fileSystem, regexDefinitions);

            if (skimmers?.Any() != true)
            {
                return null;
            }

            var files = new Dictionary<string, string>();
            foreach (string file in targetFiles)
            {
                files[file] = null;
            }

            return this.AnalyzeCore(files, skimmers, cancellationTokenSource, fileSystem);
        }

        internal ISet<Skimmer<AnalyzeContext>> LoadRuleDefinitions(FileSystem fileSystem, IEnumerable<string> regexDefinitions)
        {
            fileSystem ??= FileSystem.Instance;

            var analyzeTimer = new Stopwatch();
            analyzeTimer.Start();

            // get skimmers
            ISet<Skimmer<AnalyzeContext>> skimmers = AnalyzeCommand.CreateSkimmersFromDefinitionsFiles(fileSystem, regexDefinitions);

            analyzeTimer.Stop();
            Trace.WriteLine(string.Format(Resources.TraceLog_RuleLoaded, skimmers.Count, analyzeTimer.ElapsedMilliseconds));

            return skimmers;
        }

        internal string AnalyzeCore(IDictionary<string, string> targetFiles, ISet<Skimmer<AnalyzeContext>> skimmers, ITokenSource cancellationTokenSource, FileSystem fileSystem)
        {
            if (skimmers?.Any() != true)
            {
                return null;
            }

            fileSystem ??= FileSystem.Instance;
            bool wasAnalyzed = false;
            var sb = new StringBuilder();

            using (var outputTextWriter = new StringWriter(sb))
            using (var sarifLogger = new SarifLogger(
                outputTextWriter,
                LogFilePersistenceOptions.None,
                dataToInsert: OptionallyEmittedData.VersionControlDetails,
                dataToRemove: OptionallyEmittedData.None,
                tool: this.MakeTool(),
                levels: new List<FailureLevel> { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note, FailureLevel.None },
                kinds: this.GetResultKinds(this.ExtensionOption),
                closeWriterOnDispose: false))
           {
                sarifLogger.AnalysisStarted();

                foreach (string targetFile in targetFiles.Keys)
                {
                    var uri = new Uri(targetFile, UriKind.Absolute);
                    string fileText = null;

                    if (!this.ExtensionOption?.ShouldAnalyzeSarifFile != true && targetFile.EndsWith(".sarif", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (!targetFiles.TryGetValue(targetFile, out fileText) || fileText == null)
                    {
                        fileText = fileSystem.FileReadAllText(targetFile);
                    }

                    var analyzeTimer = new Stopwatch();
                    var disabledSkimmers = new HashSet<string>();
                    analyzeTimer.Start();

                    // initial context
                    var context = new AnalyzeContext
                    {
                        TargetUri = new Uri(targetFile, UriKind.RelativeOrAbsolute),
                        FileContents = fileText,
                        Logger = sarifLogger,
                        DynamicValidation = true,
                    };

                    using (context)
                    {
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        // clear region cache make sure latest text is cached
                        FileRegionsCache.Instance.ClearCache();

                        // Filtering file before analyzing.
                        IEnumerable<Skimmer<AnalyzeContext>> applicableSkimmers = AnalyzeCommand.DetermineApplicabilityForTargetHelper(
                            context,
                            skimmers,
                            disabledSkimmers);

                        Trace.WriteLine(string.Format(Resources.TraceLog_ApplicableRuleCount, applicableSkimmers.Count()));

                        AnalyzeCommand.AnalyzeTargetHelper(context, applicableSkimmers, disabledSkimmers);
                    }

                    analyzeTimer.Stop();
                    Trace.WriteLine(string.Format(Resources.TraceLog_TargetAnalyzed, targetFile, analyzeTimer.ElapsedMilliseconds));
                    wasAnalyzed = true;
                }

                sarifLogger.AnalysisStopped(RuntimeConditions.None);

                if (wasAnalyzed)
                {
                    outputTextWriter.Flush();
                }
            }

            return sb.ToString();
        }

        private Tool MakeTool() =>
            new Tool
            {
                Driver = new ToolComponent
                {
                    Name = ToolName,
                    Version = ToolVersion,
                    SemanticVersion = ToolSemanticVersion,
                },
            };

        private IEnumerable<ResultKind> GetResultKinds(ISariferOption sariferOption = null)
        {
            // always include fail results.
            var result = new List<ResultKind>
            {
                ResultKind.Fail,
            };

            if (sariferOption != null && sariferOption.IncludesPassResults)
            {
                result.Add(ResultKind.Pass);
            }

            return result;
        }
    }
}
