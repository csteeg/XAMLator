using System;
using System.Collections.Generic;
using System.IO;

namespace XAMLator.Client
{
	public class XAMLatorMonitor
	{
		TcpCommunicatorServer server;

		public static XAMLatorMonitor Init(IIDE ide)
		{
			Instance = new XAMLatorMonitor(ide);
			return Instance;
		}

		public static XAMLatorMonitor Instance { get; private set; }

		XAMLatorMonitor(IIDE ide)
		{
			IDE = ide;
			server = new TcpCommunicatorServer(Constants.DEFAULT_PORT);
			ide.DocumentChanged += HandleDocumentChanged;
		}

		public IIDE IDE { get; private set; }

		public void StartMonitoring()
		{
			IDE.MonitorEditorChanges();
			server.StartListening();
		}

		async void HandleDocumentChanged(object sender, DocumentChangedEventArgs e)
		{
			if (server.ClientsCount == 0)
			{
				return;
			}

			EvalRequest request;
			if (e.Filename.EndsWith(".css"))
			{
				request = new EvalRequest
				{
					ResourceName = e.Filename,
					Code = e.Text
				};
			}
			else
			{
				var classDecl = await DocumentParser.ParseDocument(e.Filename, e.Text, e.SyntaxTree, e.SemanticModel);

				if (classDecl == null)
				{
					return;
				}

				request = new EvalRequest
				{
					Code = classDecl.Code,
					NeedsRebuild = classDecl.NeedsRebuild,
					OriginalTypeName = classDecl.FullNamespace,
					NewTypeName = classDecl.CurrentFullNamespace,
					Xaml = classDecl.Xaml,
					ResourceName = classDecl.ResourceId,
					StyleSheets = classDecl.StyleSheets
				};
			}
			await server.Send(request);
		}
	}
}