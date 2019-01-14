using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace XAMLator.Client.VisualStudio
{
    public class RunningDocTableEventListener : IVsRunningDocTableEvents3
    {
        public event EventHandler<string> FileSaved;
        private readonly IVsRunningDocumentTable m_RDT;

        public RunningDocTableEventListener()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            m_RDT = (IVsRunningDocumentTable)Package.GetGlobalService(typeof(SVsRunningDocumentTable));
            m_RDT.AdviseRunningDocTableEvents(this, out uint mRdtCookie);
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining,
            uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            InvokeFileSavedAsync(docCookie);

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

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld,
            string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        private async Task InvokeFileSavedAsync(uint docCookie)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            m_RDT.GetDocumentInfo(docCookie, out uint pgrfRdtFlags, out uint pdwReadLocks, out uint pdwEditLocks,
                out string pbstrMkDocument, out IVsHierarchy ppHier, out uint pitemid, out IntPtr ppunkDocData);

            FileSaved?.Invoke(this, pbstrMkDocument);
        }
    }
}
