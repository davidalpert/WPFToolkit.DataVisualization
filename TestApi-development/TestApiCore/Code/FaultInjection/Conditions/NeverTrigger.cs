// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;

namespace Microsoft.Test.FaultInjection.Conditions
{
    [Serializable()]
    internal sealed class NeverTrigger : ICondition
    {
        public bool Trigger(IRuntimeContext context)
        {
            return false;
        }
    }
}