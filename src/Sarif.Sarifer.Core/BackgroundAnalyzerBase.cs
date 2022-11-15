// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Sarif.Writers;

// TODO: Include tool name in logId. Replace non-alphanum chars with underscore for guaranteed file system compat.

namespace Microsoft.CodeAnalysis.Sarif.Sarifer
{
    /// <summary>
    /// Base class for background analyzers.
    /// </summary>
    /// <remarks>
    /// This class invokes the analysis (implemented in derived classes), and sends the resulting
    /// <see cref="SarifLog"/> to all exported implementations of <see cref="IBackgroundAnalysisSink"/>.
    /// Derived classes need only override <see cref="CreateSarifLog(string)"/>.
    /// </remarks>
    public abstract class BackgroundAnalyzerBase : IBackgroundAnalyzer
    {
        private const int DefaultBufferSize = 1024;

        private ISariferOption option;

        /// <inheritdoc/>
        public abstract string ToolName { get; }

        /// <inheritdoc/>
        public abstract string ToolVersion { get; }

        /// <inheritdoc/>
        public abstract string ToolSemanticVersion { get; }

        internal ISariferOption ExtensionOption
        {
            get
            {
                this.option ??= SariferOption.Instance;
                return option;
            }
        }

        /// <inheritdoc/>
        public async Task<string> AnalyzeAsync(string path, string text, CancellationToken cancellationToken)
        {
            text = text ?? throw new ArgumentNullException(nameof(text));

            string solutionDirectory = await VsUtilities.GetSolutionDirectoryAsync().ConfigureAwait(continueOnCapturedContext: false);

            // If we don't have a solutionDirectory, then, we don't need to analyze.
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                return null;
            }

            string sarifText = null;

            try
            {
                using (var wrapper = new AppDomainWrapper())
                {
                    CancellableProxy cancellable = wrapper.CreateInstance<CancellableProxy>();
                    using (cancellationToken.Register(() => cancellable.Cancel()))
                    {
                        AnalyzeCommandProxy proxy = wrapper.CreateInstance<AnalyzeCommandProxy>();
                        if (proxy != null)
                        {
                            sarifText = proxy.DoWork(solutionDirectory, path, text, cancellable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }

            return sarifText;
        }

        /// <inheritdoc/>
        public async Task<string> AnalyzeAsync(IEnumerable<string> targetFiles, CancellationToken cancellationToken)
        {
            targetFiles = targetFiles ?? throw new ArgumentNullException(nameof(targetFiles));

            if (!targetFiles.Any())
            {
                return null;
            }

            string solutionDirectory = await VsUtilities.GetSolutionDirectoryAsync().ConfigureAwait(continueOnCapturedContext: false);

            string sarifText = null;

            try
            {
                using (var wrapper = new AppDomainWrapper())
                {
                    CancellableProxy cancellable = wrapper.CreateInstance<CancellableProxy>();
                    using (cancellationToken.Register(() => cancellable.Cancel()))
                    {
                        AnalyzeCommandProxy proxy = wrapper.CreateInstance<AnalyzeCommandProxy>();
                        if (proxy != null)
                        {
                            sarifText = proxy.DoWork(solutionDirectory, targetFiles, cancellable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }

            return sarifText;
        }

        /// <summary>
        /// Analyzes the specified text.
        /// </summary>
        /// <remarks>
        /// This method runs on a background thread, so there is no need for derived classes to
        /// make anything async.
        /// </remarks>
        /// <param name="uri">
        /// The absolute URI of the file to analyze, or null if the text came from a VS text
        /// buffer that was not attached to a file.
        /// </param>
        /// <param name="text">
        /// The text to analyze.
        /// </param>
        /// <param name="solutionDirectory">
        /// The root directory of the current solution, or null if no solution is open.
        /// </param>
        /// <param name="sarifLogger">
        /// A <see cref="SarifLogger"/> to which the analyzer should log the results of the
        /// analysis.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that can be used to cancel the background analysis.
        /// </param>
        /// <returns>
        /// A boolean showing if the result was processed or not.
        /// </returns>
        protected abstract bool AnalyzeCore(Uri uri, string text, string solutionDirectory, SarifLogger sarifLogger, CancellationToken cancellationToken);

        private SarifLogger MakeSarifLogger(TextWriter writer) =>
            new SarifLogger(
                writer,
                LogFilePersistenceOptions.None,
                dataToInsert: OptionallyEmittedData.VersionControlDetails,
                dataToRemove: OptionallyEmittedData.None,
                tool: this.MakeTool(),
                levels: new List<FailureLevel> { FailureLevel.Error, FailureLevel.Warning, FailureLevel.Note, FailureLevel.None },
                kinds: this.GetResultKinds(this.ExtensionOption),
                closeWriterOnDispose: false);

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
