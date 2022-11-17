// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CodeAnalysis.Sarif.Sarifer
{
    internal class RunningDocTableEventsHandler : IVsRunningDocTableEvents3
    {
        private readonly RunningDocumentTable runningDocTable;

        private readonly IBackgroundAnalysisService backgroundAnalysisService;

        private int isRunning;

        internal RunningDocTableEventsHandler(IServiceProvider serviceProvider)
        {
            this.runningDocTable = new RunningDocumentTable(serviceProvider);

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));

            if (componentModel != null)
            {
                this.backgroundAnalysisService = componentModel.GetService<IBackgroundAnalysisService>();
            }
        }

        // IVsRunningDocTableEvents
        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        // IVsRunningDocTableEvents2
        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeSave(uint docCookie)
        {
            if (this.runningDocTable != null && docCookie != 0 && this.backgroundAnalysisService != null)
            {
                this.BackgroundAnalyzeAsync(
                    this.runningDocTable.GetDocumentInfo(docCookie).Moniker,
                    this.runningDocTable.GetRunningDocumentContents(docCookie),
                    default)
                    .FileAndForget(FileAndForgetEventName.BackgroundAnalysisFailure);
            }

            return VSConstants.S_OK;
        }

        private async System.Threading.Tasks.Task BackgroundAnalyzeAsync(string path, string text, CancellationToken cancellationToken)
        {
            if (!this.IsRunning())
            {
                this.SetRun(true);
                try
                {
                    await System.Threading.Tasks.TaskScheduler.Default;

                    await this.backgroundAnalysisService.AnalyzeAsync(path, text, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this.SetRun(false);
                }
            }
        }

        private bool IsRunning() => Interlocked.CompareExchange(ref this.isRunning, 1, 1) == 1;

        private bool SetRun(bool run) => (run ?
            Interlocked.CompareExchange(ref this.isRunning, 1, 0) : Interlocked.CompareExchange(ref this.isRunning, 0, 1)) == 1;
    }
}
