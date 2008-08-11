//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MS.Internal;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    /// Panel that lays out individual rows top to bottom.  
    /// </summary>
    public class DataGridRowsPresenter : VirtualizingStackPanel
    {
        /// <summary>
        ///     Calls the protected method BringIndexIntoView.
        /// </summary>
        /// <param name="index">The index of the row to scroll into view.</param>
        /// <remarks>
        ///     BringIndexIntoView should be callable either from the ItemsControl
        ///     or directly on the panel. This was not done in WPF, so we are
        ///     building this internally for the DataGrid. However, if a public
        ///     way of calling BringIndexIntoView becomes a reality, then
        ///     this method is no longer needed.
        /// </remarks>
        internal void InternalBringIndexIntoView(int index)
        {
            BringIndexIntoView(index);
        }

        /// <summary>
        ///     This method is invoked when the IsItemsHost property changes.
        /// </summary>
        /// <param name="oldIsItemsHost">The old value of the IsItemsHost property.</param>
        /// <param name="newIsItemsHost">The new value of the IsItemsHost property.</param>
        protected override void OnIsItemsHostChanged(bool oldIsItemsHost, bool newIsItemsHost)
        {
            base.OnIsItemsHostChanged(oldIsItemsHost, newIsItemsHost);

            if (newIsItemsHost)
            {
                DataGrid dataGrid = Owner;
                if (dataGrid != null)
                {
                    // ItemsHost should be the "root" element which has
                    // IsItemsHost = true on it.  In the case of grouping,
                    // IsItemsHost is true on all panels which are generating
                    // content.  Thus, we care only about the panel which
                    // is generating content for the ItemsControl.
                    IItemContainerGenerator generator = dataGrid.ItemContainerGenerator as IItemContainerGenerator;
                    if (generator != null && generator == generator.GetItemContainerGeneratorForPanel(this))
                    {
                        dataGrid.InternalItemsHost = this;
                    }
                }
            }
            else
            {
                // No longer the items host, clear out the property on the DataGrid
                if ((_owner != null) && (_owner.InternalItemsHost == this))
                {
                    _owner.InternalItemsHost = null;
                }

                _owner = null;
            }
        }

        /// <summary>
        /// override of ViewportOffsetChanged method which forwards the call to datagrid on horizontal scroll
        /// </summary>
        /// <param name="oldViewportOffset"></param>
        /// <param name="newViewportOffset"></param>
        protected override void OnViewportOffsetChanged(Vector oldViewportOffset, Vector newViewportOffset)
        {
            base.OnViewportOffsetChanged(oldViewportOffset, newViewportOffset);
            if (!DoubleUtil.AreClose(oldViewportOffset.X, newViewportOffset.X))
            {
                DataGrid dataGrid = Owner;
                if (dataGrid != null)
                {
                    dataGrid.OnHorizontalScroll();
                }
            }
        }

        private DataGrid Owner
        {
            get
            {
                if (_owner == null)
                {
                    _owner = ItemsControl.GetItemsOwner(this) as DataGrid;
                }

                return _owner;
            }
        }

        private DataGrid _owner;
    }
}