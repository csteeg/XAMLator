using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XAMLator.Server
{
	/// <summary>
	/// Preview server that process HTTP requests, evaluates them in the <see cref="VM"/>
	/// and preview them with the <see cref="Previewer"/>.
	/// </summary>
	public class UpdateServer
	{
		static readonly UpdateServer serverInstance = new UpdateServer();

		VM vm;
		TaskScheduler mainScheduler;
		IUpdateResultHandler previewer;
		bool isRunning;
		TcpCommunicatorClient client;
		ErrorViewModel errorViewModel;

		internal static UpdateServer Instance => serverInstance;

		UpdateServer()
		{
			client = new TcpCommunicatorClient();
			client.DataReceived += HandleDataReceived;
			errorViewModel = new ErrorViewModel();
		}

		public static Task<bool> Run(Dictionary<Type, object> viewModelsMapping = null, IUpdateResultHandler previewer = null, string ideIP = null, int idePort = Constants.DEFAULT_PORT,
			IEnumerable<Assembly> referenceAssemblies = null)
		{
			return Instance.RunInternal(viewModelsMapping, previewer, ideIP, idePort, referenceAssemblies);
		}

		internal async Task<bool> RunInternal(Dictionary<Type, object> viewModelsMapping, IUpdateResultHandler previewer, string ideIP = null, int idePort = Constants.DEFAULT_PORT,
			IEnumerable<Assembly> referenceAssemblies = null)
		{
			if (isRunning)
			{
				return true;
			}

			mainScheduler = TaskScheduler.FromCurrentSynchronizationContext();
			await RegisterDevice(ideIP, idePort);
			if (viewModelsMapping == null)
			{
				viewModelsMapping = new Dictionary<Type, object>();
			}
			if (previewer == null)
			{
				previewer = new Previewer(viewModelsMapping);
			}
			this.previewer = previewer;
			vm = new VM(referenceAssemblies);
			isRunning = true;
			return true;
		}

		async Task RegisterDevice(string ideIP, int idePort)
		{
			ideIP = string.IsNullOrEmpty(ideIP) ? GetIdeIPFromResource() : ideIP;
			try
			{
				Log.Information($"Connecting to IDE at tcp://{ideIP}:{idePort}");
				await client.Connect(ideIP, idePort);
			}
			catch (Exception ex)
			{
				Log.Error($"Couldn't register device at {ideIP}");
				Log.Exception(ex);
			}
		}

		string GetIdeIPFromResource()
		{
			try
			{
				using (Stream stream = GetType().Assembly.GetManifestResourceStream(Constants.IDE_IP_RESOURCE_NAME))
				using (StreamReader reader = new StreamReader(stream))
				{
					return reader.ReadToEnd().Split('\n')[0].Trim();
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
				return null;
			}
		}

		async void HandleDataReceived(object sender, object e)
		{
			await HandleEvalRequest((e as JContainer).ToObject<EvalRequest>());
		}

		async Task HandleEvalRequest(EvalRequest request)
		{
			EvalResponse evalResponse = new EvalResponse();
			EvalResult result;
			try
			{
				result = await vm.Eval(request, mainScheduler, CancellationToken.None);
				if (result?.HasResult ?? false)
				{
					var tcs = new TaskCompletionSource<bool>();
					Xamarin.Forms.Device.BeginInvokeOnMainThread(async () =>
					{
						try
						{
							await previewer.ProcessResult(result);
							tcs.SetResult(true);
						}
						catch (Exception ex)
						{
							errorViewModel.SetError("Oh no! An exception!", ex);
							await previewer.NotifyError(errorViewModel);
							tcs.SetException(ex);
						}
					});
					await tcs.Task;
				}
				else if (result != null)
				{
					Xamarin.Forms.Device.BeginInvokeOnMainThread(async () =>
					{
						errorViewModel.SetError("Oh no! An evaluation error!", result);
						await previewer.NotifyError(errorViewModel);
					});
				}
			}
			catch (Exception ex)
			{
				Log.Exception(ex);
			}
		}
	}
}
