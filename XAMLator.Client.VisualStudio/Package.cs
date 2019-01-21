﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using XAMLator.Client.VisualStudio.Listeners;
using XAMLator.Client.VisualStudio.Services;
using Task = System.Threading.Tasks.Task;

namespace XAMLator.Client.VisualStudio
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(Client.PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class Client : AsyncPackage
    {
        /// <summary>
        /// Client GUID string.
        /// </summary>
        public const string PackageGuidString = "59f64fd1-9e19-47b8-9105-b3e502d071fc";

        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class.
        /// </summary>
        public Client()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            IComponentModel componentModel = (IComponentModel)(await GetServiceAsync(typeof(SComponentModel)));
            RunningDocTableEventListener runningDocTableEventListener = new RunningDocTableEventListener();

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            IVsRunningDocumentTable iVsRunningDocumentTable =
                (IVsRunningDocumentTable)GetGlobalService(typeof(SVsRunningDocumentTable));

            iVsRunningDocumentTable.AdviseRunningDocTableEvents(runningDocTableEventListener, out uint mRdtCookie);

            AnalysisService analysisService = new AnalysisService(componentModel.GetService<VisualStudioWorkspace>());
            DocumentService documentService =
                new DocumentService(iVsRunningDocumentTable, runningDocTableEventListener, analysisService);
            
            VisualStudioIde visualStudioIde = new VisualStudioIde(documentService);

            XAMLatorMonitor.Init(visualStudioIde);
            XAMLatorMonitor.Instance.StartMonitoring();
        }

        #endregion
    }
}
