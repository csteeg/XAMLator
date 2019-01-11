using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.StyleSheets;
using Xamarin.Forms.Xaml;
using XAMLator.Server.Abstractions;

namespace XAMLator.Server
{
	/// <summary>
	/// Loads XAML views live with requests from the IDE.
	/// </summary>
	public class VM
	{
		readonly object mutex = new object();
		IEvaluator evaluator;

		static VM()
		{
			ReplaceResourcesProvider();
		}

		public VM(IEnumerable<Assembly> referenceAssemblies = null)
		{
			evaluator = new Evaluator()
			{
				Assemblies = referenceAssemblies
			};
		}

		public Task<EvalResult> Eval(EvalRequest request, TaskScheduler mainScheduler, CancellationToken token)
		{
			var tcs = new TaskCompletionSource<EvalResult>();
			var r = new EvalResult();
			lock (mutex)
			{
				Task.Factory.StartNew(async () =>
				{
					try
					{
						r = await EvalOnMainThread(request, token);
						tcs.SetResult(r);
					}
					catch (Exception ex)
					{
						tcs.SetException(ex);
					}
				}, token, TaskCreationOptions.None, mainScheduler).Wait();
				return tcs.Task;
			}
		}

		async Task<EvalResult> EvalOnMainThread(EvalRequest request, CancellationToken token)
		{
			EvalResult evalResult = new EvalResult()
			{
				ResourceName = request.ResourceName
			};

			var sw = new System.Diagnostics.Stopwatch();

			Log.Debug($"Evaluation request {request}");

			sw.Start();

			try
			{
				if (request.ResourceName?.EndsWith(".css") ?? false) //TODO: since we don't have anything to get back our stylesheet's id, we'll do this by convention
				{
					evalResult.Code = request.Code;
					evalResult.ResultType = typeof(StyleSheet);
					evalResult.ResourceName = request.ResourceName;
					return evalResult;
				}
				else
				{
					var originalType = evalResult.OriginalType = GetTypeByName(request.OriginalTypeName);
					evalResult.Code = AddAssemblyNamesToXaml(request.Xaml, originalType.Assembly);
					VMState.EvalResults.AddOrUpdate(originalType, evalResult, (_, __) => evalResult);
					UpdateAssemblyResources(originalType.Assembly.GetName(), request.ResourceName, evalResult.Code, request.StyleSheets);

					if (PlatformConfig.SupportsEvaluation)
					{
						if (request.NeedsRebuild)
						{
							var errors = await evaluator.EvaluateCode(request.Code);
							if (errors?.Any() ?? false)
								evalResult.Messages = errors.ToArray();
						}
						var newType = GetTypeByName(request.NewTypeName);
						if (originalType != newType)
							VMState.TypeReplacements.AddOrUpdate(originalType, newType, (_, __) => newType);

						VMState.EvalResults.AddOrUpdate(newType, evalResult, (_, __) => evalResult);
						UpdateAssemblyResources(newType.Assembly.GetName(), request.ResourceName, evalResult.Code, request.StyleSheets);

						evalResult.ResultType = newType;
					}
					else
					{
						evalResult.ResultType = originalType;
					}
				}
			}
			catch (Exception exc)
			{
				evalResult.Messages = new List<EvalMessage>(evalResult.Messages ?? Enumerable.Empty<EvalMessage>()) { new EvalMessage("error", exc.Message) }.ToArray();
			}

			sw.Stop();

			Log.Debug($"Evaluation ended with result  {evalResult.ResultType}");

			evalResult.Duration = sw.Elapsed;
			return evalResult;
		}

		private void UpdateAssemblyResources(AssemblyName assemblyName, string xamlResourceName, string xaml, Dictionary<string, string> styleSheets)
		{
			var resources = VMState.AssemblyResources.GetOrAdd(assemblyName.FullName, new ConcurrentDictionary<string, string>());
			if (!string.IsNullOrEmpty(xamlResourceName) && !string.IsNullOrEmpty(xaml))
			{
				resources.AddOrUpdate(xamlResourceName, xaml, (_, __) => xaml);
			}
			foreach (var stylesheet in styleSheets)
			{
				resources.AddOrUpdate(stylesheet.Key, stylesheet.Value, (_, __) => stylesheet.Value);
			}

		}

		private string AddAssemblyNamesToXaml(string xaml, Assembly assembly)
		{
			if (assembly != null && (xaml?.Contains("clr-namespace") ?? false))
			{
				return Regex.Replace(xaml, "(\"clr-namespace:[^;\"]+)\"", $"$1;assembly={assembly.GetName().Name}\"", RegexOptions.Multiline);
			}
			return xaml;
		}

		static void ReplaceResourcesProvider()
		{
			var xamlAssembly = Assembly.Load(new AssemblyName("Xamarin.Forms.Core"));
			var xamlLoader = xamlAssembly.GetType("Xamarin.Forms.Internals.ResourceLoader");
			var providerField = (xamlLoader as TypeInfo).DeclaredFields.Single(f => f.Name == "resourceProvider");
			providerField.SetValue(null, (Func<AssemblyName, string, string>)LoadResource);
		}

		static string LoadResource(AssemblyName assemblyName, string name)
		{
			Log.Information($"Resolving resource {name}");
			//TODO: if XamlResourceId in FormsViewClassDeclaration is correctly filled, don't mess with name
			if (name.Contains('/') || name.Contains('\\'))
				name = Path.GetFileName(name);

			if (VMState.AssemblyResources.TryGetValue(assemblyName.FullName, out ConcurrentDictionary<string, string> resources)
				&& resources.TryGetValue(name, out string resource))
			{
				return resource;
			}
			return null;
		}

		/// <summary>
		/// Gets a all Type instances matching the specified class name.
		/// </summary>
		/// <param name="className">Name of the class sought.</param>
		/// <returns>Types that have the class name specified. They may not be in the same namespace.</returns>
		static Type GetTypeByName(string className)
		{
			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					Type[] assemblyTypes = a.GetTypes();
					for (int j = 0; j < assemblyTypes.Length; j++)
					{
						if (assemblyTypes[j].FullName == className)
						{
							return (assemblyTypes[j]);
						}
					}
				}
				catch
				{
					//just continue with the next assembly
				}
			}
			return null;
		}
	}
}

