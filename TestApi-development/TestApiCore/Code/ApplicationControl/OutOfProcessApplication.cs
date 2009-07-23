﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Microsoft.Test.ApplicationControl
{
    /// <summary>
    /// Represents a test application running in a new, separate process.
    /// </summary>
    ///
    /// <example>
    /// The following example demonstrates how to use this class:
    /// <code>
    /// public void MyTest()
    /// {
    ///    var path = Path.Combine(executionDir, "WpfTestApplication.exe");
    ///    var aa = new OutOfProcessApplication(new OutOfProcessApplicationSettings
    ///    {
    ///        ProcessStartInfo = new ProcessStartInfo(path),
    ///        ApplicationImplementationFactory = new UIAutomationOutOfProcessApplicationFactory()
    ///    });
    ///  
    ///    aa.Start();
    ///    aa.WaitForMainWindow(TimeSpan.FromMilliseconds(5000));
    ///    
    ///    // Perform various tests...
    ///    
    ///    aa.Close();
    /// }
    /// </code>
    /// </example>
    public class OutOfProcessApplication : AutomatedApplication
    {
        /// <summary>
        /// Initializes a new instance of an OutOfProcessApplication.
        /// </summary>
        /// <param name="settings">The settings used to start the test application.</param>
        public OutOfProcessApplication(OutOfProcessApplicationSettings settings)
        {
            ValidateApplicationSettings(settings);
            ApplicationSettings = settings;            
        }

        /// <summary>
        /// The settings for the test application.
        /// </summary>
        public OutOfProcessApplicationSettings ApplicationSettings
        {
            get;
            protected set;
        }

        /// <summary>
        /// Creates and starts the test application.
        /// </summary>
        public override void Start()
        {
            if (IsApplicationRunning)
            {
                throw new InvalidOperationException("Cannot start an application instance if it is already running.");
            }

            IsApplicationRunning = true;
            ApplicationImplementation = ApplicationSettings.ApplicationImplementationFactory.Create(ApplicationSettings, null);
            ApplicationImplementation.MainWindowOpened += this.OnMainWindowOpened;
            ApplicationImplementation.FocusChanged += this.OnFocusChanged;
            ApplicationImplementation.Exited += this.OnExit;
            ApplicationImplementation.Start();
        }

        /// <summary>
        /// Get the process associated with the application.
        /// </summary>
        public Process Process
        {
            get
            {
                var impl = ApplicationImplementation as IOutOfProcessAutomatedApplicationImpl;
                if (impl != null)
                {
                    return impl.Process;
                }

                return null;
            }
        }

        /// <summary>
        /// Validates the settings for an OutOfProcessApplication.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        private void ValidateApplicationSettings(OutOfProcessApplicationSettings settings)
        {
            if (settings.ProcessStartInfo == null)
            {
                throw new InvalidOperationException("For OutOfProc scenario, ProcessStartInfo cannot be null.");
            }

            if (settings.ApplicationImplementationFactory == null)
            {
                throw new InvalidOperationException("ApplicationImplementationFactory must be specified.");
            }
        }
    }
}
