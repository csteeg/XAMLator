using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.TextManager.Interop;
using XAMLator.Client.VisualStudio.Models;

namespace XAMLator.Client.VisualStudio.Services
{
    public class AnalysisService
    {
        private readonly VisualStudioWorkspace workspace;

        public AnalysisService(VisualStudioWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public Task<DocumentAnalysis> GetDocumentAsync(string pbstrMkDocument, IntPtr ppunkDocData)
        {
            Task<DocumentAnalysis> result = null;

            switch (Path.GetExtension(pbstrMkDocument))
            {
                case ".css":
                    result = GetCssDocumentAnalysisAsync(pbstrMkDocument, ppunkDocData);
                    break;
                default:
                    result = GetCodeDocumentAnalysisAsync(pbstrMkDocument);
                    break;
            }

            return result;
        }

        public Task<DocumentAnalysis> GetCssDocumentAnalysisAsync(string pbstrMkDocument, IntPtr ppunkDocData)
        {
            string pbstrbuf = string.Empty;

            if (Marshal.GetObjectForIUnknown(ppunkDocData) is IVsTextLines iVsTextLines)
            {
                iVsTextLines.GetLastLineIndex(out int piLine, out int piIndex);
                iVsTextLines.GetLineText(0, 0, piLine, piIndex, out pbstrbuf);
            }

            return Task.FromResult(new DocumentAnalysis
            {
                Path = pbstrMkDocument,
                Code = pbstrbuf
            });
        }

        public async Task<DocumentAnalysis> GetCodeDocumentAnalysisAsync(string pbstrMkDocument)
        {
            DocumentAnalysis result = null;

            Solution solution = workspace.CurrentSolution;
            DocumentId documentId = solution.GetDocumentIdsWithFilePath(pbstrMkDocument).FirstOrDefault();
            Document document = solution.GetDocument(documentId);

            if (document != null)
            {
                Task<SourceText> sourceTextTask = document.GetTextAsync();
                Task<SemanticModel> semanticModelTask = document.GetSemanticModelAsync();
                Task<SyntaxTree> syntaxTreeTask = document.GetSyntaxTreeAsync();

                result = new DocumentAnalysis
                {
                    Path = pbstrMkDocument,
                    Code = (await sourceTextTask)?.ToString() ?? string.Empty,
                    SemanticModel = await semanticModelTask,
                    SyntaxTree = await syntaxTreeTask
                };
            }

            return result;
        }
    }
}
