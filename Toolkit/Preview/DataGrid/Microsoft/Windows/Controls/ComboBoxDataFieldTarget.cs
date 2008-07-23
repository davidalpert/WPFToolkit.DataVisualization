//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     Enum for selection type of ComboBox column
    /// </summary>
    public enum ComboBoxDataFieldTarget
    {
        /// <summary>
        /// SelectedItem of the ComboBox will be used
        /// for DataFieldBinding
        /// </summary>
        SelectedItem,

        /// <summary>
        /// SelectedValue property of ComboBox will
        /// be used for DataFieldBinding
        /// </summary>
        SelectedValue,

        /// <summary>
        /// Text property of ComboBox will be
        /// used for DataFieldBinding
        /// </summary>
        Text
    }
}
