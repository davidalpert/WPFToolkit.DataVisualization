using System;
using System.Diagnostics;

namespace Microsoft.Test.ApplicationControl
{
    /// <summary>
    /// Defines the contract for an out of process AutomatedApplication.
    /// </summary>
    /// <remarks>
    /// Represents the 'Implemention' inteface for a bridge pattern. As such, 
    /// this can vary from the public interface of AutomatedApplication.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Impl")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    public interface IOutOfProcessAutomatedApplicationImpl : IAutomatedApplicationImpl
    {        
        /// <summary>
        /// Gets the process associated with the application.
        /// </summary>
        Process Process { get; }      
    }

}