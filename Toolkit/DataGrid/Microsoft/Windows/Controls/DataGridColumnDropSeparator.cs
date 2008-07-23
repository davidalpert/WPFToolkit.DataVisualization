//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    /// The separator used to indicate drop location during column header drag-drop
    /// </summary>
    internal class DataGridColumnDropSeparator : Separator
    {
        #region Constructors

        static DataGridColumnDropSeparator()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DataGridColumnDropSeparator), new FrameworkPropertyMetadata(typeof(DataGridColumnDropSeparator)));
            WidthProperty.OverrideMetadata(typeof(DataGridColumnDropSeparator), new FrameworkPropertyMetadata(null,
                                                                                                               new CoerceValueCallback(OnCoerceWidth)));
            HeightProperty.OverrideMetadata(typeof(DataGridColumnDropSeparator), new FrameworkPropertyMetadata(null,
                                                                                                                new CoerceValueCallback(OnCoerceHeight)));
        }

        #endregion

        #region Static Methods

        private static object OnCoerceWidth(DependencyObject d, object baseValue)
        {
            Double width = (double)baseValue;
            if (Double.IsNaN(width))
            {
                return 2.0;
            }
            return baseValue;
        }

        private static object OnCoerceHeight(DependencyObject d, object baseValue)
        {
            Double height = (double)baseValue;
            DataGridColumnDropSeparator separator = (DataGridColumnDropSeparator)d;
            if (separator._referenceHeader != null && Double.IsNaN(height))
            {
                return separator._referenceHeader.ActualHeight;
            }
            return baseValue;
        }

        #endregion

        #region Properties

        internal DataGridColumnHeader ReferenceHeader
        {
            get
            {
                return _referenceHeader;
            }
            set
            {
                _referenceHeader = value;
            }
        }

        #endregion

        #region Data

        DataGridColumnHeader _referenceHeader = null;

        #endregion

    }
}
