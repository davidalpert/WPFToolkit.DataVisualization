//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     A control for displaying a row of the DataGrid.
    ///     A row represents a data item in the DataGrid.
    ///     A row displays a cell for each column of the DataGrid.
    /// 
    ///     The data item for the row is added n times to the row's Items collection, 
    ///     where n is the number of columns in the DataGrid.
    /// </summary>
    public class DataGridRow : Control
    {
        #region Constructors

        /// <summary>
        ///     Instantiates global information.
        /// </summary>
        static DataGridRow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DataGridRow), new FrameworkPropertyMetadata(typeof(DataGridRow)));
            ItemsPanelProperty.OverrideMetadata(typeof(DataGridRow), new FrameworkPropertyMetadata(new ItemsPanelTemplate(new FrameworkElementFactory(typeof(DataGridCellsPanel)))));
            FocusableProperty.OverrideMetadata(typeof(DataGridRow), new FrameworkPropertyMetadata(false));
            BackgroundProperty.OverrideMetadata(typeof(DataGridRow), new FrameworkPropertyMetadata(null, OnNotifyRowPropertyChanged, OnCoerceBackground));

            // Set SnapsToDevicePixels to true so that this element can draw grid lines.  The metadata options are so that the property value doesn't inherit down the tree from here.
            SnapsToDevicePixelsProperty.OverrideMetadata(typeof(DataGridRow), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsArrange));
        }

        /// <summary>
        ///     Instantiates a new instance of this class.
        /// </summary>
        public DataGridRow()
        {
            _tracker = new ContainerTracking<DataGridRow>(this);
        }

        #endregion

        #region Data Item

        /// <summary>
        ///     The item that the row represents. This item is an entry in the list of items from the DataGrid.
        ///     From this item, cells are generated for each column in the DataGrid.
        /// </summary>
        public object Item
        {
            get { return GetValue(ItemProperty); }
            set { SetValue(ItemProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the Item property.
        /// </summary>
        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.Register("Item", typeof(object), typeof(DataGridRow), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyRowPropertyChanged)));

        /// <summary>
        ///     Called when the value of the Item property changes.
        /// </summary>
        /// <param name="oldItem">The old value of Item.</param>
        /// <param name="newItem">The new value of Item.</param>
        protected virtual void OnItemChanged(object oldItem, object newItem)
        {
            DataGridCellsPresenter cellsPresenter = CellsPresenter;
            if (cellsPresenter != null)
            {
                cellsPresenter.Item = newItem;
            }
        }

        #endregion

        #region Template

        /// <summary>
        ///     A template that will generate the panel that arranges the cells in this row.
        /// </summary>
        /// <remarks>
        ///     The template for the row should contain an ItemsControl that template binds to this property.
        /// </remarks>
        public ItemsPanelTemplate ItemsPanel
        {
            get { return (ItemsPanelTemplate)GetValue(ItemsPanelProperty); }
            set { SetValue(ItemsPanelProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the ItemsPanel property.
        /// </summary>
        public static readonly DependencyProperty ItemsPanelProperty = ItemsControl.ItemsPanelProperty.AddOwner(typeof(DataGridRow));

        #endregion

        #region Row Header

        /// <summary>
        ///     The object representing the Row Header.  
        /// </summary>
        public object Header
        {
            get { return GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the Header property.
        /// </summary>
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(object), typeof(DataGridRow), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyRowPropertyChanged)));

        /// <summary>
        ///     Called when the value of the Header property changes.
        /// </summary>
        /// <param name="oldHeader">The old value of Header</param>
        /// <param name="newHeader">The new value of Header</param>
        protected virtual void OnHeaderChanged(object oldHeader, object newHeader)
        {   
        }


        /// <summary>
        ///     The object representing the Row Header style.  
        /// </summary>
        public Style HeaderStyle
        {
            get { return (Style)GetValue(HeaderStyleProperty); }
            set { SetValue(HeaderStyleProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the HeaderStyle property.
        /// </summary>
        public static readonly DependencyProperty HeaderStyleProperty =
            DependencyProperty.Register("HeaderStyle", typeof(Style), typeof(DataGridRow), new FrameworkPropertyMetadata(null, OnNotifyRowAndRowHeaderPropertyChanged, OnCoerceHeaderStyle));

        /// <summary>
        ///     The object representing the Row Header template.  
        /// </summary>
        public DataTemplate HeaderTemplate
        {
            get { return (DataTemplate)GetValue(HeaderTemplateProperty); }
            set { SetValue(HeaderTemplateProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the HeaderTemplate property.
        /// </summary>
        public static readonly DependencyProperty HeaderTemplateProperty =
            DependencyProperty.Register("HeaderTemplate", typeof(DataTemplate), typeof(DataGridRow), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyRowAndRowHeaderPropertyChanged)));


        /// <summary>
        ///     The object representing the Row Header template selector.  
        /// </summary>
        public DataTemplateSelector HeaderTemplateSelector
        {
            get { return (DataTemplateSelector)GetValue(HeaderTemplateSelectorProperty); }
            set { SetValue(HeaderTemplateSelectorProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the HeaderTemplateSelector property.
        /// </summary>
        public static readonly DependencyProperty HeaderTemplateSelectorProperty =
            DependencyProperty.Register("HeaderTemplateSelector", typeof(DataTemplateSelector), typeof(DataGridRow), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyRowAndRowHeaderPropertyChanged)));


        #endregion

        #region Row Generation

        /// <summary>
        /// We can't override the metadata for a read only property, so we'll get the property change notification for AlternationIndexProperty this way instead.
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == AlternationIndexProperty)
            {
                NotifyPropertyChanged(this, e, NotificationTarget.Rows);
            }
        }

        /// <summary>
        ///     Prepares a row container for active use.
        /// </summary>
        /// <remarks>
        ///     Instantiates or updates a MultipleCopiesCollection ItemsSource in
        ///     order that cells be generated.
        /// </remarks>
        /// <param name="item">The data item that the row represents.</param>
        /// <param name="owningDataGrid">The DataGrid owner.</param>
        internal void PrepareRow(object item, DataGrid owningDataGrid)
        {
            bool fireOwnerChanged = (_owner != owningDataGrid);
            Debug.Assert(_owner == null || _owner == owningDataGrid, "_owner should be null before PrepareRow is called or the same as the owningDataGrid.");

            _owner = owningDataGrid;

            if (this != item)
            {
                Item = item;
            }

            if (IsEditing)
            {
                // If IsEditing was left on and this container was recycled, reset it here.
                IsEditing = false;
            }

            if (item == CollectionView.NewItemPlaceholder)
            {
                Visibility = owningDataGrid.PlaceholderVisibility;
            }

            // Since we just changed _owner we need to invalidate all child properties that rely on a value supplied by the DataGrid.
            // A common scenario is when a recycled Row was detached from the visual tree and has just been reattached (we always clear out the 
            // owner when recycling a container).
            if (fireOwnerChanged)
            {
                SyncProperties();
            }
        }

        /// <summary>
        ///     Clears the row of references.
        /// </summary>
        internal void ClearRow(object item, DataGrid owningDataGrid)
        {
            Debug.Assert(_owner == owningDataGrid, "_owner should be the same as the DataGrid that is clearing the row.");
            _owner = null;
        }

        /// <summary>
        ///     Used by the DataGrid owner to send notifications to the row container.
        /// </summary>
        internal ContainerTracking<DataGridRow> Tracker
        {
            get { return _tracker; }
        }

        #endregion

        #region Columns Notification

        /// <summary>
        ///     Notification from the DataGrid that the columns collection has changed.
        /// </summary>
        /// <param name="columns">The columns collection.</param>
        /// <param name="e">The event arguments from the collection's change event.</param>
        protected internal virtual void OnColumnsChanged(ObservableCollection<DataGridColumn> columns, NotifyCollectionChangedEventArgs e)
        {
            DataGridCellsPresenter cellsPresenter = CellsPresenter;
            if (cellsPresenter != null)
            {
                cellsPresenter.OnColumnsChanged(columns, e);
            }
        }

        #endregion

        #region Property Coercion

        private static object OnCoerceHeaderStyle(DependencyObject d, object baseValue)
        {
            var row = d as DataGridRow;
            return DataGridHelper.GetCoercedTransferPropertyValue(row, baseValue, HeaderStyleProperty,
                                                                  row.DataGridOwner, DataGrid.RowHeaderStyleProperty,
                                                                  null, null);
        }

        private static object OnCoerceBackground(DependencyObject d, object baseValue)
        {
            var row = d as DataGridRow;
            object coercedValue = baseValue;

            switch (row.AlternationIndex)
            {
                case 0:
                    coercedValue = DataGridHelper.GetCoercedTransferPropertyValue(row, baseValue, BackgroundProperty,
                                                                                  row.DataGridOwner, DataGrid.RowBackgroundProperty,
                                                                                  null, null);
                    break;
                case 1:
                    coercedValue = DataGridHelper.GetCoercedTransferPropertyValue(row, baseValue, BackgroundProperty,
                                                                                  row.DataGridOwner, DataGrid.AlternatingRowBackgroundProperty,
                                                                                  null, null);
                    break;
            }

            return coercedValue;
                
        }

        #endregion

        #region Notification Propagation

        private static void OnNotifyRowPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as DataGridRow).NotifyPropertyChanged(d, e, NotificationTarget.Rows);
        }

        private static void OnNotifyRowAndRowHeaderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as DataGridRow).NotifyPropertyChanged(d, e, NotificationTarget.Rows | NotificationTarget.RowHeaders);
        }

        /// <summary>
        /// Set by the CellsPresenter when it is created.  Used by the Row to send down property change notifications.
        /// </summary>
        internal DataGridCellsPresenter CellsPresenter
        {
            get { return _cellsPresenter; }
            set { _cellsPresenter = value; }
        }


        /// <summary>
        /// Set by the DetailsPresenter when it is created.  Used by the Row to send down property change notifications.
        /// </summary>
        internal DataGridDetailsPresenter DetailsPresenter
        {
            private get { return _detailsPresenter; }
            set { _detailsPresenter = value; }
        }


        /// <summary>
        /// Set by the RowHeader when it is created.  Used by the Row to send down property change notifications.
        /// </summary>
        internal DataGridRowHeader RowHeader
        {
            get { return _rowHeader; }
            set { _rowHeader = value; }
        }

        /// <summary>
        ///     General notification for DependencyProperty changes from the grid or from columns.
        /// </summary>
        internal void NotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e, NotificationTarget target)
        {
            NotifyPropertyChanged(d, string.Empty, e, target);
        }

        /// <summary>
        ///     General notification for DependencyProperty changes from the grid or from columns.
        /// </summary>
        internal void NotifyPropertyChanged(DependencyObject d, string propertyName, DependencyPropertyChangedEventArgs e, NotificationTarget target)
        {
            if (DataGridHelper.ShouldNotifyRows(target))
            {
                if (e.Property == DataGrid.RowBackgroundProperty || e.Property == DataGrid.AlternatingRowBackgroundProperty ||
                    e.Property == BackgroundProperty || e.Property == AlternationIndexProperty)
                {
                    DataGridHelper.TransferProperty(this, BackgroundProperty);
                }
                else if (e.Property == DataGrid.RowHeaderStyleProperty || e.Property == HeaderStyleProperty)
                {
                    DataGridHelper.TransferProperty(this, HeaderStyleProperty);
                }
                else if (e.Property == DataGridRow.ItemProperty)
                {
                    OnItemChanged(e.OldValue, e.NewValue);
                }
                else if (e.Property == DataGridRow.HeaderProperty)
                {
                    OnHeaderChanged(e.OldValue, e.NewValue);
                }
            }

            // TODO: No properties that notify the DetailsPresenter yet
#if NotifyDetailsPresenter
            if (DataGridHelper.ShouldNotifyDetailsPresenter(target))
            {
                if (DetailsPresenter != null)
                {
                    DetailsPresenter.NotifyPropertyChanged(d, e);
                }
            }
#endif

            if (DataGridHelper.ShouldNotifyCellsPresenter(target) || 
                DataGridHelper.ShouldNotifyCells(target) ||
                DataGridHelper.ShouldRefreshCellContent(target))
            {
                DataGridCellsPresenter cellsPresenter = CellsPresenter;
                if (cellsPresenter != null)
                {
                    cellsPresenter.NotifyPropertyChanged(d, propertyName, e, target);
                }
            }

            if (DataGridHelper.ShouldNotifyRowHeaders(target) && RowHeader != null)
            {
                RowHeader.NotifyPropertyChanged(d, e);
            }
        }


        /// <summary>
        ///     Fired when the Row is attached to the DataGrid.  The scenario here is if the user is scrolling and
        ///     the Row is a recycled container that was just added back to the visual tree.  Properties that rely on a value from
        ///     the Grid should be reevaluated because they may be stale.  
        /// </summary>
        /// <remarks>
        ///     Properties can obviously be stale if the DataGrid's value changes while the row is disconnected.  They can also
        ///     be stale for unobvious reasons.
        /// 
        ///     For example, the Style property is invalidated when we detect a new Visual parent.  This happens for 
        ///     elements in the row (such as the RowHeader) before Prepare is called on the Row.  The coercion callback
        ///     will thus be unable to find the DataGrid and will return the wrong value.  
        /// 
        ///     There is a potential for perf work here.  If we know a DP isn't invalidated when the visual tree is reconnected
        ///     and we know that the Grid hasn't modified that property then its value is likely fine.  We could also cache whether
        ///     or not the Grid's property is the one that's winning.  If not, no need to redo the coercion.  This notification 
        ///     is pretty fast already and thus not worth the work for now.
        /// </remarks>
        private void SyncProperties()
        {
            // Coerce all properties on Row that depend on values from the DataGrid
            // Style is ok since it's equivalent to ItemContainerStyle and has already been invalidated.

            DataGridHelper.TransferProperty(this, BackgroundProperty);
            DataGridHelper.TransferProperty(this, HeaderStyleProperty);

            if (CellsPresenter != null)
            {
                CellsPresenter.SyncProperties();
            }

            if (DetailsPresenter != null)
            {
                DetailsPresenter.SyncProperties();
            }

            if (RowHeader != null)
            {
                RowHeader.SyncProperties();
            }
        }

        #endregion

        #region Alternation

        /// <summary>
        ///     AlternationIndex is set on containers generated for an ItemsControl, when
        ///     the ItemsControl's AlternationCount property is positive.  The AlternationIndex
        ///     lies in the range [0, AlternationCount), and adjacent containers always get
        ///     assigned different values.
        /// </summary>
        /// <remarks>
        ///     Exposes ItemsControl.AlternationIndexProperty attached property as a direct property.
        /// </remarks>
        public int AlternationIndex
        {
            get { return (int)GetValue(AlternationIndexProperty); }
        }

        /// <summary>
        ///     DependencyProperty for AlternationIndex.
        /// </summary>
        /// <remarks>
        ///     Same as ItemsControl.AlternationIndexProperty.
        /// </remarks>
        public static readonly DependencyProperty AlternationIndexProperty = ItemsControl.AlternationIndexProperty.AddOwner(typeof(DataGridRow));

        #endregion

        #region Selection

        /// <summary>
        ///     Indicates whether this DataGridRow is selected.
        /// </summary>
        /// <remarks>
        ///     When IsSelected is set to true, an InvalidOperationException may be
        ///     thrown if the value of the SelectionUnit property on the parent DataGrid 
        ///     prevents selection or rows.
        /// </remarks>
        [Bindable(true), Category("Appearance")]
        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the IsSelected property.
        /// </summary>
        public static readonly DependencyProperty IsSelectedProperty =
                Selector.IsSelectedProperty.AddOwner(typeof(DataGridRow),
                        new FrameworkPropertyMetadata(false,
                                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                                new PropertyChangedCallback(OnIsSelectedChanged)));

        private static void OnIsSelectedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DataGridRow row = (DataGridRow)sender;
            bool isSelected = (bool)e.NewValue;

            if (isSelected && !row.IsSelectable)
            {
                throw new InvalidOperationException(SR.Get(SRID.DataGridRow_CannotSelectRowWhenCells));
            }

            // TODO: Fire automation event

            // Update the header's IsRowSelected property
            row.NotifyPropertyChanged(row, e, NotificationTarget.RowHeaders);

            // This will raise the appropriate selection event, which will
            // bubble to the DataGrid. The base class Selector code will listen
            // for these events and will update SelectedItems as necessary.
            row.RaiseSelectionChangedEvent(isSelected);
        }

        private void RaiseSelectionChangedEvent(bool isSelected)
        {
            if (isSelected)
            {
                OnSelected(new RoutedEventArgs(SelectedEvent, this));
            }
            else
            {
                OnUnselected(new RoutedEventArgs(UnselectedEvent, this));
            }
        }
        /// <summary>
        ///     Raised when the item's IsSelected property becomes true.
        /// </summary>
        public static readonly RoutedEvent SelectedEvent = Selector.SelectedEvent.AddOwner(typeof(DataGridRow));

        /// <summary>
        ///     Raised when the item's IsSelected property becomes true.
        /// </summary>
        public event RoutedEventHandler Selected
        {
            add
            {
                AddHandler(SelectedEvent, value);
            }
            remove
            {
                RemoveHandler(SelectedEvent, value);
            }
        }

        /// <summary>
        ///     Called when IsSelected becomes true. Raises the Selected event.
        /// </summary>
        /// <param name="e">Empty event arguments.</param>
        protected virtual void OnSelected(RoutedEventArgs e)
        {
            RaiseEvent(e);
        }

        /// <summary>
        ///     Raised when the item's IsSelected property becomes false.
        /// </summary>
        public static readonly RoutedEvent UnselectedEvent = Selector.UnselectedEvent.AddOwner(typeof(DataGridRow));

        /// <summary>
        ///     Raised when the item's IsSelected property becomes false.
        /// </summary>
        public event RoutedEventHandler Unselected
        {
            add
            {
                AddHandler(UnselectedEvent, value);
            }
            remove
            {
                RemoveHandler(UnselectedEvent, value);
            }
        }

        /// <summary>
        ///     Called when IsSelected becomes false. Raises the Unselected event.
        /// </summary>
        /// <param name="e">Empty event arguments.</param>
        protected virtual void OnUnselected(RoutedEventArgs e)
        {
            RaiseEvent(e);
        }

        /// <summary>
        ///     Determines if a row can be selected, based on the DataGrid's SelectionUnit property.
        /// </summary>
        private bool IsSelectable
        {
            get
            {
                DataGrid dataGrid = DataGridOwner;
                if (dataGrid != null)
                {
                    DataGridSelectionUnit unit = dataGrid.SelectionUnit;
                    return (unit == DataGridSelectionUnit.FullRow) ||
                        (unit == DataGridSelectionUnit.CellOrRowHeader);
                }

                return true;
            }
        }

        #endregion

        #region Editing

        /// <summary>
        ///     Whether the row is in editing mode.
        /// </summary>
        public bool IsEditing
        {
            get { return (bool)GetValue(IsEditingProperty); }
            internal set { SetValue(IsEditingPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey IsEditingPropertyKey =
            DependencyProperty.RegisterReadOnly("IsEditing", typeof(bool), typeof(DataGridRow), new FrameworkPropertyMetadata(false));

        /// <summary>
        ///     The DependencyProperty for IsEditing.
        /// </summary>
        public static readonly DependencyProperty IsEditingProperty = IsEditingPropertyKey.DependencyProperty;

        #endregion

        #region Frozen Columns

        /// <summary>
        /// Method which gets called when horizontal scroll happens on the scroll viewer of datagrid
        /// </summary>
        internal void OnHorizontalScroll()
        {
            if (CellsPresenter != null)
            {
                CellsPresenter.OnHorizontalScroll();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        ///     Returns the index of this row within the DataGrid's list of item containers.
        /// </summary>
        /// <remarks>
        ///     This method performs a linear search.
        /// </remarks>
        /// <returns>The index, if found, -1 otherwise.</returns>
        public int GetIndex()
        {
            DataGrid dataGridOwner = DataGridOwner;
            if (dataGridOwner != null)
            {
                return dataGridOwner.ItemContainerGenerator.IndexFromContainer(this);
            }

            return -1;
        }

        /// <summary>
        ///     Searchs up the visual parent chain from the given element until
        ///     a DataGridRow element is found.
        /// </summary>
        /// <param name="element">The descendent of a DataGridRow.</param>
        /// <returns>
        ///     The first ancestor DataGridRow of the element parameter.
        ///     Returns null of none is found.
        /// </returns>
        public static DataGridRow GetRowContainingElement(FrameworkElement element)
        {
            return DataGridHelper.FindVisualParent<DataGridRow>(element);
        }

        internal DataGrid DataGridOwner
        {
            get { return _owner; }
        }

        /// <summary>
        /// Returns true if the CellsPresenter is supposed to draw the gridlines for the row.
        /// </summary>
        internal bool CellsPresenterDrawsGridLines
        {
            get { return _detailsPresenter == null; }
        }

        /// <summary>
        /// Returns true if the DetailsPresenter is supposed to draw gridlines for the row.  Only true
        /// if the DetailsPresenter hooked itself up properly to the Row.
        /// </summary>
        internal bool DetailsPresenterDrawsGridLines
        {
            get { return _detailsPresenter != null; }
        }

        /// <summary>
        ///     Acceses the CellsPresenter and attempts to get the cell at the given index.
        ///     This is not necessarily the display order.
        /// </summary>
        internal DataGridCell TryGetCell(int index)
        {
            DataGridCellsPresenter cellsPresenter = CellsPresenter;
            if (cellsPresenter != null)
            {
                return cellsPresenter.ItemContainerGenerator.ContainerFromIndex(index) as DataGridCell;
            }

            return null;
        }

        #endregion

        #region Data

        private DataGrid                        _owner;
        private DataGridCellsPresenter          _cellsPresenter;
        private DataGridDetailsPresenter        _detailsPresenter;
        private DataGridRowHeader               _rowHeader;
        private ContainerTracking<DataGridRow>  _tracker;
        
        #endregion

    }
}