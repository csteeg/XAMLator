using System;
using XAMLator.Client.VisualStudio.Models;
using XAMLator.Client.VisualStudio.Services;
using Task = System.Threading.Tasks.Task;

namespace XAMLator.Client.VisualStudio
{
    public class VisualStudioIde : IIDE
    {
        public event EventHandler<DocumentChangedEventArgs> DocumentChanged;

        public VisualStudioIde(DocumentService documentService)
        {
            documentService.OnDocumentChanged += OnDocumentChanged;
        }

        public void MonitorEditorChanges()
        {
        }

        public void ShowError(string error, Exception ex = null)
        {
        }

        public Task RunTarget(string targetName)
        {
            return Task.CompletedTask;
        }

        private void OnDocumentChanged(object sender, DocumentAnalysis documentAnalysis)
        {
            if (documentAnalysis != null)
            {
                DocumentChangedEventArgs documentChangedEventArgs = new DocumentChangedEventArgs(documentAnalysis.Path,
                    documentAnalysis.Code, documentAnalysis.SyntaxTree, documentAnalysis.SemanticModel);

                DocumentChanged?.Invoke(this, documentChangedEventArgs);
            }
        }
    }
}
