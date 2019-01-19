using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using XAMLator.Client.VisualStudio.Listeners;
using XAMLator.Client.VisualStudio.Models;
using Task = System.Threading.Tasks.Task;

namespace XAMLator.Client.VisualStudio.Services
{
    public class DocumentService
    {
        private readonly IVsRunningDocumentTable iVsRunningDocumentTable;
        private readonly AnalysisService analysisService;
        public EventHandler<DocumentAnalysis> OnDocumentChanged;

        public DocumentService(IVsRunningDocumentTable iVsRunningDocumentTable,
            RunningDocTableEventListener runningDocTableEventListener,
            AnalysisService analysisService)
        {
            this.iVsRunningDocumentTable = iVsRunningDocumentTable;
            this.analysisService = analysisService;

            runningDocTableEventListener.AfterSave += OnAfterSave;
        }

        private void OnAfterSave(object sender, uint docCookie)
        {
#pragma warning disable 4014
            OnFileSavedAsync(sender, docCookie);
#pragma warning restore 4014
        }

        private async Task OnFileSavedAsync(object sender, uint docCookie)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            (string pbstrMkDocument, IntPtr ppunkDocData) = GetDocumentInfo(docCookie);

            await TaskScheduler.Default;

            DocumentAnalysis analysis = await analysisService.GetDocumentAsync(pbstrMkDocument, ppunkDocData);

            if (analysis != null)
                OnDocumentChanged?.Invoke(this, analysis);
        }

        private (string pbstrMkDocument, IntPtr ppunkDocData) GetDocumentInfo(uint docCookie)
        {
            IntPtr ppunkDocData = default(IntPtr);
            string pbstrMkDocument = string.Empty;

            iVsRunningDocumentTable.GetDocumentInfo(docCookie, out uint pgrfRdtFlags, out uint pdwReadLocks,
                out uint pdwEditLocks, out pbstrMkDocument, out IVsHierarchy ppHier, out uint pitemid,
                out ppunkDocData);

            return (pbstrMkDocument: pbstrMkDocument, ppunkDocData: ppunkDocData);
        }
    }
}
