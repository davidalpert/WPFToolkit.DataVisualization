//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MS.Internal;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    /// Represents the header for each row of the DataGrid
    /// </summary>
    public class DataGridRowHeader : ButtonBase
    {
        static DataGridRowHeader()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(typeof(DataGridRowHeader)));

            ContentProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(OnNotifyPropertyChanged, OnCoerceContent));
            ContentTemplateProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(OnNotifyPropertyChanged, OnCoerceContentTemplate));
            ContentTemplateSelectorProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(OnNotifyPropertyChanged, OnCoerceContentTemplateSelector));
            StyleProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(OnNotifyPropertyChanged, OnCoerceStyle));
            WidthProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(OnNotifyPropertyChanged, OnCoerceWidth));

            ClickModeProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(ClickMode.Press));
            FocusableProperty.OverrideMetadata(typeof(DataGridRowHeader), new FrameworkPropertyMetadata(false));
        }

        #region Layout
       
        /// <summary>
        /// Measure this element and it's child elements.
        /// </summary>
        /// <remarks>
        /// DataGridRowHeader needs to update the DataGrid's RowHeaderActualWidth & use this as it's width so that they all end up the
        /// same size.
        /// </remarks>
        /// <param name="availableSize"></param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            var baseSize = base.MeasureOverride(availableSize);
            
            if (DoubleUtil.IsNaN(DataGridOwner.RowHeaderWidth) &&
                baseSize.Width > DataGridOwner.RowHeaderActualWidth)
            {
                DataGridOwner.RowHeaderActualWidth = baseSize.Width;
            }

            //
            // Regardless of how width the Header wants to be, we use 
            // DataGridOwner.RowHeaderActualWidth to ensure they're all the same size.
            //
            return new Size(DataGridOwner.RowHeaderActualWidth, baseSize.Height);
        }

        #endregion

        #region Row Communication

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Give the Row a pointer to the RowHeader so that it can propagate down change notifications
            DataGridRow parent = ParentRow;

            if (parent != null)
            {
                parent.RowHeader = this;
                SyncProperties();
            }
        }


        /// <summary>
        ///     Update all properties that get a value from the DataGrid
        /// </summary>
        /// <remarks>
        ///     See comment on DataGridRow.OnDataGridChanged
        /// </remarks>
        internal void SyncProperties()
        {
            DataGridHelper.TransferProperty(this, ContentProperty);
            DataGridHelper.TransferProperty(this, StyleProperty);
            DataGridHelper.TransferProperty(this, ContentTemplateProperty);
            DataGridHelper.TransferProperty(this, ContentTemplateSelectorProperty);
            DataGridHelper.TransferProperty(this, WidthProperty);
            CoerceValue(IsRowSelectedProperty);
        }

        #endregion


        #region Property Change Notification

        /// <summary>
        ///     Notifies parts that respond to changes in the ContentTemplateProperty.
        /// </summary>
        private static void OnNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGridRowHeader)d).NotifyPropertyChanged(d, e);
        }

        /// <summary>
        ///     Notification for column header-related DependencyProperty changes from the grid or from columns.
        /// </summary>
        internal void NotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == DataGridRow.HeaderProperty || e.Property == ContentProperty)
            {
                DataGridHelper.TransferProperty(this, ContentProperty);
            }
            else if (e.Property == DataGrid.RowHeaderStyleProperty || e.Property == DataGridRow.HeaderStyleProperty || e.Property == StyleProperty)
            {
                DataGridHelper.TransferProperty(this, StyleProperty);
            }
            else if (e.Property == DataGridRow.HeaderTemplateProperty || e.Property == ContentTemplateProperty)
            {
                DataGridHelper.TransferProperty(this, ContentTemplateProperty);
            }
            else if (e.Property == DataGridRow.HeaderTemplateSelectorProperty || e.Property == ContentTemplateSelectorProperty)
            {
                DataGridHelper.TransferProperty(this, ContentTemplateSelectorProperty);
            }
            else if (e.Property == DataGrid.RowHeaderWidthProperty || e.Property == WidthProperty)
            {
                DataGridHelper.TransferProperty(this, WidthProperty);
            }
            else if (e.Property == DataGridRow.IsSelectedProperty)
            {
                CoerceValue(IsRowSelectedProperty);
            }
            else if (e.Property == DataGrid.RowHeaderActualWidthProperty)
            {
                // When the RowHeaderActualWidth changes we need to re-measure to pick up the new value for DesiredSize
                this.InvalidateMeasure();
                this.InvalidateArrange();
                
                //
                // If the DataGrid has not run layout the headers parent may not position the cells correctly when the header size changes.
                // This will cause the cells to be out of sync with the columns. To avoid this we will force a layout of the headers parent panel.
                //
                var parent = this.Parent as UIElement;
                if (parent != null)
                {
                    parent.InvalidateMeasure();
                    parent.InvalidateArrange();
                }
            }
        }

        #endregion 


        #region Property Coercion callbacks

        /// <summary>
        ///     Coerces the Content property.  We're choosing a value between Row.Header and the Content property on RowHeader.
        /// </summary>
        private static object OnCoerceContent(DependencyObject d, object baseValue)
        {
            var header = d as DataGridRowHeader;
            return DataGridHelper.GetCoercedTransferPropertyValue(header, baseValue, ContentProperty,
                                                                  header.ParentRow, DataGridRow.HeaderProperty,
                                                                  null, null);
        }

        /// <summary>
        ///     Coerces the ContentTemplate property.
        /// </summary>
        private static object OnCoerceContentTemplate(DependencyObject d, object baseValue)
        {
            var header = d as DataGridRowHeader;
            return DataGridHelper.GetCoercedTransferPropertyValue(header, baseValue, ContentTemplateProperty,
                                                                  header.ParentRow, DataGridRow.HeaderTemplateProperty,
                                                                  null, null);
        }

        /// <summary>
        ///     Coerces the ContentTemplateSelector property.
        /// </summary>
        private static object OnCoerceContentTemplateSelector(DependencyObject d, object baseValue)
        {
            var header = d as DataGridRowHeader;
            return DataGridHelper.GetCoercedTransferPropertyValue(header, baseValue, ContentTemplateSelectorProperty,
                                                                  header.ParentRow, DataGridRow.HeaderTemplateSelectorProperty,
                                                                  null, null);
        }


        /// <summary>
        ///     Coerces the Style property.
        /// </summary>
        private static object OnCoerceStyle(DependencyObject d, object baseValue)
        {
            var header = d as DataGridRowHeader;
            return DataGridHelper.GetCoercedTransferPropertyValue(header, baseValue, StyleProperty,
                                                                  header.ParentRow, DataGridRow.HeaderStyleProperty,
                                                                  header.DataGridOwner, DataGrid.RowHeaderStyleProperty);
        }

        /// <summary>
        ///     Coerces the Width property.
        /// </summary>
        private static object OnCoerceWidth(DependencyObject d, object baseValue)
        {
            var header = d as DataGridRowHeader;
            return DataGridHelper.GetCoercedTransferPropertyValue(header, baseValue, WidthProperty,
                                                                  header.DataGridOwner, DataGrid.RowHeaderWidthProperty,
                                                                  null, null);
        }

        #endregion

        #region Selection

        /// <summary>
        ///     Indicates whether the owning DataGridRow is selected.
        /// </summary>
        [Bindable(true), Category("Appearance")]
        public bool IsRowSelected
        {
            get { return (bool)GetValue(IsRowSelectedProperty); }
        }

        private static readonly DependencyPropertyKey IsRowSelectedPropertyKey =
            DependencyProperty.RegisterReadOnly("IsRowSelected", typeof(bool), typeof(DataGridRowHeader),
                new FrameworkPropertyMetadata(false, null, new CoerceValueCallback(OnCoerceIsRowSelected)));

        /// <summary>
        ///     The DependencyProperty for the IsRowSelected property.
        /// </summary>
        public static readonly DependencyProperty IsRowSelectedProperty = IsRowSelectedPropertyKey.DependencyProperty;

        private static object OnCoerceIsRowSelected(DependencyObject d, object baseValue)
        {
            DataGridRowHeader header = (DataGridRowHeader)d;
            DataGridRow parent = header.ParentRow;
            if (parent != null)
            {
                return parent.IsSelected;
            }

            return baseValue;
        }

        /// <summary>
        ///     Called when the header is clicked.
        /// </summary>
        protected override void OnClick()
        {
            base.OnClick();

            // The base implementation took capture. This prevents us from doing
            // drag selection, so release it.
            if (Mouse.Captured == this)
            {
                ReleaseMouseCapture();
            }

            DataGrid dataGridOwner = DataGridOwner;
            DataGridRow parentRow = ParentRow;
            if ((dataGridOwner != null) && (parentRow != null))
            {
                dataGridOwner.HandleSelectionForRowHeaderInput(parentRow, /* startDragging = */ true);
            }
        }

        #endregion

        #region Helpers

        internal DataGridRow ParentRow
        {
            get
            {
                return DataGridHelper.FindParent<DataGridRow>(this);
            }
        }

        private DataGrid DataGridOwner
        {
            get
            {
                DataGridRow parent = ParentRow;
                if (parent != null)
                {
                    return parent.DataGridOwner;
                }

                return null;
            }
        }

        #endregion
    }
}