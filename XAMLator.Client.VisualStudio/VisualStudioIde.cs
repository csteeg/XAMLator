using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServices;

namespace XAMLator.Client.VisualStudio
{
    public class VisualStudioIde : IIDE
    {
        public event EventHandler<DocumentChangedEventArgs> DocumentChanged;
        private readonly VisualStudioWorkspace workspace;

        public VisualStudioIde(VisualStudioWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public void ListenForChanges(RunningDocTableEventListener saveListener)
        {
            saveListener.FileSaved += OnFileSaved;
        }

        private void OnFileSaved(object sender, string filePath)
        {
            ReloadFileAsync(filePath);
        }

        private async Task ReloadFileAsync(string filePath)
        {
            Solution solution = workspace.CurrentSolution;
            DocumentId documentId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
            var document = solution.GetDocument(documentId);

            if (document != null)
            {
                SyntaxTree syntaxTree = await document.GetSyntaxTreeAsync();
                SemanticModel semanticModel = await document.GetSemanticModelAsync();
                SourceText sourceText = await document.GetTextAsync();

                string text = sourceText?.ToString() ?? string.Empty;

                DocumentChanged?.Invoke(this, new DocumentChangedEventArgs(filePath, text, syntaxTree, semanticModel));
            }
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
    }
}
