using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XAMLator.Server
{
    /// <summary>
    /// Loads XAML views live with requests from the IDE.
    /// </summary>
    public class VM
    {
        static MethodInfo loadXAML;
        readonly object mutex = new object();
        IEvaluator evaluator;

        static VM()
        {
            ResolveLoadMethod();
            ReplaceResourcesProvider();
        }

        public VM()
        {
            evaluator = new Evaluator();
        }

        public static ConcurrentDictionary<Type, Type> TypeReplacements { get; } = new ConcurrentDictionary<Type, Type>();
        public static ConcurrentDictionary<Type, EvalResult> EvalResults { get; } = new ConcurrentDictionary<Type, EvalResult>();
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, string>> AssemblyResources { get; } = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();

        /// <summary>
        /// Hook used by new instances to load their XAML instead of retrieving
        /// it from the assembly.
        /// </summary>
        /// <param name="view">View.</param>
        public static void LoadXaml(object view)
        {
            if (view == null)
                return;
            if (EvalResults.TryGetValue(view.GetType(), out EvalResult result))
            {
                loadXAML.Invoke(null, new object[] { view, result?.Xaml });
            }
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
            EvalResult evalResult = new EvalResult();

            var sw = new System.Diagnostics.Stopwatch();

            Log.Debug($"Evaluation request {request}");

            sw.Start();

            try
            {
                var originalType = evalResult.OriginalType = GetTypeByName(request.OriginalTypeName);
                evalResult.Xaml = AddAssemblyNamesToXaml(request.Xaml, originalType.Assembly);
                EvalResults.AddOrUpdate(originalType, evalResult, (_, __) => evalResult);
                UpdateAssemblyResources(originalType.Assembly.GetName(), request.XamlResourceName, evalResult.Xaml, request.StyleSheets);

                if (PlatformConfig.SupportsEvaluation)
                {
                    if (request.NeedsRebuild)
                    {
                        var errors = await evaluator.EvaluateCode(request.Declarations);
                        if (errors?.Any() ?? false)
                            evalResult.Messages = errors.ToArray();
                    }
                    var newType = GetTypeByName(request.NewTypeName);
                    TypeReplacements.AddOrUpdate(originalType, newType, (_, __) => newType);
                    EvalResults.AddOrUpdate(newType, evalResult, (_, __) => evalResult);
                    UpdateAssemblyResources(newType.Assembly.GetName(), request.XamlResourceName, evalResult.Xaml, request.StyleSheets);

                    evalResult.ResultType = newType;
                }
                else
                {
                    evalResult.ResultType = originalType;
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
            var resources = AssemblyResources.GetOrAdd(assemblyName.FullName, new ConcurrentDictionary<string, string>());
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

        static void ResolveLoadMethod()
        {
            var xamlAssembly = Assembly.Load(new AssemblyName("Xamarin.Forms.Xaml"));
            var xamlLoader = xamlAssembly.GetType("Xamarin.Forms.Xaml.XamlLoader");
            loadXAML = xamlLoader.GetRuntimeMethod("Load", new[] { typeof(object), typeof(string) });
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

            if (AssemblyResources.TryGetValue(assemblyName.FullName, out ConcurrentDictionary<string, string> resources)
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

