//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MS.Internal;
using System.Text;
using System.Security;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     A DataGrid control that displays data in rows and columns and allows
    ///     for the entering and editing of data.
    /// </summary>
    public class DataGrid : MultiSelector
    {
        #region Constructors

        /// <summary>
        ///     Instantiates global information.
        /// </summary>
        static DataGrid()
        {
            Type ownerType = typeof(DataGrid);

            DefaultStyleKeyProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(typeof(DataGrid)));
            ItemsPanelProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(new ItemsPanelTemplate(new FrameworkElementFactory(typeof(DataGridRowsPresenter)))));
            VirtualizingStackPanel.VirtualizationModeProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(VirtualizationMode.Recycling));
            ItemContainerStyleProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(null, new CoerceValueCallback(OnCoerceItemContainerStyle)));
            ItemContainerStyleSelectorProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(null, new CoerceValueCallback(OnCoerceItemContainerStyleSelector)));
            AlternationCountProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(0, null, new CoerceValueCallback(OnCoerceAlternationCount)));
            IsEnabledProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(new PropertyChangedCallback(OnIsEnabledChanged)));

            IsTabStopProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(false));
            KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(KeyboardNavigationMode.Contained));
            KeyboardNavigation.ControlTabNavigationProperty.OverrideMetadata(ownerType, new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));

            CommandManager.RegisterClassInputBinding(ownerType, new InputBinding(BeginEditCommand, new KeyGesture(Key.F2)));
            CommandManager.RegisterClassCommandBinding(ownerType, new CommandBinding(BeginEditCommand, new ExecutedRoutedEventHandler(OnExecutedBeginEdit), new CanExecuteRoutedEventHandler(OnCanExecuteBeginEdit)));

            CommandManager.RegisterClassCommandBinding(ownerType, new CommandBinding(CommitEditCommand, new ExecutedRoutedEventHandler(OnExecutedCommitEdit), new CanExecuteRoutedEventHandler(OnCanExecuteCommitEdit)));

            CommandManager.RegisterClassInputBinding(ownerType, new InputBinding(CancelEditCommand, new KeyGesture(Key.Escape)));
            CommandManager.RegisterClassCommandBinding(ownerType, new CommandBinding(CancelEditCommand, new ExecutedRoutedEventHandler(OnExecutedCancelEdit), new CanExecuteRoutedEventHandler(OnCanExecuteCancelEdit)));

            CommandManager.RegisterClassInputBinding(ownerType, new InputBinding(SelectAllCommand, DataGridHelper.CreateFromResourceStrings(SR.Get(SRID.DataGrid_SelectAllKey), SR.Get(SRID.DataGrid_SelectAllKeyDisplayString))));
            CommandManager.RegisterClassCommandBinding(ownerType, new CommandBinding(SelectAllCommand, new ExecutedRoutedEventHandler(OnExecutedSelectAll), new CanExecuteRoutedEventHandler(OnCanExecuteSelectAll)));

            CommandManager.RegisterClassInputBinding(ownerType, new InputBinding(DeleteCommand, new KeyGesture(Key.Delete)));
            CommandManager.RegisterClassCommandBinding(ownerType, new CommandBinding(DeleteCommand, new ExecutedRoutedEventHandler(OnExecutedDelete), new CanExecuteRoutedEventHandler(OnCanExecuteDelete)));

            // Default Clipboard handling
            CommandManager.RegisterClassCommandBinding(typeof(DataGrid), new CommandBinding(ApplicationCommands.Copy, new ExecutedRoutedEventHandler(OnExecutedCopy), new CanExecuteRoutedEventHandler(OnCanExecuteCopy)));

            EventManager.RegisterClassHandler(typeof(DataGrid), MouseUpEvent, new MouseButtonEventHandler(OnAnyMouseUpThunk), true);
        }

        /// <summary>
        ///     Instantiates a new instance of this class.
        /// </summary>
        public DataGrid()
        {
            _columns = new DataGridColumnCollection(this);
            _columns.CollectionChanged += new NotifyCollectionChangedEventHandler(OnColumnsChanged);

            _selectedCells = new SelectedCellsCollection(this);

            ((INotifyCollectionChanged)Items).CollectionChanged += new NotifyCollectionChangedEventHandler(OnItemsCollectionChanged);

            ((INotifyCollectionChanged)(Items.SortDescriptions)).CollectionChanged += new NotifyCollectionChangedEventHandler(OnItemsSortDescriptionsChanged);
            Items.GroupDescriptions.CollectionChanged += new NotifyCollectionChangedEventHandler(OnItemsGroupDescriptionsChanged);
        }

        #endregion

        #region Columns

        /// <summary>
        ///     A collection of column definitions describing the individual 
        ///     columns of each row.
        /// </summary>
        public ObservableCollection<DataGridColumn> Columns
        {
            get { return _columns; }
        }

        /// <summary>
        ///     Returns the column collection without having to upcast from ObservableCollection
        /// </summary>
        private DataGridColumnCollection InternalColumns
        {
            get { return _columns; }
        }

        /// <summary>
        ///     A property that specifies whether the user can resize columns in the UI by dragging the column headers.
        /// </summary>
        /// <remarks>
        ///     This does not affect whether column widths can be changed programmatically via a property such as Column.Width.
        /// </remarks>
        public bool CanUserResizeColumns
        {
            get { return (bool)GetValue(CanUserResizeColumnsProperty); }
            set { SetValue(CanUserResizeColumnsProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the CanUserResizeColumns property.
        /// </summary>
        public static readonly DependencyProperty CanUserResizeColumnsProperty =
            DependencyProperty.Register("CanUserResizeColumns", typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnNotifyColumnHeaderPropertyChanged)));

        /// <summary>
        ///     Specifies the width of the header and cells within all the columns.
        /// </summary>
        public DataGridLength ColumnWidth
        {
            get { return (DataGridLength)GetValue(ColumnWidthProperty); }
            set { SetValue(ColumnWidthProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the ColumnWidth property.
        /// </summary>
        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register("ColumnWidth", typeof(DataGridLength), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridLength.SizeToHeader));


        /// <summary>
        ///     Specifies the minimum width of the header and cells within all columns.
        /// </summary>
        public double MinColumnWidth
        {
            get { return (double)GetValue(MinColumnWidthProperty); }
            set { SetValue(MinColumnWidthProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the MinColumnWidth property.
        /// </summary>
        public static readonly DependencyProperty MinColumnWidthProperty =
            DependencyProperty.Register("MinColumnWidth", typeof(double), typeof(DataGrid),
                                        new FrameworkPropertyMetadata(20d, new PropertyChangedCallback(OnColumnSizeConstraintChanged)),
                                        new ValidateValueCallback(ValidateMinColumnWidth));


        /// <summary>
        ///     Specifies the maximum width of the header and cells within all columns.
        /// </summary>
        public double MaxColumnWidth
        {
            get { return (double)GetValue(MaxColumnWidthProperty); }
            set { SetValue(MaxColumnWidthProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the  MaxColumnWidth property.
        /// </summary>
        public static readonly DependencyProperty MaxColumnWidthProperty =
            DependencyProperty.Register("MaxColumnWidth", typeof(double), typeof(DataGrid),
                                        new FrameworkPropertyMetadata(double.PositiveInfinity, new PropertyChangedCallback(OnColumnSizeConstraintChanged)),
                                        new ValidateValueCallback(ValidateMaxColumnWidth));


        private static void OnColumnSizeConstraintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.Columns);
        }

        /// <summary>
        /// Validates that the minimum column width is an acceptable value
        /// </summary>
        private static bool ValidateMinColumnWidth(object v)
        {
            double value = (double)v;
            return !(value < 0d || DoubleUtil.IsNaN(value) || Double.IsPositiveInfinity(value));
        }

        /// <summary>
        /// Validates that the maximum column width is an acceptable value
        /// </summary>
        private static bool ValidateMaxColumnWidth(object v)
        {
            double value = (double)v;
            return !(value < 0d || DoubleUtil.IsNaN(value));
        }

        /// <summary>
        ///     Called when the Columns collection changes.
        /// </summary>
        private void OnColumnsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Update the reference to this DataGrid on the affected column(s)
            // and update the SelectedCells collection.
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    UpdateDataGridReference(e.NewItems, /* clear = */ false);
                    UpdateColumnSizeConstraints(e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    UpdateDataGridReference(e.OldItems, /* clear = */ true);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    UpdateDataGridReference(e.OldItems, /* clear = */ true);
                    UpdateDataGridReference(e.NewItems, /* clear = */ false);
                    UpdateColumnSizeConstraints(e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // We can't clear column references on Reset: _columns has 0 items and e.OldItems is empty.
                    _selectedCells.Clear();
                    break;
            }

            // FrozenColumns rely on column DisplayIndex
            // Delay the coercion if necessary
            if (InternalColumns.DisplayIndexMapInitialized)
            {
                CoerceValue(FrozenColumnCountProperty);
            }

            UpdateColumnsOnRows(e);
        }

        /// <summary>
        ///     Updates the reference to this DataGrid on the list of columns.
        /// </summary>
        /// <param name="list">The list of affected columns.</param>
        /// <param name="clear">Whether to add or remove the reference to this grid.</param>
        private void UpdateDataGridReference(IList list, bool clear)
        {
            int numItems = list.Count;
            for (int i = 0; i < numItems; i++)
            {
                DataGridColumn column = (DataGridColumn)list[i];
                if (clear)
                {
                    // Set the owner to null only if the current owner is this grid
                    if (column.DataGridOwner == this)
                    {
                        column.DataGridOwner = null;
                    }
                }
                else
                {
                    // Remove the column from any old owner
                    if (column.DataGridOwner != null && column.DataGridOwner != this)
                    {
                        column.DataGridOwner.Columns.Remove(column);
                    }

                    column.DataGridOwner = this;
                }
            }
        }

        /// <summary>
        ///     Updates the transferred size constraints from DataGrid on the columns.
        /// </summary>
        /// <param name="list">The list of affected columns.</param>
        private void UpdateColumnSizeConstraints(IList list)
        {
            var count = list.Count;
            for (var i = 0; i < count; i++)
            {
                var column = (DataGridColumn)list[i];
                column.SyncProperties();
            }
        }

        #endregion

        #region Display Index

        /// <summary>
        ///     Returns the DataGridColumn with the given DisplayIndex
        /// </summary>
        public DataGridColumn ColumnFromDisplayIndex(int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= Columns.Count)
            {
                throw new ArgumentOutOfRangeException("displayIndex", displayIndex, SR.Get(SRID.DataGrid_DisplayIndexOutOfRange));
            }

            return InternalColumns.ColumnFromDisplayIndex(displayIndex);
        }

        /// <summary>
        ///     Called when the DisplayIndex of a column is modified.
        /// </summary>
        /// <remarks>
        ///     A column's DisplayIndex may be modified as the result of another column's DisplayIndex changing.  This is because the 
        ///     DataGrid enforces that the DisplayIndex of all Columns are unique integers from 0 to Columns.Count -1.
        /// </remarks>
        protected internal virtual void OnColumnDisplayIndexChanged(DataGridColumnEventArgs e)
        {
        }


        /// <summary>
        ///     A map of display index (key) to index in the column collection (value).  
        ///     Used by the CellsPanel to quickly find a child from a column display index.
        /// </summary>
        internal List<int> DisplayIndexMap
        {
            get { return InternalColumns.DisplayIndexMap; }
        }

        /// <summary>
        ///     Throws an ArgumentOutOfRangeException if the given displayIndex is invalid.
        /// </summary>
        internal void ValidateDisplayIndex(DataGridColumn column, int displayIndex)
        {
            InternalColumns.ValidateDisplayIndex(column, displayIndex);
        }


        /// <summary>
        ///     Returns the index of a column from the given DisplayIndex
        /// </summary>
        internal int ColumnIndexFromDisplayIndex(int displayIndex)
        {
            if (displayIndex >= 0 && displayIndex < DisplayIndexMap.Count)
            {
                Debug.Assert(ColumnFromDisplayIndex(displayIndex).DisplayIndex == displayIndex, "DisplayIndexMap corrupted");
                return DisplayIndexMap[displayIndex];
            }

            return -1;
        }


        /// <summary>
        ///     Given the DisplayIndex of a column returns the DataGridColumnHeader for that column.
        ///     Used by DataGridColumnHeader to find its previous sibling.
        /// </summary>
        /// <param name="displayIndex"></param>
        /// <returns></returns>
        internal DataGridColumnHeader ColumnHeaderFromDisplayIndex(int displayIndex)
        {
            int columnIndex = ColumnIndexFromDisplayIndex(displayIndex);
          
            if (columnIndex != -1)
            {
                if (ColumnHeadersPresenter != null && ColumnHeadersPresenter.ItemContainerGenerator != null)
                {
                    return (DataGridColumnHeader)ColumnHeadersPresenter.ItemContainerGenerator.ContainerFromIndex(columnIndex);
                }
            }

            return null;
        }

        #endregion

        #region Notification Propagation

        /// <summary>
        ///     Notifies each CellsPresenter about property changes.
        /// </summary>
        private static void OnNotifyCellsPresenterPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.CellsPresenter);
        }

        /// <summary>
        ///     Notifies each Column and Cell about property changes.
        /// </summary>
        private static void OnNotifyColumnAndCellPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.Columns | NotificationTarget.Cells);
        }

        /// <summary>
        ///     Notifies each Column about property changes.
        /// </summary>
        private static void OnNotifyColumnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.Columns);
        }

        /// <summary>
        ///     Notifies the Column & Column Headers about property changes.
        /// </summary>
        private static void OnNotifyColumnAndColumnHeaderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.Columns | NotificationTarget.ColumnHeaders);
        }

        /// <summary>
        ///     Notifies the Column Headers about property changes.
        /// </summary>
        private static void OnNotifyColumnHeaderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.ColumnHeaders);
        }

        /// <summary>
        ///     Notifies the Row and Column Headers about property changes (used by the AlternationBackground property)
        /// </summary>
        private static void OnNotifyHeaderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.ColumnHeaders | NotificationTarget.RowHeaders);
        }

        /// <summary>
        ///     Notifies the DataGrid and each Row about property changes.
        /// </summary>
        private static void OnNotifyDataGridAndRowPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.Rows | NotificationTarget.DataGrid);
        }


        /// <summary>
        ///     Notifies everyone who cares about GridLine property changes (Row, Cell, RowHeader, ColumnHeader)
        /// </summary>
        private static void OnNotifyGridLinePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Clear out and regenerate all containers.  We do this so that we don't have to propagate this notification
            // to containers that are currently on the recycle queue -- doing so costs us perf on every scroll.  We don't
            // care about the time spent on a GridLine change since it'll be a very rare occurance.
            //
            // ItemsControl.OnItemTemplateChanged calls the internal ItemContainerGenerator.Refresh() method, which
            // clears out all containers and notifies the panel.  The fact we're passing in two null templates is ignored.
            //
            if (e.OldValue != e.NewValue)
            {
                ((DataGrid)d).OnItemTemplateChanged(null, null);
            }
        }


        /// <summary>
        ///     Notifies each Row about property changes.
        /// </summary>
        private static void OnNotifyRowPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.Rows);
        }


        /// <summary>
        ///     Notifies the Row Headers about property changes.
        /// </summary>
        private static void OnNotifyRowHeaderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.RowHeaders);
        }

        /// <summary>
        ///     Notifies the Row & Row Headers about property changes.
        /// </summary>
        private static void OnNotifyRowAndRowHeaderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.Rows | NotificationTarget.RowHeaders);
        }

        /// <summary>
        ///     General notification for DependencyProperty changes from the grid or from columns.
        /// </summary>
        /// <remarks>
        ///     This can be called from a variety of sources, such as from column objects
        ///     or from this DataGrid itself when there is a need to notify the rows and/or
        ///     the cells in the DataGrid about a property change. Down-stream handlers 
        ///     can check the source of the change using the "d" parameter.
        /// </remarks>
        internal void NotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e, NotificationTarget target)
        {
            NotifyPropertyChanged(d, string.Empty, e, target);
        }

        /// <summary>
        ///     General notification for DependencyProperty changes from the grid or from columns.
        /// </summary>
        /// <remarks>
        ///     This can be called from a variety of sources, such as from column objects
        ///     or from this DataGrid itself when there is a need to notify the rows and/or
        ///     the cells in the DataGrid about a property change. Down-stream handlers 
        ///     can check the source of the change using the "d" parameter.
        /// </remarks>
        internal void NotifyPropertyChanged(DependencyObject d, string propertyName, DependencyPropertyChangedEventArgs e, NotificationTarget target)
        {
            if (DataGridHelper.ShouldNotifyDataGrid(target) && e.Property == AlternatingRowBackgroundProperty)
            {
                // If the alternate row background is set, the count may be coerced to 2
                CoerceValue(AlternationCountProperty);
            }

            if (DataGridHelper.ShouldNotifyRowSubtree(target))  // Rows, Cells, CellsPresenter, DetailsPresenter or RowHeaders
            {
                // Notify the Rows about the property change
                ContainerTracking<DataGridRow> tracker = _rowTrackingRoot;
                while (tracker != null)
                {
                    tracker.Container.NotifyPropertyChanged(d, propertyName, e, target);
                    tracker = tracker.Next;
                }
            }

            if ((DataGridHelper.ShouldNotifyColumnHeadersPresenter(target) || DataGridHelper.ShouldNotifyColumnHeaders(target)) && ColumnHeadersPresenter != null)
            {
                ColumnHeadersPresenter.NotifyPropertyChanged(d, e, target);   
            }

            if (DataGridHelper.ShouldNotifyColumnCollection(target) || DataGridHelper.ShouldNotifyColumns(target))
            {
                InternalColumns.NotifyPropertyChanged(d, e, target);
            }
        }

        /// <summary>
        ///     Called by DataGridColumnCollection when columns' DisplayIndex changes
        /// </summary>
        /// <param name="e"></param>
        internal void UpdateColumnsOnVirtualizedCellInfoCollections(NotifyCollectionChangedAction action, int oldDisplayIndex, DataGridColumn oldColumn, int newDisplayIndex)
        {
            using (UpdateSelectedCells())
            {
                _selectedCells.OnColumnsChanged(action, oldDisplayIndex, oldColumn, newDisplayIndex, SelectedItems);
            }
        }

        /// <summary>
        ///     Reference to the ColumnHeadersPresenter. The presenter sets this when it is created.
        /// </summary>
        internal DataGridColumnHeadersPresenter ColumnHeadersPresenter
        {
            private get { return _columnHeadersPresenter; }
            set { _columnHeadersPresenter = value; }
        }

        /// <summary>
        ///     OnTemplateChanged override
        /// </summary>
        protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
        {
            base.OnTemplateChanged(oldTemplate, newTemplate);

            // Our column headers presenter comes from the template.  Clear out the reference to it if the template has changed
            ColumnHeadersPresenter = null;
        }

        /// <summary>
        ///     A cell is notifying the DataGrid that its IsKeyboardFocusWithin property changed.
        /// </summary>
        internal void CellIsKeyboardFocusWithinChanged(DataGridCell cell, bool isKeyboardFocusWithin)
        {
            UpdateCurrentCell(cell, isKeyboardFocusWithin);
        }

        #endregion

        #region GridLines

        /// <summary>
        ///     GridLinesVisibility Dependency Property
        /// </summary>
        public static readonly DependencyProperty GridLinesVisibilityProperty =
                    DependencyProperty.Register("GridLinesVisibility", typeof(DataGridGridLinesVisibility), typeof(DataGrid),
                                                new FrameworkPropertyMetadata(DataGridGridLinesVisibility.All, new PropertyChangedCallback(OnNotifyGridLinePropertyChanged)));


        /// <summary>
        ///     Specifies the visibility of the DataGrid's grid lines
        /// </summary>
        public DataGridGridLinesVisibility GridLinesVisibility
        {
            get { return (DataGridGridLinesVisibility)GetValue(GridLinesVisibilityProperty); }
            set { SetValue(GridLinesVisibilityProperty, value); }
        }


        /// <summary>
        /// HorizontalGridLinesBrush Dependency Property
        /// </summary>
        public static readonly DependencyProperty HorizontalGridLinesBrushProperty =
                    DependencyProperty.Register("HorizontalGridLinesBrush", typeof(Brush), typeof(DataGrid),
                                                new FrameworkPropertyMetadata(Brushes.Black, new PropertyChangedCallback(OnNotifyGridLinePropertyChanged)));


        /// <summary>
        /// Specifies the Brush used to draw the horizontal grid lines
        /// </summary>
        public Brush HorizontalGridLinesBrush
        {
            get { return (Brush)GetValue(HorizontalGridLinesBrushProperty); }
            set { SetValue(HorizontalGridLinesBrushProperty, value); }
        }

        /// <summary>
        /// VerticalGridLinesBrush Dependency Property
        /// </summary>
        public static readonly DependencyProperty VerticalGridLinesBrushProperty =
                    DependencyProperty.Register("VerticalGridLinesBrush", typeof(Brush), typeof(DataGrid),
                                                new FrameworkPropertyMetadata(Brushes.Black, new PropertyChangedCallback(OnNotifyGridLinePropertyChanged)));


        /// <summary>
        /// Specifies the Brush used to draw the vertical grid lines
        /// </summary>
        public Brush VerticalGridLinesBrush
        {
            get { return (Brush)GetValue(VerticalGridLinesBrushProperty); }
            set { SetValue(VerticalGridLinesBrushProperty, value); }
        }


#if GridLineThickness
        /// <summary>
        /// HorizontalGridLineThickness DependencyProperty
        /// </summary>
        public static readonly DependencyProperty HorizontalGridLineThicknessProperty =
                DependencyProperty.Register("HorizontalGridLineThickness", typeof(double), typeof(DataGrid),
                                            new FrameworkPropertyMetadata(1d, new PropertyChangedCallback(OnNotifyGridLinePropertyChanged)));
        
        /// <summary>
        /// Specifies the thickness of the horizontal grid lines.  
        /// </summary>
        public double HorizontalGridLineThickness 
        {
            get { return (double)GetValue(HorizontalGridLineThicknessProperty); }
            set { SetValue(HorizontalGridLineThicknessProperty, value); }
        }

        /// <summary>
        /// VerticalGridLineThickness DependencyProperty
        /// </summary>
        public static readonly DependencyProperty VerticalGridLineThicknessProperty =
                    DependencyProperty.Register("VerticalGridLineThickness", typeof(double), typeof(DataGrid),
                                                new FrameworkPropertyMetadata(1d, new PropertyChangedCallback(OnNotifyGridLinePropertyChanged)));


        /// <summary>
        /// Specifies the thickness of the vertical grid lines.  
        /// </summary>
        public double VerticalGridLineThickness
        {
            get { return (double)GetValue(VerticalGridLineThicknessProperty); }
            set { SetValue(VerticalGridLineThicknessProperty, value); }
        }

#else
        internal double HorizontalGridLineThickness
        {
            get { return 1.0; }
        }

        internal double VerticalGridLineThickness
        {
            get { return 1.0; }
        }
#endif


        #endregion 

        #region Row Generation

        /// <summary>
        ///     Determines if an item is its own container.
        /// </summary>
        /// <param name="item">The item to test.</param>
        /// <returns>true if the item is a DataGridRow, false otherwise.</returns>
        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is DataGridRow;
        }

        /// <summary>
        ///     Instantiates an instance of a container.
        /// </summary>
        /// <returns>A new DataGridRow.</returns>
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new DataGridRow();
        }

        /// <summary>
        ///     Prepares a new container for a given item.
        /// </summary>
        /// <param name="element">The new container.</param>
        /// <param name="item">The item that the container represents.</param>
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            DataGridRow row = (DataGridRow)element;
            if (row.DataGridOwner != this)
            {
                row.Tracker.StartTracking(ref _rowTrackingRoot);
            }
            row.PrepareRow(item, this);
        }

        /// <summary>
        ///     Clears a container of references.
        /// </summary>
        /// <param name="element">The container being cleared.</param>
        /// <param name="item">The data item that the container represented.</param>
        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            base.ClearContainerForItemOverride(element, item);

            DataGridRow row = (DataGridRow)element;
            if (row.DataGridOwner == this)
            {
                row.Tracker.StopTracking(ref _rowTrackingRoot);
            }
            row.ClearRow(item, this);
        }

        /// <summary>
        ///     Propagates the collection changed notification on Columns down to
        ///     each active DataGridRow.
        /// </summary>
        /// <param name="e">The event arguments from the original collection changed event.</param>
        private void UpdateColumnsOnRows(NotifyCollectionChangedEventArgs e)
        {
            ContainerTracking<DataGridRow> tracker = _rowTrackingRoot;
            while (tracker != null)
            {
                tracker.Container.OnColumnsChanged(_columns, e);
                tracker = tracker.Next;
            }
        }

        /// <summary>
        ///     Equivalent of ItemContainerStyle.
        /// </summary>
        /// <remarks>
        ///     If this property has a non-null value, it will override the value
        ///     of ItemContainerStyle.
        /// </remarks>
        public Style RowStyle
        {
            get { return (Style)GetValue(RowStyleProperty); }
            set { SetValue(RowStyleProperty, value); }
        }

        /// <summary>
        ///     DependencyProperty for the RowStyle property.
        /// </summary>
        public static readonly DependencyProperty RowStyleProperty =
            DependencyProperty.Register("RowStyle", typeof(Style), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnRowStyleChanged)));

        private static void OnRowStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            d.CoerceValue(ItemContainerStyleProperty);
        }

        private static object OnCoerceItemContainerStyle(DependencyObject d, object baseValue)
        {
            if (!DataGridHelper.IsDefaultValue(d, DataGrid.RowStyleProperty))
            {
                return d.GetValue(DataGrid.RowStyleProperty);
            }

            return baseValue;
        }

        /// <summary>
        ///     Equivalent of ItemContainerStyleSelector.
        /// </summary>
        /// <remarks>
        ///     If this property has a non-null value, it will override the value
        ///     of ItemContainerStyleSelector.
        /// </remarks>
        public StyleSelector RowStyleSelector
        {
            get { return (StyleSelector)GetValue(RowStyleSelectorProperty); }
            set { SetValue(RowStyleSelectorProperty, value); }
        }

        /// <summary>
        ///     DependencyProperty for the RowStyleSelector property.
        /// </summary>
        public static readonly DependencyProperty RowStyleSelectorProperty =
            DependencyProperty.Register("RowStyleSelector", typeof(StyleSelector), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnRowStyleSelectorChanged)));

        private static void OnRowStyleSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            d.CoerceValue(ItemContainerStyleSelectorProperty);
        }

        private static object OnCoerceItemContainerStyleSelector(DependencyObject d, object baseValue)
        {
            if (!DataGridHelper.IsDefaultValue(d, DataGrid.RowStyleSelectorProperty))
            {
                return d.GetValue(DataGrid.RowStyleSelectorProperty);
            }

            return baseValue;
        }

        /// <summary>
        ///     The default row background brush.
        /// </summary>
        public Brush RowBackground
        {
            get { return (Brush)GetValue(RowBackgroundProperty); }
            set { SetValue(RowBackgroundProperty, value); }
        }

        /// <summary>
        ///     DependencyProperty for RowBackground.
        /// </summary>
        public static readonly DependencyProperty RowBackgroundProperty =
            DependencyProperty.Register("RowBackground", typeof(Brush), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyRowPropertyChanged)));


        /// <summary>
        ///     The default row background brush for use on every other row.
        /// </summary>
        /// <remarks>
        ///     Setting this property to a non-null value will coerce AlternationCount to 2.
        /// </remarks>
        public Brush AlternatingRowBackground
        {
            get { return (Brush)GetValue(AlternatingRowBackgroundProperty); }
            set { SetValue(AlternatingRowBackgroundProperty, value); }
        }

        /// <summary>
        ///     DependencyProperty for AlternatingRowBackground.
        /// </summary>
        public static readonly DependencyProperty AlternatingRowBackgroundProperty =
            DependencyProperty.Register("AlternatingRowBackground", typeof(Brush), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyDataGridAndRowPropertyChanged)));

        private static object OnCoerceAlternationCount(DependencyObject d, object baseValue)
        {
            // Only check AlternatingRowBackground if the value isn't already set
            // to something that can use it.
            if (((int)baseValue) < 2)
            {
                DataGrid dataGrid = (DataGrid)d;
                if (dataGrid.AlternatingRowBackground != null)
                {
                    // There is an alternate background, coerce to 2.
                    return 2;
                }
            }

            return baseValue;
        }

        /// <summary>
        ///     The default height of a row.
        /// </summary>
        public double RowHeight
        {
            get { return (double)GetValue(RowHeightProperty); }
            set { SetValue(RowHeightProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for RowHeight.
        /// </summary>
        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register("RowHeight", typeof(double), typeof(DataGrid), new FrameworkPropertyMetadata(double.NaN, new PropertyChangedCallback(OnNotifyCellsPresenterPropertyChanged)));

        /// <summary>
        ///     The default minimum height of a row.
        /// </summary>
        public double MinRowHeight
        {
            get { return (double)GetValue(MinRowHeightProperty); }
            set { SetValue(MinRowHeightProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for MinRowHeight.
        /// </summary>
        public static readonly DependencyProperty MinRowHeightProperty =
            DependencyProperty.Register("MinRowHeight", typeof(double), typeof(DataGrid), new FrameworkPropertyMetadata(0.0, new PropertyChangedCallback(OnNotifyCellsPresenterPropertyChanged)));


        /// <summary>
        ///     The NewItemPlaceholder row uses this to set its visibility while it's preparing.
        /// </summary>
        internal Visibility PlaceholderVisibility
        {
            get
            {
                return _placeholderVisibility;
            }
        }

        #endregion

        #region Row/Column Headers

        /// <summary>
        ///     The default width of a row header.
        /// </summary>
        public double RowHeaderWidth
        {
            get { return (double)GetValue(RowHeaderWidthProperty); }
            set { SetValue(RowHeaderWidthProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for RowHeaderWidth.
        /// </summary>
        public static readonly DependencyProperty RowHeaderWidthProperty =
            DependencyProperty.Register("RowHeaderWidth", typeof(double), typeof(DataGrid), new FrameworkPropertyMetadata(double.NaN, new PropertyChangedCallback(OnNotifyRowHeaderWidthPropertyChanged)));

        /// <summary>
        ///     The actual width of row headers used for binding.  This is computed from the measure of all the visible row headers.
        /// </summary>
        public double RowHeaderActualWidth
        {
            get { return (double)GetValue(RowHeaderActualWidthProperty); }
            internal set { SetValue(RowHeaderActualWidthPropertyKey, value); }
        }

        /// <summary>
        ///     The DependencyPropertyKey for RowHeaderActualWidth.
        /// </summary>
        private static readonly DependencyPropertyKey  RowHeaderActualWidthPropertyKey = 
            DependencyProperty.RegisterReadOnly("RowHeaderActualWidth", typeof(double), typeof(DataGrid), new FrameworkPropertyMetadata(0.0, new PropertyChangedCallback(OnNotifyRowHeaderPropertyChanged)));

        /// <summary>
        ///     The DependencyProperty for RowHeaderActualWidth.
        /// </summary>
        public static readonly DependencyProperty RowHeaderActualWidthProperty = RowHeaderActualWidthPropertyKey.DependencyProperty;            


        /// <summary>
        ///     The default height of a column header.
        /// </summary>
        public double ColumnHeaderHeight
        {
            get { return (double)GetValue(ColumnHeaderHeightProperty); }
            set { SetValue(ColumnHeaderHeightProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for ColumnHeaderHeight.
        /// </summary>
        public static readonly DependencyProperty ColumnHeaderHeightProperty =
            DependencyProperty.Register("ColumnHeaderHeight", typeof(double), typeof(DataGrid), new FrameworkPropertyMetadata(double.NaN, OnNotifyColumnHeaderPropertyChanged));

        /// <summary>
        ///     A property that specifies the visibility of the column & row headers.
        /// </summary>
        public DataGridHeadersVisibility HeadersVisibility
        {
            get { return (DataGridHeadersVisibility)GetValue(HeadersVisibilityProperty); }
            set { SetValue(HeadersVisibilityProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the HeadersVisibility property.
        /// </summary>
        public static readonly DependencyProperty HeadersVisibilityProperty =
            DependencyProperty.Register("HeadersVisibility", typeof(DataGridHeadersVisibility), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridHeadersVisibility.All));


        /// <summary>
        ///     Updates RowHeaderActualWidth to reflect changes to RowHeaderWidth 
        /// </summary>
        private static void OnNotifyRowHeaderWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var dataGrid = ((DataGrid)d);
            var newValue = (double)e.NewValue;
            
            if (!DoubleUtil.IsNaN(newValue))
            {
                dataGrid.RowHeaderActualWidth = newValue;
            }
            else
            {
                //
                // If we're entering Auto mode we need to reset the RowHeaderActualWidth 
                // because the previous explicit value may have been bigger than the Auto width.
                //
                dataGrid.RowHeaderActualWidth = 0.0;
            }

            OnNotifyRowHeaderPropertyChanged(d, e);
        }

        /// <summary>
        /// Resets the RowHeaderActualWidth to 0.0 if in Auto mode
        /// </summary>
        private void ResetRowHeaderActualWidth()
        {
            if (DoubleUtil.IsNaN(RowHeaderWidth))
            {
                RowHeaderActualWidth = 0.0;
            }
        }

        #endregion

        #region Style Properties

        /// <summary>
        ///     A style to apply to all cells in the DataGrid.
        /// </summary>
        public Style CellStyle
        {
            get { return (Style)GetValue(CellStyleProperty); }
            set { SetValue(CellStyleProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the CellStyle property.
        /// </summary>
        public static readonly DependencyProperty CellStyleProperty = 
            DependencyProperty.Register("CellStyle", typeof(Style), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyColumnAndCellPropertyChanged)));

        /// <summary>
        ///     A style to apply to all column headers in the DataGrid
        /// </summary>
        public Style ColumnHeaderStyle
        {
            get { return (Style)GetValue(ColumnHeaderStyleProperty); }
            set { SetValue(ColumnHeaderStyleProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the ColumnHeaderStyle property.
        /// </summary>
        public static readonly DependencyProperty ColumnHeaderStyleProperty =
            DependencyProperty.Register("ColumnHeaderStyle", typeof(Style), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyColumnAndColumnHeaderPropertyChanged)));

        /// <summary>
        ///     A style to apply to all row headers in the DataGrid
        /// </summary>
        public Style RowHeaderStyle
        {
            get { return (Style)GetValue(RowHeaderStyleProperty); }
            set { SetValue(RowHeaderStyleProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty that represents the RowHeaderStyle property.
        /// </summary>
        public static readonly DependencyProperty RowHeaderStyleProperty =
            DependencyProperty.Register("RowHeaderStyle", typeof(Style), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnNotifyRowAndRowHeaderPropertyChanged)));

        /// <summary>
        ///     The default style references this brush to create a thicker border
        ///     around the focused cell.
        /// </summary>
        public static ComponentResourceKey FocusBorderBrushKey
        {
            get
            {
                if (_focusBorderBrushKey == null)
                {
                    _focusBorderBrushKey = new ComponentResourceKey(typeof(DataGrid), "FocusBorderBrushKey");
                }

                return _focusBorderBrushKey;
            }
        }

        /// <summary>
        ///     A converter which converts DataGridHeadersVisibility to VisibilityConverter based on a ConverterParameter.
        /// </summary>
        /// <remarks>
        ///     This can be used in the DataGrid's template to control which parts of the DataGrid are visible for a given DataGridHeadersVisibility.
        /// </remarks>
        public static IValueConverter HeadersVisibilityConverter
        {
            get
            {
                // This is delay created in case the template doesn't use it. 
                if (_headersVisibilityConverter == null)
                {
                    _headersVisibilityConverter = new DataGridHeadersVisibilityToVisibilityConverter();
                }

                return _headersVisibilityConverter;
            }
        }

        #endregion

        #region Scrolling

        /// <summary>
        ///     Defines the behavior that determines the visibility of horizontal ScrollBars.
        /// </summary>
        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get { return (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty); }
            set { SetValue(HorizontalScrollBarVisibilityProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the HorizontalScrollBarVisibility property.
        /// </summary>
        public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = ScrollViewer.HorizontalScrollBarVisibilityProperty.AddOwner(typeof(DataGrid), new FrameworkPropertyMetadata(ScrollBarVisibility.Auto));

        /// <summary>
        ///     Defines the behavior that determines the visibility of vertical ScrollBars.
        /// </summary>
        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get { return (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); }
            set { SetValue(VerticalScrollBarVisibilityProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the HorizontalScrollBarVisibility property.
        /// </summary>
        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = ScrollViewer.VerticalScrollBarVisibilityProperty.AddOwner(typeof(DataGrid), new FrameworkPropertyMetadata(ScrollBarVisibility.Auto));

        /// <summary>
        ///     Scrolls a row into view.
        /// </summary>
        /// <param name="item">The data item of the row to bring into view.</param>
        public void ScrollIntoView(object item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                ScrollRowIntoView(item);
            }
            else
            {
                // The items aren't generated, try at a later time
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new DispatcherOperationCallback(OnScrollIntoView), item);
            }
        }

        /// <summary>
        ///     Scrolls a cell into view.
        /// </summary>
        /// <param name="item">The data item row that contains the cell.</param>
        /// <param name="column">The cell's column.</param>
        public void ScrollIntoView(object item, DataGridColumn column)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }
            if (column == null)
            {
                throw new ArgumentNullException("column");
            }

            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                ScrollCellIntoView(item, column);
            }
            else
            {
                // The items aren't generated, try at a later time
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new DispatcherOperationCallback(OnScrollIntoView), new object[] { item, column });
            }
        }

        /// <summary>
        ///     Previous call to ScrollIntoView found that the generator had not finished
        ///     generating cells. This is the callback at Loaded priority when hopefully
        ///     that has occured.
        /// </summary>
        private object OnScrollIntoView(object arg)
        {
            object[] arguments = arg as object[];
            if (arguments != null)
            {
                ScrollCellIntoView(arguments[0], (DataGridColumn)arguments[1]);
            }
            else
            {
                ScrollRowIntoView(arg);
            }

            return null;
        }

        // TODO: Consider making a protected virtual so that sub-classes can customize the behavior
        private void ScrollRowIntoView(object item)
        {
            FrameworkElement element = ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (element != null)
            {
                element.BringIntoView();
            }
            else if (!IsGrouping)
            {
                // We might be virtualized, try scrolling by index.
                int index = Items.IndexOf(item);
                if (index >= 0)
                {
                    // It would be convenient for ItemsHost to be public, but since it is not,
                    // we are relying on some internal communication between DataGridRowsPresenter
                    // and the DataGrid.

                    DataGridRowsPresenter itemsHost = InternalItemsHost as DataGridRowsPresenter;
                    if (itemsHost != null)
                    {
                        // It would have been better to be able to directly call BringIndexIntoView,
                        // but that method is protected, so we are relying on an internal
                        // method on DataGridRowsPresenter to make the call.
                        itemsHost.InternalBringIndexIntoView(index);
                    }
                }
            }
        }

        // TODO: Consider making a protected virtual so that sub-classes can customize the behavior
        private void ScrollCellIntoView(object item, DataGridColumn column)
        {
            Debug.Assert(item != null, "item is null.");
            Debug.Assert(column != null, "column is null.");

            if (!TryScrollCellIntoView(item, column))
            {
                // The cell is probably virtualized, try to scroll the row first
                // and then hopefully the cell will exist.
                ScrollRowIntoView(item);
                UpdateLayout();
                TryScrollCellIntoView(item, column);
            }
        }

        private bool TryScrollCellIntoView(object item, DataGridColumn column)
        {
            DataGridCell cell = TryFindCell(item, column);
            if (cell != null)
            {
                cell.BringIntoView();

                // The cell was found and BringIntoView was called
                return true;
            }

            // The cell was not found, nothing happened
            return false;
        }

        /// <summary>
        ///     Called when IsMouseCaptured changes on this element.
        /// </summary>
        protected override void OnIsMouseCapturedChanged(DependencyPropertyChangedEventArgs e)
        {
            if (!IsMouseCaptured)
            {
                // When capture is lost, stop auto-scrolling
                StopAutoScroll();
            }

            base.OnIsMouseCapturedChanged(e);
        }

        private static TimeSpan AutoScrollTimeout
        {
            get
            {
                // NOTE: NtUser does the following (file: windows/ntuser/kernel/sysmet.c)
                //     gpsi->dtLBSearch = dtTime * 4;          // dtLBSearch   = 4   * gdtDblClk
                //     gpsi->dtScroll = gpsi->dtLBSearch / 5;  // dtScroll     = 4/5 * gdtDblClk

                return TimeSpan.FromMilliseconds(GetDoubleClickTime() * 0.8);
            }
        }

        /// <summary>
        ///     Begins a timer that will periodically scroll and select.
        /// </summary>
        private void StartAutoScroll()
        {
            if (_autoScrollTimer == null)
            {
                _hasAutoScrolled = false;
                // Same priority as ListBox. Currently choosing SystemIdle over ApplicationIdle since the layout
                // manger will do some work (sometimes) at ApplicationIdle.
                _autoScrollTimer = new DispatcherTimer(DispatcherPriority.SystemIdle);
                _autoScrollTimer.Interval = AutoScrollTimeout;
                _autoScrollTimer.Tick += new EventHandler(OnAutoScrollTimeout);
                _autoScrollTimer.Start();
            }
        }

        /// <summary>
        ///     Stops the timer that controls auto-scrolling.
        /// </summary>
        private void StopAutoScroll()
        {
            if (_autoScrollTimer != null)
            {
                _autoScrollTimer.Stop();
                _autoScrollTimer = null;
                _hasAutoScrolled = false;
            }
        }

        /// <summary>
        ///     The callback when the auto-scroll timer ticks.
        /// </summary>
        private void OnAutoScrollTimeout(object sender, EventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                DoAutoScroll();
            }
            else
            {
                StopAutoScroll();
            }
        }

        /// <summary>
        ///     Based on the mouse position relative to the rows and cells,
        ///     scrolls and selects rows and/or cells.
        /// </summary>
        /// <returns>true if a scroll and select was attempted. false otherwise.</returns>
        private bool DoAutoScroll()
        {
            Debug.Assert(_isDraggingSelection, "DoAutoScroll should only be called when dragging selection.");

            RelativeMousePositions position = RelativeMousePosition;
            if (position != RelativeMousePositions.Over)
            {
                // Get the cell that is nearest the mouse position and is
                // not being clipped by the ScrollViewer.
                DataGridCell cell = GetCellNearMouse();
                if (cell != null)
                {
                    DataGridColumn column = cell.Column;
                    object dataItem = cell.RowDataItem;

                    // Based on the position of the mouse relative to the field
                    // of cells, choose the cell that is torwards the mouse.
                    // Note: This assumes a grid layout.
                    if (IsMouseToLeft(position))
                    {
                        int columnIndex = column.DisplayIndex;
                        if (columnIndex > 0)
                        {
                            column = ColumnFromDisplayIndex(columnIndex - 1);
                        }

                    }
                    else if (IsMouseToRight(position))
                    {
                        int columnIndex = column.DisplayIndex;
                        if (columnIndex < (_columns.Count - 1))
                        {
                            column = ColumnFromDisplayIndex(columnIndex + 1);
                        }
                    }

                    if (IsMouseAbove(position))
                    {
                        int rowIndex = Items.IndexOf(dataItem);
                        if (rowIndex > 0)
                        {
                            dataItem = Items[rowIndex - 1];
                        }
                    }
                    else if (IsMouseBelow(position))
                    {
                        int rowIndex = Items.IndexOf(dataItem);
                        if (rowIndex < (Items.Count - 1))
                        {
                            dataItem = Items[rowIndex + 1];
                        }
                    }

                    if (_isRowDragging)
                    {
                        // Perform a row header drag-select
                        ScrollRowIntoView(dataItem);
                        DataGridRow row = (DataGridRow)ItemContainerGenerator.ContainerFromItem(dataItem);
                        if (row != null)
                        {
                            _hasAutoScrolled = true;
                            HandleSelectionForRowHeaderInput(row, /* startDragging = */ false);
                            CurrentItem = dataItem;
                            return true;
                        }
                    }
                    else
                    {
                        // Perform a cell drag-select
                        ScrollCellIntoView(dataItem, column);
                        cell = TryFindCell(dataItem, column);
                        if (cell != null)
                        {
                            _hasAutoScrolled = true;
                            HandleSelectionForCellInput(cell, /* startDragging = */ false, /* allowsExtendSelect = */ true, /* allowsMinimalSelect = */ true);
                            cell.Focus();
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Prevents the ScrollViewer from handling keyboard input.
        /// </summary>
        protected override bool HandlesScrolling
        {
            get { return true; }
        }

        /// <summary>
        ///     Workaround for not having access to ItemsControl.ItemsHost.
        /// </summary>
        internal Panel InternalItemsHost
        {
            get { return _internalItemsHost; }
            set { _internalItemsHost = value; }
        }

        /// <summary>
        ///     Workaround for not having access to ItemsControl.ScrollHost.
        /// </summary>
        internal ScrollViewer InternalScrollHost
        {
            get
            {
                if (_internalScrollHost == null && _internalItemsHost != null)
                {
                    _internalScrollHost = DataGridHelper.FindVisualParent<ScrollViewer>(_internalItemsHost);
                }

                return _internalScrollHost;
            }
        }

        #endregion

        #region Editing Commands

        /// <summary>
        ///     The command to fire and allow to route to the DataGrid in order to indicate that the
        ///     current cell or row should begin editing.
        /// </summary>
        public static readonly RoutedCommand BeginEditCommand = new RoutedCommand(SR.Get(SRID.DataGrid_BeginEditCommandText), typeof(DataGrid));

        /// <summary>
        ///     The command to fire and allow to route to the DataGrid in order to indicate that the
        ///     current cell or row should commit any pending changes and exit edit mode.
        /// </summary>
        public static readonly RoutedCommand CommitEditCommand = new RoutedCommand(SR.Get(SRID.DataGrid_CommitEditCommandText), typeof(DataGrid));

        /// <summary>
        ///     The command to fire and allow to route to the DataGrid in order to indicate that the
        ///     current cell or row should purge any pending changes and revert to the state it was
        ///     in before BeginEdit.
        /// </summary>
        public static readonly RoutedCommand CancelEditCommand = new RoutedCommand(SR.Get(SRID.DataGrid_CancelEditCommandText), typeof(DataGrid));

        /// <summary>
        ///     A command that, when invoked, will delete the current row.
        /// </summary>
        public static readonly RoutedCommand DeleteCommand = new RoutedCommand(SR.Get(SRID.DataGrid_DeleteCommandText), typeof(DataGrid));

        private static void OnCanExecuteBeginEdit(object sender, CanExecuteRoutedEventArgs e)
        {
            ((DataGrid)sender).OnCanExecuteBeginEdit(e);
        }

        private static void OnExecutedBeginEdit(object sender, ExecutedRoutedEventArgs e)
        {
            ((DataGrid)sender).OnExecutedBeginEdit(e);
        }

        /// <summary>
        ///     Invoked to determine if the BeginEdit command can be executed.
        /// </summary>
        protected virtual void OnCanExecuteBeginEdit(CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !IsReadOnly && (CurrentCellContainer != null) && !IsEditingCurrentCell && !IsCurrentCellReadOnly;
            e.Handled = true;
        }

        /// <summary>
        ///     Invoked when the BeginEdit command is executed.
        /// </summary>
        protected virtual void OnExecutedBeginEdit(ExecutedRoutedEventArgs e)
        {
            DataGridCell cell = CurrentCellContainer;
            if ((cell != null) && !cell.IsReadOnly && !cell.IsEditing)
            {
                bool addedPlaceholder = false;
                bool deselectedPlaceholder = false;
                bool reselectPlaceholderCells = false;
                List<int> columnIndexRanges = null;
                int newItemIndex = -1;
                object newItem = null;
                bool placeholderAtBeginning = (EditableItems.NewItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning);

                if (IsNewItemPlaceholder(cell.RowDataItem))
                {
                    // If editing the new item placeholder, then create a new item and edit that instead.

                    if (SelectedItems.Contains(CollectionView.NewItemPlaceholder))
                    {
                        // Unselect the NewItemPlaceholder and select the new row
                        UnselectItem(CollectionView.NewItemPlaceholder);
                        deselectedPlaceholder = true;
                    }
                    else
                    {
                        // Cells will automatically unselect when the new item placeholder is removed, but we
                        // should reselect them on the new item.
                        newItemIndex = Items.IndexOf(cell.RowDataItem);
                        reselectPlaceholderCells = ((newItemIndex >= 0) && _selectedCells.Intersects(newItemIndex, out columnIndexRanges));
                    }

                    newItem = AddNewItem();
                    CurrentItem = newItem; // Puts focus on the added row
                    cell = CurrentCellContainer;

                    if (deselectedPlaceholder)
                    {
                        // Re-select the new item if the placeholder was selected before
                        SelectItem(newItem);
                    }
                    else if (reselectPlaceholderCells)
                    {
                        // Re-select placeholder cells if they were selected before
                        using (UpdateSelectedCells())
                        {
                            int rowIndex = newItemIndex;

                            // When the placeholder is at the beginning, we don't hide it, so those cells need to be unselected.
                            // The cells to select are also now one row below.
                            if (placeholderAtBeginning)
                            {
                                _selectedCells.RemoveRegion(newItemIndex, 0, 1, Columns.Count);
                                rowIndex++;
                            }

                            for (int i = 0, count = columnIndexRanges.Count; i < count; i += 2)
                            {
                                _selectedCells.AddRegion(rowIndex, columnIndexRanges[i], 1, columnIndexRanges[i + 1]);
                            }
                        }
                    }

                    addedPlaceholder = true;
                }

                RoutedEventArgs editingEventArgs = e.Parameter as RoutedEventArgs;

                // Give the callback an opportunity to cancel edit mode
                DataGridBeginningEditEventArgs beginningEditEventArgs = new DataGridBeginningEditEventArgs(cell.Column, cell.RowOwner, editingEventArgs);
                OnBeginningEdit(beginningEditEventArgs);

                if (beginningEditEventArgs.Cancel)
                {
                    if (deselectedPlaceholder)
                    {
                        // If the new item placeholder was deselected and the new item was selected,
                        // de-select the new item. Selecting the new item placeholder comes at the end.
                        // This is to accomodate the scenario where the new item placeholder only appears
                        // when not editing a new item.
                        UnselectItem(newItem);
                    }
                    else if (reselectPlaceholderCells && placeholderAtBeginning)
                    {
                        // When the placeholder is at the beginning, we need to unselect the added item cells.
                        _selectedCells.RemoveRegion(newItemIndex + 1, 0, 1, Columns.Count);
                    }

                    if (addedPlaceholder)
                    {
                        // The edit was canceled, cancel the new item
                        CancelRowItem();

                        // Display the new item placeholder again
                        UpdateNewItemPlaceholder(/* isAddingNewItem = */ false);

                        // Put focus back on the placeholder
                        SetCurrentItemToPlaceholder();
                    }

                    if (deselectedPlaceholder)
                    {
                        // If the new item placeholder was deselected, then select it again.
                        SelectItem(CollectionView.NewItemPlaceholder);
                    }
                    else if (reselectPlaceholderCells)
                    {
                        for (int i = 0, count = columnIndexRanges.Count; i < count; i += 2)
                        {
                            _selectedCells.AddRegion(newItemIndex, columnIndexRanges[i], 1, columnIndexRanges[i + 1]);
                        }
                    }
                }
                else
                {
                    if (!addedPlaceholder && !IsEditingRowItem)
                    {
                        EditRowItem(cell.RowDataItem);
                    }

                    cell.BeginEdit(editingEventArgs);
                    cell.RowOwner.IsEditing = true;
                }
            }

            // CancelEdit and CommitEdit rely on IsAddingNewItem and IsEditingRowItem
            CommandManager.InvalidateRequerySuggested();

            e.Handled = true;
        }

        private static void OnCanExecuteCommitEdit(object sender, CanExecuteRoutedEventArgs e)
        {
            ((DataGrid)sender).OnCanExecuteCommitEdit(e);
        }

        private static void OnExecutedCommitEdit(object sender, ExecutedRoutedEventArgs e)
        {
            ((DataGrid)sender).OnExecutedCommitEdit(e);
        }

        private bool CanEndEdit(CanExecuteRoutedEventArgs e, bool commit)
        {
            // If the command routed through a cell, then use that cell. Otherwise, use the current cell.
            UIElement source = e.OriginalSource as UIElement;
            DataGridCell cellContainer = ((source == this) || (source == null)) ? CurrentCellContainer : DataGridHelper.FindVisualParent<DataGridCell>(source);
            if (cellContainer == null)
            {
                // If there is no cell, then nothing can be determined. So, no edit could end.
                return false;
            }

            DataGridEditingUnit editingUnit = GetEditingUnit(e.Parameter);
            IEditableCollectionView editableItems = EditableItems;
            object rowItem = cellContainer.RowDataItem;

            // Check that there is an appropriate pending add or edit.
            // - If any cell is in edit mode
            // - OR If the editing unit is row AND one of:
            //   - There is a pending add OR
            //   - There is a pending edit
            return (cellContainer.IsEditing || 
                   ((editingUnit == DataGridEditingUnit.Row) && 
                     ((editableItems.IsAddingNew && (editableItems.CurrentAddItem == rowItem)) || 
                      (editableItems.IsEditingItem && (commit || editableItems.CanCancelEdit) && (editableItems.CurrentEditItem == rowItem)))));
        }

        /// <summary>
        ///     Invoked to determine if the CommitEdit command can be executed.
        /// </summary>
        protected virtual void OnCanExecuteCommitEdit(CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanEndEdit(e, /* commit = */ true);
            e.Handled = true;
        }

        /// <summary>
        ///     Invoked when the CommitEdit command is executed.
        /// </summary>
        protected virtual void OnExecutedCommitEdit(ExecutedRoutedEventArgs e)
        {
            DataGridCell cell = CurrentCellContainer;
            if (cell != null)
            {
                DataGridEditingUnit editingUnit = GetEditingUnit(e.Parameter);

                bool eventCanceled = false;
                if (cell.IsEditing)
                {
                    DataGridEndingEditEventArgs endingEditEventArgs = new DataGridEndingEditEventArgs(cell.Column, cell.RowOwner, cell.EditingElement, DataGridEditingUnit.Cell);
                    OnCommittingEdit(endingEditEventArgs);

                    eventCanceled = endingEditEventArgs.Cancel;
                    if (!eventCanceled)
                    {
                        cell.CommitEdit();
                    }
                }

                if (!eventCanceled && (editingUnit == DataGridEditingUnit.Row) && IsAddingOrEditingRowItem(cell.RowDataItem))
                {
                    DataGridEndingEditEventArgs endingEditEventArgs = new DataGridEndingEditEventArgs(null, cell.RowOwner, null, DataGridEditingUnit.Row);
                    OnCommittingEdit(endingEditEventArgs);

                    if (!endingEditEventArgs.Cancel)
                    {
                        CommitRowItem();
                    }
                }

                // Update the state of row editing
                UpdateRowEditing(cell);

                // CancelEdit and CommitEdit rely on IsAddingNewItem and IsEditingRowItem
                CommandManager.InvalidateRequerySuggested();
            }

            e.Handled = true;
        }

        private DataGridEditingUnit GetEditingUnit(object parameter)
        {
            // If the parameter contains a DataGridEditingUnit, then use it.
            // Otherwise, choose Cell if a cell is currently being edited, or Row if not.
            return ((parameter != null) && (parameter is DataGridEditingUnit)) ?
                    (DataGridEditingUnit)parameter :
                    IsEditingCurrentCell ? DataGridEditingUnit.Cell : DataGridEditingUnit.Row;
        }

        /// <summary>
        ///     Raised just before editing is committed.
        ///     Gives handlers the opportunity to cancel the operation.
        /// </summary>
        public event EventHandler<DataGridEndingEditEventArgs> CommittingEdit;

        /// <summary>
        ///     Called just before editing is committed.
        ///     Gives subclasses the opportunity to cancel the operation.
        /// </summary>
        protected virtual void OnCommittingEdit(DataGridEndingEditEventArgs e)
        {
            if (CommittingEdit != null)
            {
                CommittingEdit(this, e);
            }
        }

        private static void OnCanExecuteCancelEdit(object sender, CanExecuteRoutedEventArgs e)
        {
            ((DataGrid)sender).OnCanExecuteCancelEdit(e);
        }

        private static void OnExecutedCancelEdit(object sender, ExecutedRoutedEventArgs e)
        {
            ((DataGrid)sender).OnExecutedCancelEdit(e);
        }

        /// <summary>
        ///     Invoked to determine if the CancelEdit command can be executed.
        /// </summary>
        protected virtual void OnCanExecuteCancelEdit(CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanEndEdit(e, /* commit = */ false);
            e.Handled = true;
        }

        /// <summary>
        ///     Invoked when the CancelEdit command is executed.
        /// </summary>
        protected virtual void OnExecutedCancelEdit(ExecutedRoutedEventArgs e)
        {
            DataGridCell cell = CurrentCellContainer;
            if (cell != null)
            {
                DataGridEditingUnit editingUnit = GetEditingUnit(e.Parameter);

                bool eventCanceled = false;
                if (cell.IsEditing)
                {
                    DataGridEndingEditEventArgs endingEditEventArgs = new DataGridEndingEditEventArgs(cell.Column, cell.RowOwner, cell.EditingElement, DataGridEditingUnit.Cell);
                    OnCancelingEdit(endingEditEventArgs);

                    eventCanceled = endingEditEventArgs.Cancel;
                    if (!eventCanceled)
                    {
                        cell.CancelEdit();
                    }
                }

                if (!eventCanceled && CanCancelAddingOrEditingRowItem(editingUnit, cell.RowDataItem))
                {
                    DataGridEndingEditEventArgs endingEditEventArgs = new DataGridEndingEditEventArgs(null, cell.RowOwner, null, DataGridEditingUnit.Row);
                    OnCancelingEdit(endingEditEventArgs);

                    if (!endingEditEventArgs.Cancel)
                    {
                        CancelRowItem();
                    }
                }

                // Update the state of row editing
                UpdateRowEditing(cell);

                // CancelEdit and CommitEdit rely on IsAddingNewItem and IsEditingRowItem
                CommandManager.InvalidateRequerySuggested();
            }

            e.Handled = true;
        }

        /// <summary>
        ///     Raised just before editing is canceled.
        ///     Gives handlers the opportunity to cancel the operation.
        /// </summary>
        public event EventHandler<DataGridEndingEditEventArgs> CancelingEdit;

        /// <summary>
        ///     Called just before editing is canceled.
        ///     Gives subclasses the opportunity to cancel the operation.
        /// </summary>
        protected virtual void OnCancelingEdit(DataGridEndingEditEventArgs e)
        {
            if (CancelingEdit != null)
            {
                CancelingEdit(this, e);
            }
        }

        private static void OnCanExecuteDelete(object sender, CanExecuteRoutedEventArgs e)
        {
            ((DataGrid)sender).OnCanExecuteDelete(e);
        }

        private static void OnExecutedDelete(object sender, ExecutedRoutedEventArgs e)
        {
            ((DataGrid)sender).OnExecutedDelete(e);
        }

        /// <summary>
        ///     Invoked to determine if the Delete command can be executed.
        /// </summary>
        protected virtual void OnCanExecuteDelete(CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanUserDeleteRows &&  // User is allowed to delete
                CurrentCell.IsValid &&           // There is a current cell
                (DataItemsSelected > 0) &&       // There is a selection
                ((_currentCellContainer == null) || !_currentCellContainer.IsEditing); // Not editing a cell
            e.Handled = true;
        }

        /// <summary>
        ///     Invoked when the Delete command is executed.
        /// </summary>
        protected virtual void OnExecutedDelete(ExecutedRoutedEventArgs e)
        {
            if (DataItemsSelected > 0)
            {
                bool shouldDelete = false;
                bool isEditingRowItem = IsEditingRowItem;
                if (isEditingRowItem || IsAddingNewItem)
                {
                    // If editing or adding a row, cancel that edit.
                    if (CancelEdit(DataGridEditingUnit.Row) && isEditingRowItem)
                    {
                        // If adding, we're done. If editing, then an actual delete
                        // needs to happen.
                        shouldDelete = true;
                    }
                }
                else
                {
                    // There is no pending edit, just delete.
                    shouldDelete = true;
                }

                if (shouldDelete)
                {
                    // Normally, the current item will be within the selection,
                    // deteremine a new item to select once the items are removed.
                    int numSelected = SelectedItems.Count;
                    int indexToSelect = -1;
                    object currentItem = CurrentItem;
                    if (SelectedItems.Contains(currentItem)) // The current item is in the selection
                    {
                        // Choose the smaller index between the anchor and the current item
                        // as the index to select after the items are removed.
                        indexToSelect = Items.IndexOf(currentItem);
                        if (_selectionAnchor != null)
                        {
                            int anchorIndex = Items.IndexOf(_selectionAnchor.Value.Item);
                            if ((anchorIndex >= 0) && (anchorIndex < indexToSelect))
                            {
                                indexToSelect = anchorIndex;
                            }
                        }
                        indexToSelect = Math.Min(Items.Count - numSelected - 1, indexToSelect);
                    }

                    // Save off the selected items. The selected items are going to be cleared
                    // first as a performance optimization. When items are removed, they are checked
                    // against the selected items to be removed from that collection. This can be slow
                    // since each item could cause a linear search of the selected items collection.
                    // Since it is known that all of the selected items are going to be deleted, they
                    // can safely be unselected.
                    ArrayList itemsToRemove = new ArrayList(SelectedItems);

                    using (UpdateSelectedCells())
                    {
                        bool alreadyUpdating = IsUpdatingSelectedItems;
                        if (!alreadyUpdating)
                        {
                            BeginUpdateSelectedItems();
                        }
                        try
                        {
                            // Pre-emptively clear the selection lists
                            _selectedCells.ClearFullRows(SelectedItems);
                            SelectedItems.Clear();
                        }
                        finally
                        {
                            if (!alreadyUpdating)
                            {
                                EndUpdateSelectedItems();
                            }
                        }
                    }

                    // We are not going to defer the rest of the selection change due to existing
                    // Selector behavior. When an item is removed from the ItemsSource, the Selector
                    // will immediately remove it from SelectedItems. In this process, it starts a 
                    // defer, which asserts because this code would have already started a defer.

                    // Remove the items that are selected
                    for (int i = 0; i < numSelected; i++)
                    {
                        object itemToRemove = itemsToRemove[i];
                        if (itemToRemove != CollectionView.NewItemPlaceholder)
                        {
                            EditableItems.Remove(itemToRemove);
                        }
                    }

                    // Select a new item
                    if (indexToSelect >= 0)
                    {
                        object itemToSelect = Items[indexToSelect];

                        // This should focus the row and bring it into view.
                        CurrentItem = itemToSelect;

                        // Since the current cell should be in view, there should be a container
                        DataGridCell cell = CurrentCellContainer;
                        if (cell != null)
                        {
                            _selectionAnchor = null;
                            HandleSelectionForCellInput(cell, /* startDragging = */ false, /* allowsExtendSelect = */ false, /* allowsMinimalSelect = */ false);
                        }
                    }
                }
            }

            e.Handled = true;
        }

        #endregion

        #region Editing

        //
        // TODO: IsReadOnly is not complete, which is why it is currently internal.
        //

        /// <summary>
        ///     Whether the DataGrid's rows and cells can be placed in edit mode.
        /// </summary>
        internal bool IsReadOnly
        {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for IsReadOnly.
        /// </summary>
        internal static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnIsReadOnlyChanged)));

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                // When going from R/W to R/O, cancel any current edits
                ((DataGrid)d).CancelAnyEdit();
            }

            d.CoerceValue(CanUserAddRowsProperty);
            d.CoerceValue(CanUserDeleteRowsProperty);

            // Affects the IsReadOnly property on cells
            OnNotifyColumnAndCellPropertyChanged(d, e);
        }

        /// <summary>
        ///     The object (or row) that, if not in edit mode, can be edited.
        /// </summary>
        /// <remarks>
        ///     This is the data item for the row that either has or contains focus.
        /// </remarks>
        public object CurrentItem
        {
            get { return (object)GetValue(CurrentItemProperty); }
            set { SetValue(CurrentItemProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for CurrentItem.
        /// </summary>
        public static readonly DependencyProperty CurrentItemProperty =
            DependencyProperty.Register("CurrentItem", typeof(object), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurrentItemChanged)));

        private static void OnCurrentItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)d;
            DataGridCellInfo currentCell = dataGrid.CurrentCell;
            object newItem = e.NewValue;

            if (currentCell.Item != newItem)
            {
                // Update the CurrentCell structure with the new item
                dataGrid.CurrentCell = DataGridCellInfo.CreatePossiblyPartialCellInfo(newItem, currentCell.Column, dataGrid);
            }
        }

        /// <summary>
        ///     The column of the CurrentItem (row) that corresponds with the current cell.
        /// </summary>
        /// <remarks>
        ///     null indicates that a cell does not have focus. The row may still have focus.
        /// </remarks>
        public DataGridColumn CurrentColumn
        {
            get { return (DataGridColumn)GetValue(CurrentColumnProperty); }
            set { SetValue(CurrentColumnProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for CurrentColumn.
        /// </summary>
        public static readonly DependencyProperty CurrentColumnProperty =
            DependencyProperty.Register("CurrentColumn", typeof(DataGridColumn), typeof(DataGrid), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurrentColumnChanged)));

        private static void OnCurrentColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)d;
            DataGridCellInfo currentCell = dataGrid.CurrentCell;
            DataGridColumn newColumn = (DataGridColumn)e.NewValue;

            if (currentCell.Column != newColumn)
            {
                // Update the CurrentCell structure with the new column
                dataGrid.CurrentCell = DataGridCellInfo.CreatePossiblyPartialCellInfo(currentCell.Item, newColumn, dataGrid);
            }
        }
        
        /// <summary>
        ///     The cell that, if not in edit mode, can be edited.
        /// </summary>
        /// <remarks>
        ///     The value returned is a structure that provides enough information to describe
        ///     the cell. It is neither an actual reference to the cell container nor the value
        ///     displayed in a given cell.
        /// </remarks>
        public DataGridCellInfo CurrentCell
        {
            get { return (DataGridCellInfo)GetValue(CurrentCellProperty); }
            set { SetValue(CurrentCellProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for CurrentCell.
        /// </summary>
        public static readonly DependencyProperty CurrentCellProperty =
            DependencyProperty.Register("CurrentCell", typeof(DataGridCellInfo), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridCellInfo.Unset, new PropertyChangedCallback(OnCurrentCellChanged)));

        private static void OnCurrentCellChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)d;
            DataGridCellInfo oldCell = (DataGridCellInfo)e.OldValue;
            DataGridCellInfo currentCell = (DataGridCellInfo)e.NewValue;

            if (dataGrid.CurrentItem != currentCell.Item)
            {
                dataGrid.CurrentItem = currentCell.Item;
            }
            if (dataGrid.CurrentColumn != currentCell.Column)
            {
                dataGrid.CurrentColumn = currentCell.Column;
            }

            if (dataGrid._currentCellContainer != null)
            {
                // _currentCellContainer should still be the old container and not the new one.
                // If _currentCellContainer were null, then it should mean that no BeginEdit was called
                // so, we shouldn't be missing any EndEdits.

                if ((dataGrid.IsAddingNewItem || dataGrid.IsEditingRowItem) && (oldCell.Item != currentCell.Item))
                {
                    // There is a row edit pending and the current cell changed to another row.
                    // Commit the row, which also commits the cell.
                    dataGrid.EndEdit(CommitEditCommand, dataGrid._currentCellContainer, DataGridEditingUnit.Row, /* exitEditingMode = */ true);
                }
                else if (dataGrid._currentCellContainer.IsEditing)
                {
                    // Only the cell needs to commit.
                    dataGrid.EndEdit(CommitEditCommand, dataGrid._currentCellContainer, DataGridEditingUnit.Cell, /* exitEditingMode = */ true);
                }
            }

            dataGrid._currentCellContainer = null;

            if (currentCell.IsValid && dataGrid.IsKeyboardFocusWithin)
            {
                // If CurrentCell was set by the user and not through a focus change,
                // then focus must be updated, but only when the DataGrid already
                // has focus.
                DataGridCell cell = dataGrid._pendingCurrentCellContainer;
                if (cell == null)
                {
                    cell = dataGrid.CurrentCellContainer;
                    if (cell == null)
                    {
                        // The cell might be virtualized. Try to devirtualize by scrolling.
                        dataGrid.ScrollCellIntoView(currentCell.Item, currentCell.Column);
                        cell = dataGrid.CurrentCellContainer;
                    }
                }
                if ((cell != null) && !cell.IsKeyboardFocusWithin)
                {
                    cell.Focus();
                }
            }

            dataGrid.OnCurrentCellChanged(EventArgs.Empty);
        }

        /// <summary>
        ///     An event to notify that the value of CurrentCell changed.
        /// </summary>
        public event EventHandler<EventArgs> CurrentCellChanged;

        /// <summary>
        ///     Called when the value of CurrentCell changes.
        /// </summary>
        /// <param name="e">Empty event arguments.</param>
        protected virtual void OnCurrentCellChanged(EventArgs e)
        {
            if (CurrentCellChanged != null)
            {
                CurrentCellChanged(this, e);
            }
        }

        private void UpdateCurrentCell(DataGridCell cell, bool isKeyboardFocusWithin)
        {
            if (isKeyboardFocusWithin)
            {
                // Focus is within the cell, make it the current cell.
                CurrentCellContainer = cell;
            }
            else if (!IsKeyboardFocusWithin)
            {
                // Focus moved outside the DataGrid, so clear out the current cell.
                CurrentCellContainer = null;
            }

            // Focus is within the DataGrid but not within this particular cell.
            // Assume that focus is moving to another cell, and that cell will update
            // the current cell.
        }

        private DataGridCell CurrentCellContainer
        {
            get
            {
                if (_currentCellContainer == null)
                {
                    DataGridCellInfo currentCell = CurrentCell;
                    if (currentCell.IsValid)
                    {
                        _currentCellContainer = TryFindCell(currentCell);
                    }
                }

                return _currentCellContainer;
            }

            set
            {
                if ((_currentCellContainer != value) && 
                    ((value == null) || (value != _pendingCurrentCellContainer)))
                {
                    // Setting CurrentCell might cause some re-entrancy due to focus changes.
                    // We need to detect this without actually changing the value until after
                    // setting CurrentCell.
                    _pendingCurrentCellContainer = value;

                    // _currentCellContainer must remain intact while changing CurrentCell
                    // so that the previous edit can be committed.
                    if (value == null)
                    {
                        ClearValue(CurrentCellProperty);
                    }
                    else
                    {
                        CurrentCell = new DataGridCellInfo(value);
                    }

                    _pendingCurrentCellContainer = null;
                    _currentCellContainer = value;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool IsEditingCurrentCell
        {
            get
            {
                DataGridCell cell = CurrentCellContainer;
                if (cell != null)
                {
                    return cell.IsEditing;
                }

                return false;
            }
        }

        private bool IsCurrentCellReadOnly
        {
            get
            {
                DataGridCell cell = CurrentCellContainer;
                if (cell != null)
                {
                    return cell.IsReadOnly;
                }

                return false;
            }
        }

        /// <summary>
        ///     Called just before a cell will change to edit mode
        ///     to allow handlers to prevent the cell from entering edit mode.
        /// </summary>
        public event EventHandler<DataGridBeginningEditEventArgs> BeginningEdit;

        /// <summary>
        ///     Called just before a cell will change to edit mode
        ///     to all subclasses to prevent the cell from entering edit mode.
        /// </summary>
        /// <remarks>
        ///     Default implementation raises the BeginningEdit event.
        /// </remarks>
        protected virtual void OnBeginningEdit(DataGridBeginningEditEventArgs e)
        {
            if (BeginningEdit != null)
            {
                BeginningEdit(this, e);
            }
        }

        /// <summary>
        ///     Called after a cell has changed to editing mode to allow
        ///     handlers to modify the contents of the cell.
        /// </summary>
        public event EventHandler<DataGridPreparingCellForEditEventArgs> PreparingCellForEdit;

        /// <summary>
        ///     Called after a cell has changed to editing mode to allow
        ///     subclasses to modify the contents of the cell.
        /// </summary>
        /// <remarks>
        ///     Default implementation raises the PreparingCellForEdit event.
        ///     This method is invoked from DataGridCell (instead of DataGrid) once it has entered edit mode.
        /// </remarks>
        protected internal virtual void OnPreparingCellForEdit(DataGridPreparingCellForEditEventArgs e)
        {
            if (PreparingCellForEdit != null)
            {
                PreparingCellForEdit(this, e);
            }
        }

        /// <summary>
        ///     Raises the BeginEdit command, which will place the current cell or row into
        ///     edit mode.
        /// </summary>
        /// <remarks>
        ///     If the command is enabled, this will lead to the BeginningEdit and PreparingCellForEdit
        ///     overrides and events.
        /// </remarks>
        /// <returns>true if the current cell or row enters edit mode, false otherwise.</returns>
        public bool BeginEdit()
        {
            return BeginEdit(/* editingEventArgs = */ null);
        }

        /// <summary>
        ///     Raises the BeginEdit command, which will place the current cell or row into
        ///     edit mode.
        /// </summary>
        /// <remarks>
        ///     If the command is enabled, this will lead to the BeginningEdit and PreparingCellForEdit
        ///     overrides and events.
        /// </remarks>
        /// <param name="editingEventArgs">The event arguments, if any, that led to BeginEdit being called. May be null.</param>
        /// <returns>true if the current cell or row enters edit mode, false otherwise.</returns>
        public bool BeginEdit(RoutedEventArgs editingEventArgs)
        {
            if (!IsReadOnly)
            {
                DataGridCell cellContainer = CurrentCellContainer;
                if (cellContainer != null)
                {
                    if (!cellContainer.IsEditing &&
                        BeginEditCommand.CanExecute(editingEventArgs, cellContainer))
                    {
                        BeginEditCommand.Execute(editingEventArgs, cellContainer);
                    }

                    return cellContainer.IsEditing;
                }
            }

            return false;
        }

        /// <summary>
        ///     Raises the CancelEdit command.
        ///     If a cell is currently in edit mode, cancels the cell edit, but leaves any row edits alone.
        ///     If a cell is not in edit mode, then cancels any pending row edits.
        /// </summary>
        /// <returns>true if the current cell or row exits edit mode, false otherwise.</returns>
        public bool CancelEdit()
        {
            if (IsEditingCurrentCell)
            {
                return CancelEdit(DataGridEditingUnit.Cell);
            }
            else if (IsEditingRowItem || IsAddingNewItem)
            {
                return CancelEdit(DataGridEditingUnit.Row);
            }

            return true; // No one is in edit mode
        }

        /// <summary>
        ///     Raises the CancelEdit command.
        ///     Reverts any pending editing changes to the desired editing unit and exits edit mode.
        /// </summary>
        /// <param name="editingUnit">Whether to cancel edit mode of the current cell or current row.</param>
        /// <returns>true if the current cell or row exits edit mode, false otherwise.</returns>
        public bool CancelEdit(DataGridEditingUnit editingUnit)
        {
            return EndEdit(CancelEditCommand, CurrentCellContainer, editingUnit, true);
        }

        private void CancelAnyEdit()
        {
            if (IsAddingNewItem || IsEditingRowItem)
            {
                // There is a row edit in progress, cancel it, which will also cancel the cell edit.
                CancelEdit(DataGridEditingUnit.Row);
            }
            else if (IsEditingCurrentCell)
            {
                // Cancel the current cell edit.
                CancelEdit(DataGridEditingUnit.Cell);
            }
        }

        /// <summary>
        ///     Raises the CommitEdit command.
        ///     If a cell is currently being edited, commits any pending changes to the cell, but
        ///     leaves any pending changes to the row. This should mean that changes are propagated
        ///     from the editing environment to the pending row.
        ///     If a cell is not currently being edited, then commits any pending rows.
        /// </summary>
        /// <returns>true if the current cell or row exits edit mode, false otherwise.</returns>
        public bool CommitEdit()
        {
            if (IsEditingCurrentCell)
            {
                return CommitEdit(DataGridEditingUnit.Cell, true);
            }
            else if (IsEditingRowItem || IsAddingNewItem)
            {
                return CommitEdit(DataGridEditingUnit.Row, true);
            }

            return true; // No one is in edit mode
        }

        /// <summary>
        ///     Raises the CommitEdit command.
        ///     Commits any pending changes for the given editing unit and exits edit mode.
        /// </summary>
        /// <param name="editingUnit">Whether to commit changes for the current cell or current row.</param>
        /// <param name="exitEditingMode">Whether to exit edit mode.</param>
        /// <returns>true if the current cell or row exits edit mode, false otherwise.</returns>
        public bool CommitEdit(DataGridEditingUnit editingUnit, bool exitEditingMode)
        {
            return EndEdit(CommitEditCommand, CurrentCellContainer, editingUnit, exitEditingMode);
        }

        private void CommitAnyEdit()
        {
            if (IsAddingNewItem || IsEditingRowItem)
            {
                // There is a row edit in progress, commit it, which will also commit the cell edit.
                CommitEdit(DataGridEditingUnit.Row, /* exitEditingMode = */ true);
            }
            else if (IsEditingCurrentCell)
            {
                // Commit the current cell edit.
                CommitEdit(DataGridEditingUnit.Cell, /* exitEditingMode = */ true);
            }
        }

        private bool EndEdit(RoutedCommand command, DataGridCell cellContainer, DataGridEditingUnit editingUnit, bool exitEditMode)
        {
            bool cellLeftEditingMode = true;
            bool rowLeftEditingMode = true;

            if (cellContainer != null)
            {
                if (command.CanExecute(editingUnit, cellContainer))
                {
                    command.Execute(editingUnit, cellContainer);
                }

                cellLeftEditingMode = !cellContainer.IsEditing;
                rowLeftEditingMode = !IsEditingRowItem && !IsAddingNewItem;
            }

            if (!exitEditMode)
            {
                if (editingUnit == DataGridEditingUnit.Cell)
                {
                    if (cellContainer != null)
                    {
                        if (cellLeftEditingMode)
                        {
                            return BeginEdit(null);
                        }
                    }
                    else
                    {
                        // A cell was not placed in edit mode
                        return false;
                    }
                }
                else
                {
                    if (rowLeftEditingMode)
                    {
                        object rowItem = cellContainer.RowDataItem;
                        if (rowItem != null)
                        {
                            EditRowItem(rowItem);
                            return IsEditingRowItem;
                        }
                    }

                    // A row item was not placed in edit mode
                    return false;
                }
            }

            return cellLeftEditingMode && ((editingUnit == DataGridEditingUnit.Cell) || rowLeftEditingMode);
        }

        #endregion

        #region Row Editing

        /// <summary>
        ///     Whether the end-user can add new rows to the ItemsSource.
        /// </summary>
        public bool CanUserAddRows
        {
            get { return (bool)GetValue(CanUserAddRowsProperty); }
            set { SetValue(CanUserAddRowsProperty, value); }
        }

        /// <summary>
        ///     DependencyProperty for CanUserAddRows.
        /// </summary>
        public static readonly DependencyProperty CanUserAddRowsProperty =
            DependencyProperty.Register("CanUserAddRows", typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnCanUserAddRowsChanged), new CoerceValueCallback(OnCoerceCanUserAddRows)));

        private static void OnCanUserAddRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).UpdateNewItemPlaceholder(/* isAddingNewItem = */ false);
        }

        private static object OnCoerceCanUserAddRows(DependencyObject d, object baseValue)
        {
            return OnCoerceCanUserAddOrDeleteRows((DataGrid)d, (bool)baseValue, /* canUserAddRowsProperty = */ true);
        }

        private static bool OnCoerceCanUserAddOrDeleteRows(DataGrid dataGrid, bool baseValue, bool canUserAddRowsProperty)
        {
            // Only when the base value is true do we need to validate that the user
            // can actually add or delete rows.
            if (baseValue)
            {
                if (dataGrid.IsReadOnly || !dataGrid.IsEnabled)
                {
                    // Read-only/disabled DataGrids cannot be modified.
                    return false;
                }
                else
                {
                    if ((canUserAddRowsProperty && !dataGrid.EditableItems.CanAddNew) ||
                        (!canUserAddRowsProperty && !dataGrid.EditableItems.CanRemove))
                    {
                        // The collection view does not allow the add or delete action
                        return false;
                    }
                }
            }

            return baseValue;
        }

        /// <summary>
        ///     Whether the end-user can delete rows from the ItemsSource.
        /// </summary>
        public bool CanUserDeleteRows
        {
            get { return (bool)GetValue(CanUserDeleteRowsProperty); }
            set { SetValue(CanUserDeleteRowsProperty, value); }
        }

        /// <summary>
        ///     DependencyProperty for CanUserDeleteRows.
        /// </summary>
        public static readonly DependencyProperty CanUserDeleteRowsProperty =
            DependencyProperty.Register("CanUserDeleteRows", typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnCanUserDeleteRowsChanged), new CoerceValueCallback(OnCoerceCanUserDeleteRows)));

        private static void OnCanUserDeleteRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // The Delete command needs to have CanExecute run
            CommandManager.InvalidateRequerySuggested();
        }

        private static object OnCoerceCanUserDeleteRows(DependencyObject d, object baseValue)
        {
            return OnCoerceCanUserAddOrDeleteRows((DataGrid)d, (bool)baseValue, /* canUserAddRowsProperty = */ false);
        }

        /// <summary>
        ///     An event that is raised when a new item is created so that
        ///     developers can initialize the item with custom default values.
        /// </summary>
        public event InitializingNewItemEventHandler InitializingNewItem;
        
        /// <summary>
        ///     A method that is called when a new item is created so that
        ///     overrides can initialize the item with custom default values.
        /// </summary>
        /// <remarks>
        ///     The default implementation raises the InitializingNewItem event.
        /// </remarks>
        /// <param name="e">Event arguments that provide access to the new item.</param>
        protected virtual void OnInitializingNewItem(InitializingNewItemEventArgs e)
        {
            if (InitializingNewItem != null)
            {
                InitializingNewItem(this, e);
            }
        }

        private object AddNewItem()
        {
            Debug.Assert(CanUserAddRows, "AddNewItem called when the end-user cannot add new rows.");
            Debug.Assert(!IsAddingNewItem, "AddNewItem called when a pending add is taking place.");

            // Hide the placeholder
            UpdateNewItemPlaceholder(/* isAddingNewItem = */ true);

            object newItem = EditableItems.AddNew();
            if (newItem != null)
            {
                InitializingNewItemEventArgs e = new InitializingNewItemEventArgs(newItem);
                OnInitializingNewItem(e);
            }

            // CancelEdit and CommitEdit rely on IsAddingNewItem
            CommandManager.InvalidateRequerySuggested();

            return newItem;
        }

        private void EditRowItem(object rowItem)
        {
            EditableItems.EditItem(rowItem);

            // CancelEdit and CommitEdit rely on IsEditingRowItem
            CommandManager.InvalidateRequerySuggested();
        }

        private void CommitRowItem()
        {
            Debug.Assert(IsEditingRowItem || IsAddingNewItem, "CommitRowItem was called when a row was not being edited or added.");

            if (IsEditingRowItem)
            {
                EditableItems.CommitEdit();
            }
            else
            {
                EditableItems.CommitNew();

                // Show the placeholder again
                UpdateNewItemPlaceholder(/* isAddingNewItem = */ false);
            }
        }

        private void CancelRowItem()
        {
            Debug.Assert(IsEditingRowItem || IsAddingNewItem, "CancelRowItem was called when a row was not being edited or added.");

            if (IsEditingRowItem)
            {
                EditableItems.CancelEdit();
            }
            else
            {
                object currentAddItem = EditableItems.CurrentAddItem;
                bool wasCurrent = currentAddItem == CurrentItem;
                bool wasSelected = SelectedItems.Contains(currentAddItem);
                bool reselectPlaceholderCells = false;
                List<int> columnIndexRanges = null;
                int newItemIndex = -1;

                if (wasSelected)
                {
                    // Unselect the item that was being added
                    UnselectItem(currentAddItem);
                }
                else
                {
                    // Cells will automatically unselect when the new item is removed, but we
                    // should reselect them on the placeholder.
                    newItemIndex = Items.IndexOf(currentAddItem);
                    reselectPlaceholderCells = ((newItemIndex >= 0) && _selectedCells.Intersects(newItemIndex, out columnIndexRanges));
                }

                // Cancel the add and remove it from the collection
                EditableItems.CancelNew();

                // Show the placeholder again
                UpdateNewItemPlaceholder(/* isAddingNewItem = */ false);

                if (wasCurrent)
                {
                    // Focus the placeholder if the new item had focus
                    CurrentItem = CollectionView.NewItemPlaceholder;
                }

                if (wasSelected)
                {
                    // Re-select the placeholder if it was selected before
                    SelectItem(CollectionView.NewItemPlaceholder);
                }
                else if (reselectPlaceholderCells)
                {
                    // Re-select placeholder cells if they were selected before
                    using (UpdateSelectedCells())
                    {
                        int rowIndex = newItemIndex;
                        bool placeholderAtBeginning = (EditableItems.NewItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning);

                        // When the placeholder is at the beginning, we need to unselect the cells 
                        // in the added row and move those back to the previous row.
                        if (placeholderAtBeginning)
                        {
                            _selectedCells.RemoveRegion(newItemIndex, 0, 1, Columns.Count);
                            rowIndex--;
                        }

                        for (int i = 0, count = columnIndexRanges.Count; i < count; i += 2)
                        {
                            _selectedCells.AddRegion(rowIndex, columnIndexRanges[i], 1, columnIndexRanges[i + 1]);
                        }
                    }
                }
            }
        }

        private void UpdateRowEditing(DataGridCell cell)
        {
            var editableItems = EditableItems;
            object rowDataItem = cell.RowDataItem;

            if (IsEditingItem(rowDataItem) && !editableItems.CanCancelEdit)
            {
                // If the row is being edited (not added), but it doesn't support
                // pending changes, then tell the item to commit (this doesn't really
                // do anything except update the IEditableCollectionView state).
                editableItems.CommitEdit();
            }

            // If the row is not in edit/add mode, then clear its IsEditing flag.
            if (!IsAddingOrEditingRowItem(rowDataItem))
            {
                cell.RowOwner.IsEditing = false;
            }
        }

        private IEditableCollectionView EditableItems
        {
            get { return (IEditableCollectionView)Items; }
        }

        private bool IsAddingNewItem
        {
            get { return EditableItems.IsAddingNew; }
        }

        private bool IsEditingRowItem
        {
            get { return EditableItems.IsEditingItem; }
        }

        private bool IsAddingOrEditingRowItem(object item)
        {
            return IsEditingItem(item) ||
                (IsAddingNewItem && (EditableItems.CurrentAddItem == item));
        }

        private bool CanCancelAddingOrEditingRowItem(DataGridEditingUnit editingUnit, object item)
        {
            return ((editingUnit == DataGridEditingUnit.Row) &&
                    ((IsEditingItem(item) && EditableItems.CanCancelEdit) ||
                     (IsAddingNewItem && (EditableItems.CurrentAddItem == item))));
        }

        private bool IsEditingItem(object item)
        {
            return IsEditingRowItem && (EditableItems.CurrentEditItem == item);
        }

        private void UpdateNewItemPlaceholder(bool isAddingNewItem)
        {
            var newItemPlaceholderRow = (DataGridRow)ItemContainerGenerator.ContainerFromItem(CollectionView.NewItemPlaceholder);
            var editableItems = EditableItems;
            bool canUserAddRows = CanUserAddRows;
            bool hasNewItemPlaceholdRow = newItemPlaceholderRow != null;

            if (!isAddingNewItem)
            {
                //
                // NewItemPlaceholderPosition isn't a DP but we want to default to AtEnd instead of None (can only be done
                // when canUserAddRows becomes true).  This may override the users intent to make it None, however
                // they can work around this by resetting it to None after making a change which results in canUserAddRows
                // becoming true.
                //
                if (canUserAddRows &&
                    editableItems != null &&
                    editableItems.NewItemPlaceholderPosition == NewItemPlaceholderPosition.None)
                {
                    editableItems.NewItemPlaceholderPosition = NewItemPlaceholderPosition.AtEnd;
                }

                // Use the previous visibility
                if (hasNewItemPlaceholdRow)
                {
                    newItemPlaceholderRow.Visibility = _previousPlaceholderVisibility;
                }
                
                _placeholderVisibility = _previousPlaceholderVisibility;
            }
            else
            {
                // The placeholder should be hidden when at the end
                if (hasNewItemPlaceholdRow)
                {
                    if (canUserAddRows)
                    {
                        // During a row add, hide the placeholder, but store the previously desired position
                        // so that it can be restored later.
                        _previousPlaceholderVisibility = newItemPlaceholderRow.Visibility;
                    }
                
                    newItemPlaceholderRow.Visibility = Visibility.Collapsed;
                }

                _placeholderVisibility = Visibility.Collapsed;
            }
        }

        private void SetCurrentItemToPlaceholder()
        {
            NewItemPlaceholderPosition position = EditableItems.NewItemPlaceholderPosition;
            if (position == NewItemPlaceholderPosition.AtEnd)
            {
                int itemCount = Items.Count;
                if (itemCount > 0)
                {
                    CurrentItem = Items[itemCount - 1];
                }
            }
            else if (position == NewItemPlaceholderPosition.AtBeginning)
            {
                if (Items.Count > 0)
                {
                    CurrentItem = Items[0];
                }
            }
        }

        private int DataItemsCount
        {
            get
            {
                int itemsCount = Items.Count;
                
                // Subtract one if there is a new item placeholder
                if (HasNewItemPlaceholder)
                {
                    itemsCount--;
                }

                return itemsCount;
            }
        }

        private int DataItemsSelected
        {
            get
            {
                int itemsSelected = SelectedItems.Count;

                if (HasNewItemPlaceholder && SelectedItems.Contains(CollectionView.NewItemPlaceholder))
                {
                    itemsSelected--;
                }

                return itemsSelected;
            }
        }

        private bool HasNewItemPlaceholder
        {
            get
            {
                IEditableCollectionView editableItems = EditableItems;
                return (editableItems.NewItemPlaceholderPosition != NewItemPlaceholderPosition.None);
            }
        }

        private bool IsNewItemPlaceholder(object item)
        {
            return (item == CollectionView.NewItemPlaceholder) || (item == DataGrid.NewItemPlaceholder);
        }

        #endregion

        #region Selection

        /// <summary>
        ///     The currently selected cells.
        /// </summary>
        public IList<DataGridCellInfo> SelectedCells
        {
            get { return _selectedCells; }
        }

        internal SelectedCellsCollection SelectedCellsInternal
        {
            get { return _selectedCells; }
        }

        /// <summary>
        ///     Event that fires when the SelectedCells collection changes.
        /// </summary>
        public event SelectedCellsChangedEventHandler SelectedCellsChanged;

        /// <summary>
        ///     Direct notification from the SelectedCells collection of a change.
        /// </summary>
        internal void OnSelectedCellsChanged(NotifyCollectionChangedAction action, VirtualizedCellInfoCollection oldItems, VirtualizedCellInfoCollection newItems)
        {
            DataGridSelectionMode selectionMode = SelectionMode;
            DataGridSelectionUnit selectionUnit = SelectionUnit;
            if (!IsUpdatingSelectedCells && (selectionUnit == DataGridSelectionUnit.FullRow))
            {
                throw new InvalidOperationException(SR.Get(SRID.DataGrid_CannotSelectCell));
            }

            // Update the pending list of changes
            if (oldItems != null)
            {
                // When IsUpdatingSelectedCells is true, there may have been cells
                // added to _pendingSelectedCells that are now being removed.
                // These cells should be removed from _pendingSelectedCells and
                // not added to _pendingUnselectedCells.
                if (_pendingSelectedCells != null)
                {
                    VirtualizedCellInfoCollection.Xor(_pendingSelectedCells, oldItems);
                }

                if (_pendingUnselectedCells == null)
                {
                    _pendingUnselectedCells = oldItems;
                }
                else
                {
                    _pendingUnselectedCells.Union(oldItems);
                }
            }

            if (newItems != null)
            {
                // When IsUpdatingSelectedCells is true, there may have been cells
                // added to _pendingUnselectedCells that are now being removed.
                // These cells should be removed from _pendingUnselectedCells and
                // not added to _pendingSelectedCells.
                if (_pendingUnselectedCells != null)
                {
                    VirtualizedCellInfoCollection.Xor(_pendingUnselectedCells, newItems);
                }

                if (_pendingSelectedCells == null)
                {
                    _pendingSelectedCells = newItems;
                }
                else
                {
                    _pendingSelectedCells.Union(newItems);
                }
            }

            if (!IsUpdatingSelectedCells) // Not deferring change notifications
            {
                // This is most likely the case when SelectedCells was updated by
                // the application. In this case, some fix-up is required, and
                // the public event needs to fire.

                using (UpdateSelectedCells()) // This will fire the event on dispose
                {
                    if ((selectionMode == DataGridSelectionMode.Single) && // Single select mode
                        (action == NotifyCollectionChangedAction.Add) && // An item was added
                        (_selectedCells.Count > 1)) // There is more than one selected cell
                    {
                        // When in single selection mode and there is more than one selected
                        // cell, remove all cells but the new cell.
                        _selectedCells.RemoveAllButOne(newItems[0]);
                    }
                    else if ((action == NotifyCollectionChangedAction.Remove) && 
                             (oldItems != null) &&
                             (selectionUnit == DataGridSelectionUnit.CellOrRowHeader))
                    {
                        // If removed cells belong to rows that are selected, then the row
                        // needs to be unselected (other selected cells may remain selected).
                        bool alreadyUpdating = IsUpdatingSelectedItems;
                        if (!alreadyUpdating)
                        {
                            BeginUpdateSelectedItems();
                        }
                        try
                        {
                            object lastRowItem = null;
                            foreach (DataGridCellInfo cellInfo in oldItems)
                            {
                                // First ensure that we haven't already checked the item
                                object rowItem = cellInfo.Item;
                                if (rowItem != lastRowItem)
                                {
                                    lastRowItem = rowItem;

                                    if (SelectedItems.Contains(rowItem))
                                    {
                                        // Remove the item
                                        SelectedItems.Remove(rowItem);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (!alreadyUpdating)
                            {
                                EndUpdateSelectedItems();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Fires the public change event when there are pending cell changes.
        /// </summary>
        private void NotifySelectedCellsChanged()
        {
            if (((_pendingSelectedCells != null) && (_pendingSelectedCells.Count > 0)) || 
                ((_pendingUnselectedCells != null) && (_pendingUnselectedCells.Count > 0)))
            {
                // Create the new event args
                SelectedCellsChangedEventArgs e = new SelectedCellsChangedEventArgs(this, _pendingSelectedCells, _pendingUnselectedCells);

                // Calculate the previous and current selection counts to determine if commands need invalidating
                int currentSelectionCount = _selectedCells.Count;
                int unselectedCellCount = (_pendingUnselectedCells != null) ? _pendingUnselectedCells.Count : 0;
                int selectedCellCount = (_pendingSelectedCells != null) ? _pendingSelectedCells.Count : 0;
                int previousSelectionCount = currentSelectionCount - selectedCellCount + unselectedCellCount;

                // Clear the pending lists
                _pendingSelectedCells = null;
                _pendingUnselectedCells = null;

                // Fire the public event
                OnSelectedCellsChanged(e);

                // If old or new selection is empty - invalidate Copy command
                if ((previousSelectionCount == 0) || (currentSelectionCount == 0))
                {
                    // The Copy command needs to have CanExecute run
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        ///     Called when there are changes to the SelectedCells collection.
        /// </summary>
        /// <param name="e">Event arguments that indicate which cells were added or removed.</param>
        /// <remarks>
        ///     Base implementation fires the public SelectedCellsChanged event.
        /// </remarks>
        protected virtual void OnSelectedCellsChanged(SelectedCellsChangedEventArgs e)
        {
            if (SelectedCellsChanged != null)
            {
                SelectedCellsChanged(this, e);
            }
        }

        /// <summary>
        ///     A command that, when invoked, will select all items in the DataGrid.
        /// </summary>
        public static readonly RoutedCommand SelectAllCommand = new RoutedCommand(SR.Get(SRID.DataGrid_SelectAllCommandText), typeof(DataGrid));

        private static void OnCanExecuteSelectAll(object sender, CanExecuteRoutedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            e.CanExecute = (dataGrid.SelectionMode == DataGridSelectionMode.Extended) && dataGrid.IsEnabled;
            e.Handled = true;
        }

        private static void OnExecutedSelectAll(object sender, ExecutedRoutedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            if (dataGrid.SelectionUnit == DataGridSelectionUnit.Cell)
            {
                dataGrid.SelectAllCells();
            }
            else
            {
                dataGrid.SelectAllRows();
            }
            e.Handled = true;
        }

        private void SelectAllRows()
        {
            int numItems = Items.Count;
            int numColumns = _columns.Count;
            if ((numColumns > 0) && (numItems > 0))
            {
                using (UpdateSelectedCells())
                {
                    // Selecting the cells first is an optimization, which doesn't happen in a direct call to SelectAll.
                    _selectedCells.AddRegion(0, 0, numItems, numColumns);
                    SelectAll();
                }
            }
        }

        /// <summary>
        ///     Selects all cells.
        /// </summary>
        public void SelectAllCells()
        {
            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                SelectAllRows();
            }
            else
            {
                int numItems = Items.Count;
                int numColumns = _columns.Count;

                if ((numItems > 0) && (numColumns > 0))
                {
                    using (UpdateSelectedCells())
                    {
                        if (_selectedCells.Count > 0)
                        {
                            _selectedCells.Clear();
                        }

                        _selectedCells.AddRegion(0, 0, numItems, numColumns);
                    }
                }
            }
        }

        /// <summary>
        ///     Unselects all cells.
        /// </summary>
        public void UnselectAllCells()
        {
            DataGridSelectionUnit selectionUnit = SelectionUnit;

            using (UpdateSelectedCells())
            {
                if (selectionUnit != DataGridSelectionUnit.FullRow)
                {
                    // Unselect all of the cells
                    _selectedCells.Clear();
                }

                if (selectionUnit != DataGridSelectionUnit.Cell)
                {
                    // Unselect all the items
                    UnselectAll();
                }
            }
        }

        /// <summary>
        ///     Defines the selection behavior.
        /// </summary>
        /// <remarks>
        ///     The SelectionMode and the SelectionUnit properties together define
        ///     the selection behavior for the DataGrid.
        /// </remarks>
        public DataGridSelectionMode SelectionMode
        {
            get { return (DataGridSelectionMode)GetValue(SelectionModeProperty); }
            set { SetValue(SelectionModeProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the SelectionMode property.
        /// </summary>
        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register("SelectionMode", typeof(DataGridSelectionMode), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridSelectionMode.Extended, new PropertyChangedCallback(OnSelectionModeChanged)));

        private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)d;
            DataGridSelectionMode newSelectionMode = (DataGridSelectionMode)e.NewValue;
            bool changingToSingleMode = newSelectionMode == DataGridSelectionMode.Single;
            DataGridSelectionUnit selectionUnit = dataGrid.SelectionUnit;

            if (changingToSingleMode && (selectionUnit == DataGridSelectionUnit.Cell))
            {
                // Setting CanSelectMultipleItems affects SelectedItems, but DataGrid
                // needs to modify SelectedCells manually.
                using (dataGrid.UpdateSelectedCells())
                {
                    dataGrid._selectedCells.RemoveAllButOne();
                }
            }

            // Update whether multiple items can be selected. Setting this property
            // will remove items when going from multiple to single mode.
            dataGrid.CanSelectMultipleItems = (newSelectionMode != DataGridSelectionMode.Single);

            if (changingToSingleMode && (selectionUnit == DataGridSelectionUnit.CellOrRowHeader))
            {
                // In CellOrRowHeader, wait until after CanSelectMultipleItems is done removing items.
                if (dataGrid.SelectedItems.Count > 0)
                {
                    // If there is a selected item, then de-select all cells except for that one row.
                    using (dataGrid.UpdateSelectedCells())
                    {
                        dataGrid._selectedCells.RemoveAllButOneRow(dataGrid.Items.IndexOf(dataGrid.SelectedItems[0]));
                    }
                }
                else
                {
                    // If there is no selected item, then de-select all cells except for one.
                    using (dataGrid.UpdateSelectedCells())
                    {
                        dataGrid._selectedCells.RemoveAllButOne();
                    }
                }
            }
        }

        /// <summary>
        ///     Defines the selection behavior.
        /// </summary>
        /// <remarks>
        ///     The SelectionMode and the SelectionUnit properties together define
        ///     the selection behavior for the DataGrid.
        /// </remarks>
        public DataGridSelectionUnit SelectionUnit
        {
            get { return (DataGridSelectionUnit)GetValue(SelectionUnitProperty); }
            set { SetValue(SelectionUnitProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the SelectionUnit property.
        /// </summary>
        public static readonly DependencyProperty SelectionUnitProperty =
            DependencyProperty.Register("SelectionUnit", typeof(DataGridSelectionUnit), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridSelectionUnit.FullRow, new PropertyChangedCallback(OnSelectionUnitChanged)));

        private static void OnSelectionUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)d;
            DataGridSelectionUnit oldUnit = (DataGridSelectionUnit)e.OldValue;
            
            // Full wipe on unit change

            if (oldUnit != DataGridSelectionUnit.Cell)
            {
                dataGrid.UnselectAll();
            }

            if (oldUnit != DataGridSelectionUnit.FullRow)
            {
                using (dataGrid.UpdateSelectedCells())
                {
                    dataGrid._selectedCells.Clear();
                }
            }
        }

        /// <summary>
        ///     Called when SelectedItems changes.
        /// </summary>
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (!IsUpdatingSelectedCells)
            {
                using (UpdateSelectedCells())
                {
                    // Remove cells of rows that were deselected
                    int count = e.RemovedItems.Count;
                    for (int i = 0; i < count; i++)
                    {
                        object rowItem = e.RemovedItems[i];
                        UpdateSelectionOfCellsInRow(rowItem, /* isSelected = */ false);
                    }

                    // Add cells of rows that were selected
                    count = e.AddedItems.Count;
                    for (int i = 0; i < count; i++)
                    {
                        object rowItem = e.AddedItems[i];
                        UpdateSelectionOfCellsInRow(rowItem, /* isSelected = */ true);
                    }
                }
            }

            // Delete depends on the selection state
            CommandManager.InvalidateRequerySuggested();

            base.OnSelectionChanged(e);
        }

        private void UpdateIsSelected()
        {
            UpdateIsSelected(_pendingUnselectedCells, /* isSelected = */ false);
            UpdateIsSelected(_pendingSelectedCells, /* isSelected = */ true);
        }

        /// <summary>
        ///     Updates the IsSelected property on cells due to a change in SelectedCells.
        /// </summary>
        private void UpdateIsSelected(VirtualizedCellInfoCollection cells, bool isSelected)
        {
            if (cells != null)
            {
                int numCells = cells.Count;
                if (numCells > 0)
                {
                    // Determine if it would be better to iterate through all the visible cells
                    // instead of through the update list.
                    bool useTracker = false;
                    
                    // For "small" updates it's simpler to just go through the cells, get the container,
                    // and update IsSelected. For "large" updates, it's faster to go through the visible
                    // cells, see if they're in the collection, and then update IsSelected.
                    // Determining small vs. large is going to be done using a magic number.
                    // 750 is close to the number of visible cells Excel shows by default on a 1280x1024 monitor.
                    if (numCells > 750)
                    {
                        int numTracker = 0;
                        int numColumns = _columns.Count;

                        ContainerTracking<DataGridRow> rowTracker = _rowTrackingRoot;
                        while (rowTracker != null)
                        {
                            numTracker += numColumns;
                            if (numTracker >= numCells)
                            {
                                // There are more cells visible than being updated
                                break;
                            }
                            rowTracker = rowTracker.Next;
                        }

                        useTracker = (numCells > numTracker);
                    }

                    if (useTracker)
                    {
                        ContainerTracking<DataGridRow> rowTracker = _rowTrackingRoot;
                        while (rowTracker != null)
                        {
                            DataGridRow row = rowTracker.Container;
                            DataGridCellsPresenter cellsPresenter = row.CellsPresenter;
                            if (cellsPresenter != null)
                            {
                                ContainerTracking<DataGridCell> cellTracker = cellsPresenter.CellTrackingRoot;
                                while (cellTracker != null)
                                {
                                    DataGridCell cell = cellTracker.Container;
                                    DataGridCellInfo cellInfo = new DataGridCellInfo(cell);
                                    if (cells.Contains(cellInfo))
                                    {
                                        cell.SyncIsSelected(isSelected);
                                    }

                                    cellTracker = cellTracker.Next;
                                }
                            }

                            rowTracker = rowTracker.Next;
                        }
                    }
                    else
                    {
                        foreach (DataGridCellInfo cellInfo in cells)
                        {
                            DataGridCell cell = TryFindCell(cellInfo);
                            if (cell != null)
                            {
                                cell.SyncIsSelected(isSelected);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateSelectionOfCellsInRow(object rowItem, bool isSelected)
        {
            int rowIndex = Items.IndexOf(rowItem);
            if (rowIndex >= 0)
            {
                int columnCount = _columns.Count;
                if (columnCount > 0)
                {
                    if (isSelected)
                    {
                        _selectedCells.AddRegion(rowIndex, 0, 1, columnCount);
                    }
                    else
                    {
                        _selectedCells.RemoveRegion(rowIndex, 0, 1, columnCount);
                    }
                }
            }
        }

        /// <summary>
        ///     Notification that a particular cell's IsSelected property changed.
        /// </summary>
        internal void CellIsSelectedChanged(DataGridCell cell, bool isSelected)
        {
            if (!IsUpdatingSelectedCells)
            {
                DataGridCellInfo cellInfo = new DataGridCellInfo(cell);
                if (isSelected)
                {
                    _selectedCells.AddValidatedCell(cellInfo);
                }
                else if (_selectedCells.Contains(cellInfo))
                {
                    _selectedCells.Remove(cellInfo);
                }
            }
        }

        /// <summary>
        ///     There was general input that means that selection should occur on
        ///     the given cell.
        /// </summary>
        /// <param name="cell">The target cell.</param>
        /// <param name="startDragging">Whether the input also indicated that dragging should start.</param>
        internal void HandleSelectionForCellInput(DataGridCell cell, bool startDragging, bool allowsExtendSelect, bool allowsMinimalSelect)
        {
            DataGridSelectionUnit selectionUnit = SelectionUnit;

            // If the mode is None, then no selection will occur
            if (selectionUnit == DataGridSelectionUnit.FullRow)
            {
                // In FullRow mode, items are selected
                MakeFullRowSelection(cell.RowDataItem, allowsExtendSelect, allowsMinimalSelect);
            }
            else
            {
                // In the other modes, cells can be individually selected
                MakeCellSelection(new DataGridCellInfo(cell), allowsExtendSelect, allowsMinimalSelect);
            }

            if (startDragging)
            {
                BeginDragging();
            }
        }

        /// <summary>
        ///     There was general input on a row header that indicated that
        ///     selection should occur on the given row.
        /// </summary>
        /// <param name="row">The target row.</param>
        /// <param name="startDragging">Whether the input also indicated that dragging should start.</param>
        internal void HandleSelectionForRowHeaderInput(DataGridRow row, bool startDragging)
        {
            object rowItem = row.Item;

            // When not dragging, move focus to the first cell
            if (!_isDraggingSelection && (_columns.Count > 0))
            {
                if (!IsKeyboardFocusWithin)
                {
                    // In order for CurrentCell to move focus, the
                    // DataGrid needs to be focused.
                    Focus();
                }
                CurrentCell = new DataGridCellInfo(rowItem, ColumnFromDisplayIndex(0), this);
            }

            // Select a row when the mode is not None and the unit allows selecting rows
            if (CanSelectRows)
            {
                MakeFullRowSelection(rowItem, /* allowsExtendSelect = */ true, /* allowsMinimalSelect = */ true);

                if (startDragging)
                {
                    BeginRowDragging();
                }
            }
        }

        private void BeginRowDragging()
        {
            BeginDragging();
            _isRowDragging = true;
        }

        private void BeginDragging()
        {
            if (Mouse.Capture(this, CaptureMode.SubTree))
            {
                _isDraggingSelection = true;
                _dragPoint = Mouse.GetPosition(this);
            }
        }

        private void EndDragging()
        {
            StopAutoScroll();
            if (Mouse.Captured == this)
            {
                ReleaseMouseCapture();
            }
            _isDraggingSelection = false;
            _isRowDragging = false;
        }

        /// <summary>
        ///     Processes selection for a row.
        ///     Depending on the current keyboard state, this may mean
        ///     - Selecting the row
        ///     - Deselecting the row
        ///     - Deselecting other rows
        ///     - Extending selection to the row
        /// </summary>
        private void MakeFullRowSelection(object dataItem, bool allowsExtendSelect, bool allowsMinimalSelect)
        {
            bool extendSelection = allowsExtendSelect && ShouldExtendSelection;
            
            // minimalModify means that previous selections should not be cleared
            // or that the particular item should be toggled.
            bool minimalModify = allowsMinimalSelect && ShouldMinimallyModifySelection;

            using (UpdateSelectedCells())
            {
                bool alreadyUpdating = IsUpdatingSelectedItems;
                if (!alreadyUpdating)
                {
                    BeginUpdateSelectedItems();
                }
                try
                {
                    if (extendSelection)
                    {
                        // Extend selection from the anchor to the item

                        int numColumns = _columns.Count;
                        if (numColumns > 0)
                        {
                            ItemCollection items = Items;
                            int startIndex = items.IndexOf(_selectionAnchor.Value.Item);
                            int endIndex = items.IndexOf(dataItem);
                            if (startIndex > endIndex)
                            {
                                // Ensure that startIndex is before endIndex
                                int temp = startIndex;
                                startIndex = endIndex;
                                endIndex = temp;
                            }

                            if ((startIndex >= 0) && (endIndex >= 0))
                            {
                                IList selectedItems = SelectedItems;
                                int numItemsSelected = selectedItems.Count;

                                if (!minimalModify)
                                {
                                    bool clearedCells = false;

                                    // Unselect items not within the selection range
                                    for (int index = 0; index < numItemsSelected; index++)
                                    {
                                        object item = selectedItems[index];
                                        int itemIndex = items.IndexOf(item);

                                        if ((itemIndex < startIndex) || (endIndex < itemIndex))
                                        {
                                            // Selector has been signaled to delay updating the
                                            // collection until we have finished the entire update.
                                            // The item will actually remain in the collection
                                            // until EndUpdateSelectedItems.
                                            selectedItems.RemoveAt(index);

                                            if (!clearedCells)
                                            {
                                                // We only want to clear if something is actually being removed.
                                                _selectedCells.Clear();
                                                clearedCells = true;
                                            }
                                        }
                                    }
                                }
                                else 
                                {
                                    // If we hold Control key - unselect only the previous drag selection (between CurrentCell and endIndex)
                                    int currentCellIndex = items.IndexOf(CurrentCell.Item);
                                    int removeRangeStartIndex = -1;
                                    int removeRangeEndIndex = -1;
                                    if (currentCellIndex < startIndex)
                                    {
                                        removeRangeStartIndex = currentCellIndex;
                                        removeRangeEndIndex = startIndex - 1;
                                    }
                                    else if (currentCellIndex > endIndex)
                                    {
                                        removeRangeStartIndex = endIndex + 1;
                                        removeRangeEndIndex = currentCellIndex;
                                    }

                                    if (removeRangeStartIndex >= 0 && removeRangeEndIndex >= 0)
                                    {
                                        for (int index = 0; index < numItemsSelected; index++)
                                        {
                                            object item = selectedItems[index];
                                            int itemIndex = items.IndexOf(item);

                                            if ((removeRangeStartIndex <= itemIndex) && (itemIndex <= removeRangeEndIndex))
                                            {
                                                // Selector has been signaled to delay updating the
                                                // collection until we have finished the entire update.
                                                // The item will actually remain in the collection
                                                // until EndUpdateSelectedItems.
                                                selectedItems.RemoveAt(index);
                                            }
                                        }
                                        _selectedCells.RemoveRegion(removeRangeStartIndex, 0, removeRangeEndIndex - removeRangeStartIndex + 1, Columns.Count);
                                    }

                                }

                                // Select the children in the selection range
                                IEnumerator enumerator = ((IEnumerable)items).GetEnumerator();
                                for (int index = 0; index <= endIndex; index++)
                                {
                                    if (!enumerator.MoveNext())
                                    {
                                        // In case the enumerator ends unexpectedly
                                        break;
                                    }

                                    if (index >= startIndex)
                                    {
                                        selectedItems.Add(enumerator.Current);
                                    }
                                }

                                _selectedCells.AddRegion(startIndex, 0, endIndex - startIndex + 1, _columns.Count);
                            }
                        }
                    }
                    else
                    {
                        if (minimalModify && SelectedItems.Contains(dataItem))
                        {
                            // Unselect the one item
                            UnselectItem(dataItem);
                        }
                        else
                        {
                            if (!minimalModify || !CanSelectMultipleItems)
                            {
                                // Unselect the other items
                                if (_selectedCells.Count > 0)
                                {
                                    // Pre-emptively clear the SelectedCells collection, which is O(1),
                                    // instead of waiting for the selection change notification to clear
                                    // SelectedCells row by row, which is O(n).
                                    _selectedCells.Clear();
                                }
                                if (SelectedItems.Count > 0)
                                {
                                    SelectedItems.Clear();
                                }
                            }

                            // Select the item
                            SelectItem(dataItem);
                        }

                        _selectionAnchor = new DataGridCellInfo(dataItem, ColumnFromDisplayIndex(0), this);
                    }
                }
                finally
                {
                    if (!alreadyUpdating)
                    {
                        EndUpdateSelectedItems();
                    }
                }
            }
        }

        /// <summary>
        ///     Process selection on a cell.
        ///     Depending on the current keyboard state, this may mean
        ///     - Selecting the cell
        ///     - Deselecting the cell
        ///     - Deselecting other cells
        ///     - Extending selection to the cell
        /// </summary>
        private void MakeCellSelection(DataGridCellInfo cellInfo, bool allowsExtendSelect, bool allowsMinimalSelect)
        {
            bool extendSelection = allowsExtendSelect && ShouldExtendSelection;

            // minimalModify means that previous selections should not be cleared
            // or that the particular item should be toggled.
            bool minimalModify = allowsMinimalSelect && ShouldMinimallyModifySelection;

            using (UpdateSelectedCells())
            {
                if (extendSelection)
                {
                    // Extend selection from the anchor to the cell

                    ItemCollection items = Items;

                    int startIndex = items.IndexOf(_selectionAnchor.Value.Item);
                    int endIndex = items.IndexOf(cellInfo.Item);
                    DataGridColumn anchorColumn = _selectionAnchor.Value.Column;
                    int startColumnIndex = anchorColumn.DisplayIndex;
                    int endColumnIndex = cellInfo.Column.DisplayIndex;

                    if ((startIndex >= 0) && (endIndex >= 0) &&
                        (startColumnIndex >= 0) && (endColumnIndex >= 0))
                    {
                        int newRowCount = Math.Abs(endIndex - startIndex) + 1;
                        int newColumnCount = Math.Abs(endColumnIndex - startColumnIndex) + 1;

                        if (!minimalModify)
                        {
                            // When extending cell selection, clear out any selected items
                            if (SelectedItems.Count > 0)
                            {
                                UnselectAll();
                            }

                            _selectedCells.Clear();
                        }
                        else
                        {
                            // Remove the previously selected region
                            int currentCellIndex = items.IndexOf(CurrentCell.Item);
                            int currentCellColumnIndex = CurrentCell.Column.DisplayIndex;

                            int previousStartIndex = Math.Min(startIndex, currentCellIndex);
                            int previousRowCount = Math.Abs(currentCellIndex - startIndex) + 1;
                            int previousStartColumnIndex = Math.Min(startColumnIndex, currentCellColumnIndex);
                            int previousColumnCount = Math.Abs(currentCellColumnIndex - startColumnIndex) + 1;

                            _selectedCells.RemoveRegion(previousStartIndex, previousStartColumnIndex, previousRowCount, previousColumnCount);

                            if (SelectionUnit == DataGridSelectionUnit.CellOrRowHeader)
                            {
                                int removeRowStartIndex = previousStartIndex;
                                int removeRowEndIndex = previousStartIndex + previousRowCount - 1;

                                if (previousColumnCount <= newColumnCount)
                                {
                                    // When no columns were removed, we can check fewer rows
                                    if (previousRowCount > newRowCount)
                                    {
                                        // One or more rows were removed, so only check those rows
                                        int removeCount = previousRowCount - newRowCount;
                                        removeRowStartIndex = (previousStartIndex == currentCellIndex) ? currentCellIndex : currentCellIndex - removeCount + 1;
                                        removeRowEndIndex = removeRowStartIndex + removeCount - 1;
                                    }
                                    else
                                    {
                                        // No rows were removed, so don't check anything
                                        removeRowEndIndex = removeRowStartIndex - 1;
                                    }
                                }

                                // For cells that were removed, check if their row is selected
                                for (int i = removeRowStartIndex; i <= removeRowEndIndex; i++)
                                {
                                    object item = Items[i];
                                    if (SelectedItems.Contains(item))
                                    {
                                        // When a cell in a row is unselected, unselect the row too
                                        SelectedItems.Remove(item);
                                    }
                                }
                            }
                        }

                        // Select the cells in rows within the selection range
                        _selectedCells.AddRegion(Math.Min(startIndex, endIndex), Math.Min(startColumnIndex, endColumnIndex), newRowCount, newColumnCount);
                    }
                }
                else
                {
                    if (minimalModify && _selectedCells.Contains(cellInfo))
                    {
                        // Unselect the one cell
                        _selectedCells.Remove(cellInfo);

                        if ((SelectionUnit == DataGridSelectionUnit.CellOrRowHeader) &&
                            SelectedItems.Contains(cellInfo.Item))
                        {
                            // When a cell in a row is unselected, unselect the row too
                            SelectedItems.Remove(cellInfo.Item);
                        }
                    }
                    else
                    {
                        if (!minimalModify || !CanSelectMultipleItems)
                        {
                            // Unselect any items
                            if (SelectedItems.Count > 0)
                            {
                                UnselectAll();
                            }

                            // Unselect all the other cells
                            _selectedCells.Clear();
                        }

                        // Select the cell
                        _selectedCells.AddValidatedCell(cellInfo);
                    }

                    _selectionAnchor = cellInfo;
                }
            }
        }

        private void SelectItem(object item)
        {
            using (UpdateSelectedCells())
            {
                int itemIndex = Items.IndexOf(item);
                int numColumns = _columns.Count;
                if ((itemIndex >= 0) && (numColumns > 0))
                {
                    _selectedCells.AddRegion(itemIndex, 0, 1, numColumns);
                }
                SelectedItems.Add(item);
            }
        }

        private void UnselectItem(object item)
        {
            using (UpdateSelectedCells())
            {
                int itemIndex = Items.IndexOf(item);
                int numColumns = _columns.Count;
                if ((itemIndex >= 0) && (numColumns > 0))
                {
                    _selectedCells.RemoveRegion(itemIndex, 0, 1, numColumns);
                }
                SelectedItems.Remove(item);
            }
        }

        /// <summary>
        ///     When changing SelectedCells, do:
        ///     using (UpdateSelectedCells())
        ///     {
        ///         ...
        ///     }
        /// </summary>
        private IDisposable UpdateSelectedCells()
        {
            return new ChangingSelectedCellsHelper(this);
        }

        private void BeginUpdateSelectedCells()
        {
            Debug.Assert(!IsUpdatingSelectedCells);
            _updatingSelectedCells = true;
        }

        private void EndUpdateSelectedCells()
        {
            Debug.Assert(IsUpdatingSelectedCells);

            UpdateIsSelected();
            _updatingSelectedCells = false;

            NotifySelectedCellsChanged();
        }

        private bool IsUpdatingSelectedCells
        {
            get { return _updatingSelectedCells; }
        }

        /// <summary>
        ///     Handles tracking defered selection change notifications for selected cells.
        /// </summary>
        private class ChangingSelectedCellsHelper : IDisposable
        {
            internal ChangingSelectedCellsHelper(DataGrid dataGrid)
            {
                _dataGrid = dataGrid;
                _wasUpdatingSelectedCells = _dataGrid.IsUpdatingSelectedCells;
                if (!_wasUpdatingSelectedCells)
                {
                    _dataGrid.BeginUpdateSelectedCells();
                }
            }

            public void Dispose()
            {
                if (!_wasUpdatingSelectedCells)
                {
                    _dataGrid.EndUpdateSelectedCells();
                }
            }

            private DataGrid _dataGrid;
            private bool _wasUpdatingSelectedCells;
        }

        /// <summary>
        ///     SHIFT is down or performing a drag selection.
        ///     Multiple items can be selected.
        ///     There is a selection anchor.
        /// </summary>
        private bool ShouldExtendSelection
        {
            get
            {
                return CanSelectMultipleItems && (_selectionAnchor != null) &&
                    (_isDraggingSelection || ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift));
            }
        }

        /// <summary>
        ///     CTRL is down.
        ///     Previous selection should not be cleared, or a selected item should be toggled.
        /// </summary>
        private bool ShouldMinimallyModifySelection
        {
            get
            {
                return ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);
            }
        }

        private bool CanSelectRows
        {
            get
            {
                switch (SelectionUnit)
                {
                    case DataGridSelectionUnit.FullRow:
                    case DataGridSelectionUnit.CellOrRowHeader:
                        return true;

                    case DataGridSelectionUnit.Cell:
                        return false;
                }

                Debug.Fail("Unknown SelectionUnit encountered.");
                return false;
            }
        }

        private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _currentCellContainer = null;

            using (UpdateSelectedCells())
            {
                // Send the change notification to the selected cells collection
                _selectedCells.OnItemsCollectionChanged(e, SelectedItems);
            }
        }

        #endregion

        #region Input

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            d.CoerceValue(CanUserAddRowsProperty);
            d.CoerceValue(CanUserDeleteRowsProperty);

            // Many commands use IsEnabled to determine if they are enabled or not
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        ///     Called when a keyboard key is pressed.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Tab:
                    OnTabKeyDown(e);
                    break;

                case Key.Enter:
                    OnEnterKeyDown(e);
                    break;

                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                    OnArrowKeyDown(e);
                    break;

                case Key.Home:
                case Key.End:
                    OnHomeOrEndKeyDown(e);
                    break;

                case Key.PageUp:
                case Key.PageDown:
                    OnPageUpOrDownKeyDown(e);
                    break;
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        private static FocusNavigationDirection KeyToTraversalDirection(Key key)
        {
            switch (key)
            {
                case Key.Left:
                    return FocusNavigationDirection.Left;

                case Key.Right:
                    return FocusNavigationDirection.Right;

                case Key.Up:
                    return FocusNavigationDirection.Up;

                case Key.Down:
                default:
                    return FocusNavigationDirection.Down;
            }
        }

        private void OnArrowKeyDown(KeyEventArgs e)
        {
            DataGridCell currentCellContainer = CurrentCellContainer;
            if (currentCellContainer != null)
            {
                e.Handled = true;
                bool wasEditing = currentCellContainer.IsEditing;


                UIElement startElement = Keyboard.FocusedElement as UIElement;
                ContentElement startContentElement = (startElement == null) ? Keyboard.FocusedElement as ContentElement : null;
                if ((startElement != null) || (startContentElement != null))
                {
                    bool navigateFromCellContainer = e.OriginalSource == currentCellContainer;
                    if (navigateFromCellContainer)
                    {
                        KeyboardNavigationMode keyboardNavigationMode = KeyboardNavigation.GetDirectionalNavigation(this);
                        if (keyboardNavigationMode == KeyboardNavigationMode.Once)
                        {
                            // KeyboardNavigation will move the focus out of the DataGrid
                            DependencyObject nextFocusTarget = this.PredictFocus(KeyToTraversalDirection(e.Key));
                            if (nextFocusTarget != null && !this.IsAncestorOf(nextFocusTarget))
                            {
                                Keyboard.Focus(nextFocusTarget as IInputElement);
                            }
                            return;
                        }

                        int currentDisplayIndex = this.CurrentColumn.DisplayIndex;
                        int currentRowIndex = Items.IndexOf(CurrentItem);
                        int nextDisplayIndex = currentDisplayIndex;
                        int nextRowIndex = currentRowIndex;
                        bool controlModifier = ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);

                        // Reverse the navigation in RTL flow direction
                        Key rtlKey = e.Key;
                        if (this.FlowDirection == FlowDirection.RightToLeft)
                        {
                            if (rtlKey == Key.Left)
                            {
                                rtlKey = Key.Right;
                            }
                            else if (rtlKey == Key.Right)
                            {
                                rtlKey = Key.Left;
                            }
                        }

                        switch (rtlKey)
                        {
                            case Key.Left:
                                if (controlModifier)
                                {
                                    nextDisplayIndex = 0;
                                }
                                else
                                {
                                    nextDisplayIndex--;
                                    if (nextDisplayIndex < 0)
                                    {
                                        if (keyboardNavigationMode == KeyboardNavigationMode.Cycle)
                                        {
                                            nextDisplayIndex = Columns.Count - 1;
                                        }
                                        else if (keyboardNavigationMode == KeyboardNavigationMode.Contained)
                                        {
                                            return;
                                        }
                                        else // Continue, Local, None - move focus out of the datagrid
                                        {
                                            MoveFocus(new TraversalRequest(e.Key == Key.Left ? FocusNavigationDirection.Left : FocusNavigationDirection.Right));
                                            return;
                                        }
                                    }
                                }
                                break;

                            case Key.Right:
                                if (controlModifier)
                                {
                                    nextDisplayIndex = Math.Max(0, Columns.Count - 1);
                                }
                                else
                                {
                                    nextDisplayIndex++;
                                    if (nextDisplayIndex >= Columns.Count)
                                    {
                                        if (keyboardNavigationMode == KeyboardNavigationMode.Cycle)
                                        {
                                            nextDisplayIndex = 0;
                                        }
                                        else if (keyboardNavigationMode == KeyboardNavigationMode.Contained)
                                        {
                                            return;
                                        }
                                        else // Continue, Local, None - move focus out of the datagrid
                                        {
                                            MoveFocus(new TraversalRequest(e.Key == Key.Left ? FocusNavigationDirection.Left : FocusNavigationDirection.Right));
                                            return;
                                        }
                                    }
                                }
                                break;

                            case Key.Up:
                                if (controlModifier)
                                {
                                    nextRowIndex = 0;
                                }
                                else
                                {
                                    nextRowIndex--;
                                    if (nextRowIndex < 0)
                                    {
                                        if (keyboardNavigationMode == KeyboardNavigationMode.Cycle)
                                        {
                                            nextRowIndex = Items.Count - 1;
                                        }
                                        else if (keyboardNavigationMode == KeyboardNavigationMode.Contained)
                                        {
                                            return;
                                        }
                                        else // Continue, Local, None - move focus out of the datagrid
                                        {
                                            MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
                                            return;
                                        }
                                    }
                                }
                                break;

                            case Key.Down:
                            default:
                                if (controlModifier)
                                {
                                    nextRowIndex = Math.Max(0, Items.Count - 1);
                                }
                                else
                                {
                                    nextRowIndex++;
                                    if (nextRowIndex >= Items.Count)
                                    {
                                        if (keyboardNavigationMode == KeyboardNavigationMode.Cycle)
                                        {
                                            nextRowIndex = 0;
                                        }
                                        else if (keyboardNavigationMode == KeyboardNavigationMode.Contained)
                                        {
                                            return;
                                        }
                                        else // Continue, Local, None - move focus out of the datagrid
                                        {
                                            MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
                                            return;
                                        }
                                    }
                                }
                                break;
                        }

                        DataGridColumn nextColumn = ColumnFromDisplayIndex(nextDisplayIndex);
                        object nextItem = Items[nextRowIndex];
                        ScrollCellIntoView(nextItem, nextColumn);
                        DataGridCell nextCellContainer = TryFindCell(nextItem, nextColumn);

                        if (nextCellContainer == null || nextCellContainer == currentCellContainer || !nextCellContainer.Focus())
                            return;
                    }
 
                    // Attempt to move focus
                    TraversalRequest request = new TraversalRequest(KeyToTraversalDirection(e.Key));
                    if (navigateFromCellContainer ||
                        ((startElement != null) && startElement.MoveFocus(request)) ||
                        ((startContentElement != null) && startContentElement.MoveFocus(request)))
                    {
                        SelectAndEditOnFocusMove(e, currentCellContainer, wasEditing, /* allowsExtendSelect = */ true, /* ignoreControlKey = */ true);
                    }
                }
            }
        }

        /// <summary>
        ///     Called when the tab key is pressed to perform focus navigation.
        /// </summary>
        private void OnTabKeyDown(KeyEventArgs e)
        {
            // When the end-user uses the keyboard to tab to another cell while the current cell
            // is in edit-mode, then the next cell should enter edit mode in addition to gaining
            // focus. There is no way to detect this from the focus change events, so the cell
            // is going to handle the complete operation manually.
            // The standard focus change method is being called here, so even if focus moves
            // to something other than a cell, focus should land on the element that it would
            // have landed on anyway.

            DataGridCell currentCellContainer = CurrentCellContainer;
            if (currentCellContainer != null)
            {
                bool wasEditing = currentCellContainer.IsEditing;
                bool previous = ((e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);

                // Start navigation from the current focus to allow moveing focus on other focusable elements inside the cell
                UIElement startElement = Keyboard.FocusedElement as UIElement;
                ContentElement startContentElement = (startElement == null) ? Keyboard.FocusedElement as ContentElement : null;
                if ((startElement != null) || (startContentElement != null))
                {
                    e.Handled = true;

                    FocusNavigationDirection direction = previous ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;
                    TraversalRequest request = new TraversalRequest(direction);
                    request.Wrapped = true; // Navigate only within datagrid

                    // Move focus to the the next or previous tab stop.
                    if (((startElement != null) && startElement.MoveFocus(request)) ||
                        ((startContentElement != null) && startContentElement.MoveFocus(request)))
                    {
                        // If focus moved to the cell while in edit mode - keep navigating to the previous cell
                        if (wasEditing && previous && Keyboard.FocusedElement == currentCellContainer)
                        {
                            currentCellContainer.MoveFocus(request);
                        }

                        // When doing TAB and SHIFT+TAB focus movement, don't confuse the selection
                        // code, which also relies on SHIFT to know whether to extend selection or not.
                        SelectAndEditOnFocusMove(e, currentCellContainer, wasEditing, /* allowsExtendSelect = */ false, /* ignoreControlKey = */ true);
                    }
                }
            }
        }

        private void OnEnterKeyDown(KeyEventArgs e)
        {
            DataGridCell currentCellContainer = CurrentCellContainer;
            if ((currentCellContainer != null) && (_columns.Count > 0))
            {
                e.Handled = true;

                DataGridColumn column = currentCellContainer.Column;

                // Commit a current edit
                CommitAnyEdit();

                if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == 0)
                {
                    bool shiftModifier = ((e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);

                    // Go to the next row, keeping the column the same
                    int numItems = Items.Count;
                    int index = Math.Max(0, Math.Min(numItems - 1, Items.IndexOf(currentCellContainer.RowDataItem) + (shiftModifier ? -1 : 1)));

                    if (index < numItems)
                    {
                        object rowItem = Items[index];
                        ScrollIntoView(rowItem, column);

                        if (CurrentCell.Item != rowItem)
                        {
                            // Focus the new cell
                            CurrentCell = new DataGridCellInfo(rowItem, column, this);

                            // Will never edit on ENTER, so just say that the old cell wasn't in edit mode
                            SelectAndEditOnFocusMove(e, currentCellContainer, /* wasEditing = */ false, /* allowsExtendSelect = */ false, /* ignoreControlKey = */ true);
                        }
                        else
                        {
                            // When the new item jumped to the bottom, CurrentCell doesn't actually change,
                            // but there is a new container.
                            currentCellContainer = CurrentCellContainer;
                            if (currentCellContainer != null)
                            {
                                currentCellContainer.Focus();
                            }
                        }
                    }
                }
            }
        }

        private void SelectAndEditOnFocusMove(KeyEventArgs e, DataGridCell oldCell, bool wasEditing, bool allowsExtendSelect, bool ignoreControlKey)
        {
            DataGridCell newCell = Keyboard.FocusedElement as DataGridCell;

            // If focus has moved within DataGridCell use CurrentCellContainer
            if (newCell == null && CurrentCellContainer!=null && CurrentCellContainer.IsKeyboardFocusWithin)
            {
                newCell = CurrentCellContainer;
            }

            if ((newCell != null) && (newCell.DataGridOwner == this))
            {
                if (ignoreControlKey || ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == 0))
                {
                    HandleSelectionForCellInput(newCell, /* startDragging = */ false, allowsExtendSelect, /* allowsMinimalSelect = */ false);
                }

                // If focus moved to a new cell within the same row that didn't
                // decide on its own to enter edit mode, put it in edit mode.
                if (wasEditing && !newCell.IsEditing && (oldCell.RowDataItem == newCell.RowDataItem))
                {
                    BeginEdit(e);
                }
            }
        }

        private void OnHomeOrEndKeyDown(KeyEventArgs e)
        {
            if ((_columns.Count > 0) && (Items.Count > 0))
            {
                e.Handled = true;

                bool homeKey = (e.Key == Key.Home);
                bool controlModifier = ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);

                // Go to the first or last cell
                object item = controlModifier ? Items[homeKey ? 0 : Items.Count - 1] : CurrentItem;
                DataGridColumn column = ColumnFromDisplayIndex(homeKey ? 0 : _columns.Count - 1);

                ScrollCellIntoView(item, column);
                DataGridCell cell = TryFindCell(item, column);
                if (cell != null)
                {
                    cell.Focus();
                    HandleSelectionForCellInput(cell, /* startDragging = */ false, /* allowsExtendSelect = */ true, /* allowsMinimalSelect = */ false);
                }
            }
        }

        private void OnPageUpOrDownKeyDown(KeyEventArgs e)
        {
            // This code relies on DataGridRowsPresenter since ScrollHost relies
            // on InternalItemsHost, which relies on DataGridRowsPresenter.
            // Additionally, it relies on ViewportHeight being in logical units
            // instead of pixels.

            ScrollViewer scrollHost = InternalScrollHost;
            if (scrollHost != null)
            {
                object currentRow = CurrentItem;
                DataGridColumn currentColumn = CurrentColumn;
                int rowIndex = Items.IndexOf(currentRow);
                if (rowIndex >= 0)
                {
                    // Predict the page up/page down item based on the viewport height, which
                    // should be in logical units.
                    // This is not going to work well when the rows have different heights, but
                    // it is the best estimate we have at the moment.

                    int jumpDistance = Math.Max(1, (int)scrollHost.ViewportHeight - 1);
                    int targetIndex = (e.Key == Key.PageUp) ? rowIndex - jumpDistance : rowIndex + jumpDistance;
                    targetIndex = Math.Max(0, Math.Min(targetIndex, Items.Count - 1));

                    // Scroll the target row into view, keeping the current column
                    object targetRow = Items[targetIndex];
                    ScrollCellIntoView(targetRow, currentColumn);
                    DataGridCell cell = TryFindCell(targetRow, currentColumn);
                    if (cell != null)
                    {
                        cell.Focus();
                        HandleSelectionForCellInput(cell, /* startDragging = */ false, /* allowsExtendSelect = */ true, /* allowsMinimalSelect = */ false);
                    }
                }
            }
        }

        /// <summary>
        ///     Continues a drag selection.
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDraggingSelection)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    // Check that the mouse has moved relative to the DataGrid.
                    // This check prevents the case where a row is partially visible
                    // at the bottom. If this row is clicked, then it will be scrolled
                    // into view and away from the mouse. The mouse will then appear
                    // (according to these messages) as if it moved over a new cell, and
                    // could invoke a drag, but the actual mouse position relative to 
                    // the DataGrid hasn't changed.
                    Point currentMousePosition = Mouse.GetPosition(this);
                    if (!DoubleUtil.AreClose(currentMousePosition, _dragPoint))
                    {
                        _dragPoint = currentMousePosition;

                        RelativeMousePositions position = RelativeMousePosition;
                        if (position == RelativeMousePositions.Over)
                        {
                            // The mouse is within the field of cells and rows, use the actual
                            // elements to determine changes to selection.
                            if (_isRowDragging)
                            {
                                DataGridRow row = MouseOverRow;
                                if ((row != null) && (row.Item != CurrentItem))
                                {
                                    // Continue a row header drag to the given row
                                    HandleSelectionForRowHeaderInput(row, /* startDragging = */ false);
                                    CurrentItem = row.Item;
                                    e.Handled = true;
                                }
                            }
                            else
                            {
                                DataGridCell cell = MouseOverCell;
                                if (cell == null)
                                {
                                    DataGridRow row = MouseOverRow;
                                    if (row != null)
                                    {
                                        // The mouse is over a row but not necessarily a cell,
                                        // such as over a header or details section. Find the
                                        // nearest cell and use that.
                                        cell = GetCellNearMouse();
                                    }
                                }
                                if ((cell != null) && (cell != CurrentCellContainer))
                                {
                                    HandleSelectionForCellInput(cell, /* startDragging = */ false, /* allowsExtendSelect = */ true, /* allowsMinimalSelect = */ true);
                                    cell.Focus();
                                    e.Handled = true;
                                }
                            }
                        }
                        else
                        {
                            // The mouse is outside of the field of cells and rows.
                            if (_isRowDragging && IsMouseToLeftOrRightOnly(position))
                            {
                                // Figure out which row the mouse is in-line with and select it
                                DataGridRow row = GetRowNearMouse();
                                if ((row != null) && (row.Item != CurrentItem))
                                {
                                    // The mouse is directly to the left or right of the row
                                    HandleSelectionForRowHeaderInput(row, /* startDragging = */ false);
                                    CurrentItem = row.Item;
                                    e.Handled = true;
                                }
                            }
                            else if (_hasAutoScrolled)
                            {
                                // The mouse is outside the grid, and we've started auto-scrolling.
                                // The user has moved the mouse and would like a quick update.
                                if (DoAutoScroll())
                                {
                                    e.Handled = true;
                                }
                            }
                            else
                            {
                                // Ensure that the auto-scroll timer has started
                                StartAutoScroll();
                            }
                        }
                    }
                }
                else
                {
                    // The mouse button is up, end the drag operation
                    EndDragging();
                }
            }
        }

        private static void OnAnyMouseUpThunk(object sender, MouseButtonEventArgs e)
        {
            ((DataGrid)sender).OnAnyMouseUp(e);
        }

        /// <summary>
        ///     Ends a drag selection.
        /// </summary>
        private void OnAnyMouseUp(MouseButtonEventArgs e)
        {
            EndDragging();
        }

        /// <summary>
        ///     When a ContextMenu opens on a cell that isn't selected, it should 
        ///     become selected.
        /// </summary>
        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            DataGridCell cell = null;
            DataGridRowHeader rowHeader = null;
            UIElement sourceElement = e.OriginalSource as UIElement;
            while (sourceElement != null)
            {
                cell = sourceElement as DataGridCell;
                if (cell != null)
                    break;

                rowHeader = sourceElement as DataGridRowHeader;
                if (rowHeader != null)
                    break;

                sourceElement = VisualTreeHelper.GetParent(sourceElement) as UIElement;
            }

            if ((cell != null) && !cell.IsSelected && !cell.IsKeyboardFocusWithin)
            {
                cell.Focus();
                HandleSelectionForCellInput(cell, /* startDragging = */ false, /* allowsExtendSelect = */ true, /* allowsMinimalSelect = */ true);
            }

            if (rowHeader != null)
            {
                DataGridRow parentRow = rowHeader.ParentRow;
                if (parentRow != null)
                {
                    HandleSelectionForRowHeaderInput(parentRow, /* startDragging = */ false);
                }
            }

        }

        /// <summary>
        ///     Finds the row that contains the mouse's Y coordinate.
        /// </summary>
        /// <remarks>
        ///     Relies on InternalItemsHost.
        ///     Meant to be used when the mouse is outside the DataGrid.
        /// </remarks>
        private DataGridRow GetRowNearMouse()
        {
            Debug.Assert(RelativeMousePosition != RelativeMousePositions.Over, "The mouse is not supposed to be over the DataGrid.");

            Panel itemsHost = InternalItemsHost;
            if (itemsHost != null)
            {
                // Iterate from the end to the beginning since it is more common
                // to drag toward the end.
                int count = itemsHost.Children.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    DataGridRow row = itemsHost.Children[i] as DataGridRow;
                    if (row != null)
                    {
                        Point pt = Mouse.GetPosition(row);
                        Rect rowBounds = new Rect(new Point(), row.RenderSize);
                        if ((pt.Y >= rowBounds.Top) && (pt.Y <= rowBounds.Bottom))
                        {
                            // The mouse cursor's Y position is within the Y bounds of the row
                            return row;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Finds the cell that is nearest to the mouse.
        /// </summary>
        /// <remarks>
        ///     Relies on InternalItemsHost.
        /// </remarks>
        private DataGridCell GetCellNearMouse()
        {
            Panel itemsHost = InternalItemsHost;
            if (itemsHost != null)
            {
                Rect itemsHostBounds = new Rect(new Point(), itemsHost.RenderSize);
                double closestDistance = Double.PositiveInfinity;
                DataGridCell closestCell = null;
                bool isMouseInCorner = IsMouseInCorner(RelativeMousePosition);

                // Iterate from the end to the beginning since it is more common
                // to drag toward the end.
                int count = itemsHost.Children.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    DataGridRow row = itemsHost.Children[i] as DataGridRow;
                    if (row != null)
                    {
                        DataGridCellsPresenter cellsPresenter = row.CellsPresenter;
                        if (cellsPresenter != null)
                        {
                            // Go through all of the instantiated cells and find the closest cell
                            ContainerTracking<DataGridCell> cellTracker = cellsPresenter.CellTrackingRoot;
                            while (cellTracker != null)
                            {
                                DataGridCell cell = cellTracker.Container;

                                double cellDistance;
                                if (CalculateCellDistance(cell, row, itemsHost, itemsHostBounds, isMouseInCorner, out cellDistance))
                                {
                                    if ((closestCell == null) || (cellDistance < closestDistance))
                                    {
                                        // This cell's distance is less, so make it the closest cell
                                        closestDistance = cellDistance;
                                        closestCell = cell;
                                    }
                                }

                                cellTracker = cellTracker.Next;
                            }

                            // Check if the header is close
                            DataGridRowHeader rowHeader = row.RowHeader;
                            if (rowHeader != null)
                            {
                                double cellDistance;
                                if (CalculateCellDistance(rowHeader, row, itemsHost, itemsHostBounds, isMouseInCorner, out cellDistance))
                                {
                                    if ((closestCell == null) || (cellDistance < closestDistance))
                                    {
                                        // If the header is the closest, then use the first cell from the row
                                        DataGridCell cell = row.TryGetCell(DisplayIndexMap[0]);
                                        if (cell != null)
                                        {
                                            closestDistance = cellDistance;
                                            closestCell = cell;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return closestCell;
            }

            return null;
        }

        /// <summary>
        ///     Determines if a cell meets the criteria for being chosen. If it does, it
        ///     calculates its a "distance" that can be compared to other cells.
        /// </summary>
        /// <param name="distance">
        ///     A value that represents the distance between the mouse and the cell.
        ///     This is not necessarily an accurate pixel number in some cases.
        /// </param>
        /// <returns>
        ///     true if the cell can be a drag target. false otherwise.
        /// </returns>
        private bool CalculateCellDistance(FrameworkElement cell, DataGridRow rowOwner, Panel itemsHost, Rect itemsHostBounds, bool isMouseInCorner, out double distance)
        {
            GeneralTransform transform = cell.TransformToAncestor(itemsHost);
            Rect cellBounds = new Rect(new Point(), cell.RenderSize);

            // Limit to only cells that are entirely visible
            if (itemsHostBounds.Contains(transform.TransformBounds(cellBounds)))
            {
                Point pt = Mouse.GetPosition(cell);
                if (isMouseInCorner)
                {
                    // When the mouse is in the corner, go by distance from center of the cell
                    Vector v = new Vector(pt.X - (cellBounds.Width * 0.5), pt.Y - (cellBounds.Height * 0.5));
                    distance = v.Length;
                    return true;
                }
                else
                {
                    Point rowPt = Mouse.GetPosition(rowOwner);
                    Rect rowBounds = new Rect(new Point(), rowOwner.RenderSize);

                    // The mouse should overlap a row or column
                    if ((pt.X >= cellBounds.Left) && (pt.X <= cellBounds.Right))
                    {
                        // The mouse is within a column
                        if ((rowPt.Y >= rowBounds.Top) && (rowPt.Y <= rowBounds.Bottom))
                        {
                            // Mouse is within the cell
                            distance = 0.0;
                        }
                        else
                        {
                            // Mouse is outside but is within a columns horizontal bounds
                            distance = Math.Abs(pt.Y - cellBounds.Top);
                        }
                        return true;
                    }
                    else if ((rowPt.Y >= rowBounds.Top) && (rowPt.Y <= rowBounds.Bottom))
                    {
                        // Mouse is outside but is within a row's vertical bounds
                        distance = Math.Abs(pt.X - cellBounds.Left);
                        return true;
                    }
                }
            }

            distance = Double.PositiveInfinity;
            return false;
        }

        /// <summary>
        ///     The row that the mouse is over.
        /// </summary>
        private static DataGridRow MouseOverRow
        {
            get
            {
                return DataGridHelper.FindVisualParent<DataGridRow>(Mouse.DirectlyOver as UIElement);
            }
        }

        // The cell that the mouse is over.
        private static DataGridCell MouseOverCell
        {
            get
            {
                return DataGridHelper.FindVisualParent<DataGridCell>(Mouse.DirectlyOver as UIElement);
            }
        }

        /// <summary>
        ///     The mouse position relative to the ItemsHost.
        /// </summary>
        /// <remarks>
        ///     Relies on InternalItemsHost.
        /// </remarks>
        private RelativeMousePositions RelativeMousePosition
        {
            get
            {
                RelativeMousePositions position = RelativeMousePositions.Over;

                Panel itemsHost = InternalItemsHost;
                if (itemsHost != null)
                {
                    Point pt = Mouse.GetPosition(itemsHost);
                    Rect bounds = new Rect(new Point(), itemsHost.RenderSize);

                    if (pt.X < bounds.Left)
                    {
                        position |= RelativeMousePositions.Left;
                    }
                    else if (pt.X > bounds.Right)
                    {
                        position |= RelativeMousePositions.Right;
                    }

                    if (pt.Y < bounds.Top)
                    {
                        position |= RelativeMousePositions.Above;
                    }
                    else if (pt.Y > bounds.Bottom)
                    {
                        position |= RelativeMousePositions.Below;
                    }
                }

                return position;
            }
        }

        private static bool IsMouseToLeft(RelativeMousePositions position)
        {
            return (position & RelativeMousePositions.Left) == RelativeMousePositions.Left;
        }

        private static bool IsMouseToRight(RelativeMousePositions position)
        {
            return (position & RelativeMousePositions.Right) == RelativeMousePositions.Right;
        }

        private static bool IsMouseAbove(RelativeMousePositions position)
        {
            return (position & RelativeMousePositions.Above) == RelativeMousePositions.Above;
        }

        private static bool IsMouseBelow(RelativeMousePositions position)
        {
            return (position & RelativeMousePositions.Below) == RelativeMousePositions.Below;
        }

        private static bool IsMouseToLeftOrRightOnly(RelativeMousePositions position)
        {
            return (position == RelativeMousePositions.Left) || (position == RelativeMousePositions.Right);
        }

        private static bool IsMouseInCorner(RelativeMousePositions position)
        {
            return (position != RelativeMousePositions.Over) &&
                (position != RelativeMousePositions.Above) &&
                (position != RelativeMousePositions.Below) &&
                (position != RelativeMousePositions.Left) &&
                (position != RelativeMousePositions.Right);
        }

        [Flags]
        private enum RelativeMousePositions
        {
            Over    = 0x00,
            Above   = 0x01,
            Below   = 0x02,
            Left    = 0x04,
            Right   = 0x08,
        }

        #endregion

        #region Cell Info

        private DataGridCell TryFindCell(DataGridCellInfo info)
        {
            // Does not de-virtualize cells

            return TryFindCell(info.Item, info.Column);
        }

        private DataGridCell TryFindCell(object item, DataGridColumn column)
        {
            // Does not de-virtualize cells

            DataGridRow row = (DataGridRow)ItemContainerGenerator.ContainerFromItem(item);
            int columnIndex = _columns.IndexOf(column);
            if ((row != null) && (columnIndex >= 0))
            {
                return row.TryGetCell(columnIndex);
            }

            return null;
        }

        #endregion

        #region Auto Sort

        /// <summary>
        /// Dependecy property for CanUserSortColumns Property
        /// </summary>
        public static readonly DependencyProperty CanUserSortColumnsProperty =
            DependencyProperty.Register("CanUserSortColumns",
                                        typeof(bool),
                                        typeof(DataGrid),
                                        new FrameworkPropertyMetadata(true));

        /// <summary>
        /// The property which determines whether the datagrid can be sorted by 
        /// cells in the columns or not
        /// </summary>
        public bool CanUserSortColumns
        {
            get { return (bool)GetValue(CanUserSortColumnsProperty); }
            set { SetValue(CanUserSortColumnsProperty, value); }
        }

        public event DataGridSortingEventHandler Sorting;

        /// <summary>
        /// Protected method which raises the sorting event and does default sort
        /// </summary>
        /// <param name="eventArgs"></param>
        protected virtual void OnSorting(DataGridSortingEventArgs eventArgs)
        {
            eventArgs.Handled = false;
            if (Sorting != null)
            {
                Sorting(this, eventArgs);
            }

            if (!eventArgs.Handled)
            {
                DefaultSort(eventArgs.Column,
                    /* clearExistinSortDescriptions */ (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift);
            }
        }

        /// <summary>
        /// Method to perform sorting on datagrid
        /// </summary>
        /// <param name="sortColumn"></param>
        internal void PerformSort(DataGridColumn sortColumn)
        {
            Debug.Assert(sortColumn != null, "column should not be null");

            if (!CanUserSortColumns || !sortColumn.CanUserSort)
            {
                return;
            }

            CommitAnyEdit();

            PrepareForSort(sortColumn);

            DataGridSortingEventArgs eventArgs = new DataGridSortingEventArgs(sortColumn);
            OnSorting(eventArgs);

            if (Items.NeedsRefresh)
            {
                try
                {
                    Items.Refresh();
                }
                catch (InvalidOperationException invalidOperationException)
                {
                    Items.SortDescriptions.Clear();
                    throw new InvalidOperationException(SR.Get(SRID.DataGrid_ProbableInvalidSortDescription), invalidOperationException);
                }
            }
        }

        /// <summary>
        /// Clears the sort directions for all the columns except the column to be sorted upon
        /// </summary>
        /// <param name="sortColumn"></param>
        private void PrepareForSort(DataGridColumn sortColumn)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                return;
            }
            if (Columns != null)
            {
                foreach (DataGridColumn column in Columns)
                {
                    if (column != sortColumn)
                    {
                        column.SortDirection = null;
                    }
                }
            }
        }

        /// <summary>
        /// Determines the sort direction and sort property name and adds a sort
        /// description to the Items>SortDescriptions Collection. Clears all the 
        /// existing sort descriptions.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="clearExistingSortDescriptions"></param>
        private void DefaultSort(DataGridColumn column, bool clearExistingSortDescriptions)
        {
            ListSortDirection sortDirection = ListSortDirection.Ascending;
            Nullable<ListSortDirection> currentSortDirection = column.SortDirection;
            if (currentSortDirection.HasValue &&
                currentSortDirection.Value == ListSortDirection.Ascending)
            {
                sortDirection = ListSortDirection.Descending;
            }

            string sortPropertyName = column.SortMemberPath;
            if (string.IsNullOrEmpty(sortPropertyName))
            {
                DataGridBoundColumn boundColumn = column as DataGridBoundColumn;
                if (boundColumn != null)
                {
                    Binding binding = boundColumn.DataFieldBinding as Binding;
                    if (binding != null)
                    {
                        if (!string.IsNullOrEmpty(binding.XPath))
                        {
                            sortPropertyName = binding.XPath;
                        }
                        else if (binding.Path != null)
                        {
                            sortPropertyName = binding.Path.Path;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(sortPropertyName))
            {
                int descriptorIndex = -1;
                if (clearExistingSortDescriptions)
                {
                    //clear the sortdesriptions collection
                    Items.SortDescriptions.Clear();
                }
                else
                {
                    //get the index of existing descriptor to replace it
                    for (int i = 0; i < Items.SortDescriptions.Count; i++)
                    {
                        if (string.Compare(Items.SortDescriptions[i].PropertyName, sortPropertyName) == 0 &&
                            (GroupingSortDescriptionIndices == null ||
                            !GroupingSortDescriptionIndices.Contains(i)))
                        {
                            descriptorIndex = i;
                            break;
                        }
                    }
                }
                SortDescription sortDescription = new SortDescription(sortPropertyName, sortDirection);
                try
                {
                    if (descriptorIndex >= 0)
                    {
                        Items.SortDescriptions[descriptorIndex] = sortDescription;
                    }
                    else
                    {
                        Items.SortDescriptions.Add(sortDescription);
                    }

                    if (clearExistingSortDescriptions || !_sortingStarted)
                    {
                        RegenerateGroupingSortDescriptions();
                        _sortingStarted = true;
                    }
                }
                catch (InvalidOperationException invalidOperationException)
                {
                    Items.SortDescriptions.Clear();
                    throw new InvalidOperationException(SR.Get(SRID.DataGrid_InvalidSortDescription), invalidOperationException);
                }
                column.SortDirection = sortDirection;
            }
        }

        /// <summary>
        /// List which holds all the indices of SortDescriptions which were
        /// added for the sake of GroupDescriptions
        /// </summary>
        private List<int> GroupingSortDescriptionIndices
        {
            get
            {
                return _groupingSortDescriptionIndices;
            }
            set
            {
                _groupingSortDescriptionIndices = value;
            }
        }

        /// <summary>
        /// SortDescription collection changed listener. Ensures that GroupingSortDescriptionIndices
        /// is in sync with SortDescriptions.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnItemsSortDescriptionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_ignoreSortDescriptionsChange || GroupingSortDescriptionIndices == null)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Debug.Assert(e.NewItems.Count == 1, "SortDescriptionCollection should handle one element at a time");
                    for (int i = 0, count = GroupingSortDescriptionIndices.Count; i < count; i++)
                    {
                        if (GroupingSortDescriptionIndices[i] >= e.NewStartingIndex)
                        {
                            GroupingSortDescriptionIndices[i]++;
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    Debug.Assert(e.OldItems.Count == 1, "SortDescriptionCollection should handle one element at a time");
                    for (int i = 0, count = GroupingSortDescriptionIndices.Count; i < count; i++)
                    {
                        if (GroupingSortDescriptionIndices[i] > e.OldStartingIndex)
                        {
                            GroupingSortDescriptionIndices[i]--;
                        }
                        else if (GroupingSortDescriptionIndices[i] == e.OldStartingIndex)
                        {
                            GroupingSortDescriptionIndices.RemoveAt(i);
                            i--;
                            count--;
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    //SortDescriptionCollection doesnt support move, atleast as an atomic operation. Hence Do nothing.
                    break;
                case NotifyCollectionChangedAction.Replace:
                    Debug.Assert(e.OldItems.Count == 1 && e.NewItems.Count == 1, "SortDescriptionCollection should handle one element at a time");
                    GroupingSortDescriptionIndices.Remove(e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    GroupingSortDescriptionIndices.Clear();
                    break;
            }
        }

        /// <summary>
        /// Method to remove all the SortDescriptions which were added based on GroupDescriptions
        /// </summary>
        private void RemoveGroupingSortDescriptions()
        {
            if (GroupingSortDescriptionIndices == null)
            {
                return;
            }

            bool originalIgnoreSortDescriptionChanges = _ignoreSortDescriptionsChange;
            _ignoreSortDescriptionsChange = true;
            try
            {
                for (int i = 0, count = GroupingSortDescriptionIndices.Count; i < count; i++)
                {
                    Items.SortDescriptions.RemoveAt(GroupingSortDescriptionIndices[i] - i);
                }
                GroupingSortDescriptionIndices.Clear();
            }
            finally
            {
                _ignoreSortDescriptionsChange = originalIgnoreSortDescriptionChanges;
            }
        }

        /// <summary>
        /// Helper method which determines if one can create a SortDescription out of
        /// a GroupDescription.
        /// </summary>
        /// <param name="propertyGroupDescription"></param>
        /// <returns></returns>
        private bool CanConvertToSortDescription(PropertyGroupDescription propertyGroupDescription)
        {
            if (propertyGroupDescription != null &&
                propertyGroupDescription.Converter == null &&
                propertyGroupDescription.StringComparison == StringComparison.Ordinal)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Method to add SortDescriptions based on GroupDescriptions.
        /// Only PropertGroupDescriptions with no ValueConverter and with
        /// Oridinal comparison are considered suitable.
        /// </summary>
        private void AddGroupingSortDescriptions()
        {
            bool originalIgnoreSortDescriptionChanges = _ignoreSortDescriptionsChange;
            _ignoreSortDescriptionsChange = true;
            try
            {
                int insertIndex = 0;
                foreach (GroupDescription groupDescription in Items.GroupDescriptions)
                {
                    PropertyGroupDescription propertyGroupDescription = groupDescription as PropertyGroupDescription;
                    if (CanConvertToSortDescription(propertyGroupDescription))
                    {
                        SortDescription sortDescription = new SortDescription(propertyGroupDescription.PropertyName, ListSortDirection.Ascending);
                        Items.SortDescriptions.Insert(insertIndex, sortDescription);
                        if (GroupingSortDescriptionIndices == null)
                        {
                            GroupingSortDescriptionIndices = new List<int>();
                        }
                        GroupingSortDescriptionIndices.Add(insertIndex++);
                    }
                }
            }
            finally
            {
                _ignoreSortDescriptionsChange = originalIgnoreSortDescriptionChanges;
            }
        }

        /// <summary>
        /// Method to regenrated the SortDescriptions based on the GroupDescriptions
        /// </summary>
        private void RegenerateGroupingSortDescriptions()
        {
            RemoveGroupingSortDescriptions();
            AddGroupingSortDescriptions();
        }

        /// <summary>
        /// CollectionChanged listener for GroupDescriptions of DataGrid.
        /// Regenerates Grouping based sort descriptions is required.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnItemsGroupDescriptionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_sortingStarted)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Debug.Assert(e.NewItems.Count == 1, "GroupDescriptionCollection should handle one element at a time");
                    if (CanConvertToSortDescription(e.NewItems[0] as PropertyGroupDescription))
                    {
                        RegenerateGroupingSortDescriptions();
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    Debug.Assert(e.OldItems.Count == 1, "GroupDescriptionCollection should handle one element at a time");
                    if (CanConvertToSortDescription(e.OldItems[0] as PropertyGroupDescription))
                    {
                        RegenerateGroupingSortDescriptions();
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    //Do Nothing
                    break;
                case NotifyCollectionChangedAction.Replace:
                    Debug.Assert(e.OldItems.Count == 1 && e.NewItems.Count == 1, "GroupDescriptionCollection should handle one element at a time");
                    if ((CanConvertToSortDescription(e.OldItems[0] as PropertyGroupDescription)) || 
                        (CanConvertToSortDescription(e.NewItems[0] as PropertyGroupDescription)))
                    {
                        RegenerateGroupingSortDescriptions();
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    RemoveGroupingSortDescriptions();
                    break;
            }
        }

        #endregion

        #region Column Auto Generation

        /// <summary>
        /// This event will be raised whenever auto generation of columns gets completed
        /// </summary>
        public event EventHandler AutoGeneratedColumns;

        /// <summary>
        /// This event will be raised for each column getting auto generated
        /// </summary>
        public event EventHandler<DataGridAutoGeneratingColumnEventArgs> AutoGeneratingColumn;

        /// <summary>
        ///     The DependencyProperty that represents the AutoGenerateColumns property.
        /// </summary>
        public static readonly DependencyProperty AutoGenerateColumnsProperty =
            DependencyProperty.Register("AutoGenerateColumns", typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnAutoGenerateColumnsPropertyChanged)));

        /// <summary>
        /// The property which determines whether the columns are to be auto generated or not.
        /// Setting of the property actually generates or deletes columns.
        /// </summary>
        public bool AutoGenerateColumns
        {
            get { return (bool)GetValue(AutoGenerateColumnsProperty); }
            set { SetValue(AutoGenerateColumnsProperty, value); }
        }

        /// <summary>
        /// The polumorphic method which raises the AutoGeneratedColumns event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnAutoGeneratedColumns(EventArgs e)
        {
            if (AutoGeneratedColumns != null)
            {
                AutoGeneratedColumns(this, e);
            }
        }

        /// <summary>
        /// The polymorphic method which raises the AutoGeneratingColumn event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            if (AutoGeneratingColumn != null)
            {
                AutoGeneratingColumn(this, e);
            }
        }

        /// <summary>
        ///     Determines the desired size of the control given a constraint.
        /// </summary>
        /// <remarks>
        ///     On the first measure:
        ///     - Performs auto-generation of columns if needed.
        ///     - Coerces CanUserAddRows and CanUserDeleteRows.
        ///     - Updates the NewItemPlaceholder.
        /// </remarks>
        /// <param name="availableSize">The available space.</param>
        /// <returns>The desired size of the control.</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (_measureNeverInvoked)
            {
                _measureNeverInvoked = false;
                if (AutoGenerateColumns)
                {
                    AddAutoColumns();
                }

                InternalColumns.InitializeDisplayIndexMap();

                // FrozenColumns rely on column DisplayIndex
                CoerceValue(FrozenColumnCountProperty);

                // These properties rely on a variety of properties. This is necessary since
                // our default (true) is actually incorrect initially (when ItemsSource is null).
                // So, we delay to this point, in case ItemsSource is never set, to coerce them
                // to their correct values. If ItemsSource did change, then they will have their
                // correct values already and this is extra work.
                CoerceValue(CanUserAddRowsProperty);
                CoerceValue(CanUserDeleteRowsProperty);

                // We need to call this in case CanUserAddRows has remained true (the default value)
                // since startup and no one has set the placeholder position.
                UpdateNewItemPlaceholder(/* isAddingNewItem = */ false);
            }

            return base.MeasureOverride(availableSize);
        }

        /// <summary>
        /// The polymorphic method which gets called whenever the ItemsSource gets changed.
        /// We regenerate columns if required when ItemsSource gets changed.
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);

            INotifyCollectionChanged oldNotifyCollection = oldValue as INotifyCollectionChanged;
            if (oldNotifyCollection != null && DeferAutoGeneration)
            {
                oldNotifyCollection.CollectionChanged -= new NotifyCollectionChangedEventHandler(OnItemsSourceCollectionChanged);
            }

            using (UpdateSelectedCells())
            {
                // Selector will try to maintain the previous row selection.
                // Keep SelectedCells in sync.
                _selectedCells.RestoreOnlyFullRows(SelectedItems);
            }

            if (AutoGenerateColumns == true)
            {
                RegenerateAutoColumns();
            }

            CoerceValue(CanUserAddRowsProperty);
            CoerceValue(CanUserDeleteRowsProperty);

            ResetRowHeaderActualWidth();

            UpdateNewItemPlaceholder(/* isAddingNewItem = */ false);
        }

        /// <summary>
        /// Private property to hook and unhook collection changed event on ItemsSources CollectionChanged
        /// event when ever _deferAutoGeneration flag changes.
        /// </summary>
        private bool DeferAutoGeneration
        {
            get
            {
                return _deferAutoGeneration;
            }
            set
            {
                bool oldValue = _deferAutoGeneration;
                _deferAutoGeneration = value;
                if (oldValue != value)
                {
                    INotifyCollectionChanged notifyCollection = ItemsSource as INotifyCollectionChanged;
                    if (notifyCollection != null)
                    {
                        if (value)
                        {
                            notifyCollection.CollectionChanged += new NotifyCollectionChangedEventHandler(OnItemsSourceCollectionChanged);
                        }
                        else
                        {
                            notifyCollection.CollectionChanged -= new NotifyCollectionChangedEventHandler(OnItemsSourceCollectionChanged);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The event listener to ItemsSource collection changed event which performs deffered auto generation
        /// and also unhooks it self as needed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                AddAutoColumns();
                DeferAutoGeneration = false;
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ResetRowHeaderActualWidth();
            }
        }

        /// <summary>
        /// Method which generated auto columns and adds to the data grid.
        /// </summary>
        private void AddAutoColumns()
        {
            if (ItemsSource != null &&
                ItemsSource is INotifyCollectionChanged &&
                DataItemsCount == 0)
            {
                //do deferred generation
                DeferAutoGeneration = true;
            }
            else if (!_measureNeverInvoked)
            {
                DataGrid.GenerateColumns((IItemProperties)(Items),
                            this,
                            null);
  
                OnAutoGeneratedColumns(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Method which deletes all the auto generated columns.
        /// </summary>
        private void DeleteAutoColumns()
        {
            if (!DeferAutoGeneration && !_measureNeverInvoked)
            {
                for (int columnIndex = Columns.Count - 1; columnIndex >= 0; --columnIndex)
                {
                    if (Columns[columnIndex].IsAutoGenerated)
                    {
                        Columns.RemoveAt(columnIndex);
                    }
                }
            }
            else
            {
                DeferAutoGeneration = false;
            }
        }

        /// <summary>
        /// Method which regenerates the columns for the datagrid
        /// </summary>
        private void RegenerateAutoColumns()
        {
            DeleteAutoColumns();
            AddAutoColumns();
        }

        /// <summary>
        /// Helper method which generates columns for a given IItemProperties
        /// </summary>
        /// <param name="iItemProperties"></param>
        /// <returns></returns>
        public static Collection<DataGridColumn> GenerateColumns(IItemProperties iItemProperties)
        {
            if (iItemProperties == null)
            {
                throw new ArgumentNullException("iItemProperties");
            }

            Collection<DataGridColumn> columnCollection = new Collection<DataGridColumn>();
            DataGrid.GenerateColumns(iItemProperties,
                null,
                columnCollection);
            return columnCollection;
        }

        /// <summary>
        /// Helper method which generates columns for a given IItemProperties and adds
        /// them either to a datagrid or to a collection of columns as specified by the flag.
        /// </summary>
        /// <param name="dataGrid"></param>
        /// <param name="iItemProperties"></param>
        /// <param name="columnCollection"></param>
        private static void GenerateColumns(IItemProperties iItemProperties,
            DataGrid dataGrid,
            Collection<DataGridColumn> columnCollection)
        {
            Debug.Assert(iItemProperties != null, "iItemProperties should not be null");
            Debug.Assert(dataGrid != null || columnCollection != null, "Both dataGrid and columnCollection cannot not be null at the same time");
            
            ReadOnlyCollection<ItemPropertyInfo> itemProperties = iItemProperties.ItemProperties;

            if (itemProperties != null &&
                itemProperties.Count > 0)
            {
                foreach (ItemPropertyInfo itemProperty in itemProperties)
                {
                    DataGridColumn dataGridColumn = DataGridColumn.CreateDefaultColumn(itemProperty);

                    if (dataGrid != null)
                    {
                        //AutoGeneratingColumn event is raised before generating and adding column to datagrid
                        //and the column returned by the event handler is used instead of the original column.
                        DataGridAutoGeneratingColumnEventArgs eventArgs = new DataGridAutoGeneratingColumnEventArgs(dataGridColumn, itemProperty);
                        dataGrid.OnAutoGeneratingColumn(eventArgs);

                        if (!eventArgs.Cancel && eventArgs.Column != null)
                        {
                            eventArgs.Column.IsAutoGenerated = true;
                            dataGrid.Columns.Add(eventArgs.Column);
                        }
                    }
                    else
                    {
                        columnCollection.Add(dataGridColumn);
                    }
                }
            }
        }

        /// <summary>
        /// The event listener which listens to the change in the AutoGenerateColumns flag
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnAutoGenerateColumnsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            bool newValue = (bool)(e.NewValue);

            DataGrid dataGrid = (DataGrid)d;

            if (newValue)
            {
                dataGrid.AddAutoColumns();
            }
            else
            {
                dataGrid.DeleteAutoColumns();
            }
        }

        #endregion

        #region Frozen Columns

        /// <summary>
        /// Dependency Property fro FrozenColumnCount Property
        /// </summary>
        public static readonly DependencyProperty FrozenColumnCountProperty =
            DependencyProperty.Register("FrozenColumnCount", 
                                        typeof(int), 
                                        typeof(DataGrid), 
                                        new FrameworkPropertyMetadata(0, 
                                                                      new PropertyChangedCallback(OnFrozenColumnCountPropertyChanged), 
                                                                      new CoerceValueCallback(OnCoerceFrozenColumnCount)),
                                        new ValidateValueCallback(ValidateFrozenColumnCount));

        /// <summary>
        /// Property which determines the number of columns which are frozen from the beginning in order of display
        /// </summary>
        public int FrozenColumnCount
        {
            get { return (int)GetValue(FrozenColumnCountProperty); }
            set { SetValue(FrozenColumnCountProperty, value); }
        }

        /// <summary>
        /// Coercion call back for FrozenColumnCount property, which ensures that it is never more that column count
        /// </summary>
        /// <param name="d"></param>
        /// <param name="baseValue"></param>
        /// <returns></returns>
        private static object OnCoerceFrozenColumnCount(DependencyObject d, object baseValue)
        {
            DataGrid dataGrid = (DataGrid)d;
            int frozenColumnCount = (int)baseValue;

            if (frozenColumnCount > dataGrid.Columns.Count)
            {
                return dataGrid.Columns.Count;
            }

            return baseValue;
        }

        /// <summary>
        /// Property changed callback fro FrozenColumnCount
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnFrozenColumnCountPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.ColumnCollection | NotificationTarget.ColumnHeaders | NotificationTarget.CellsPresenter);
        }

        /// <summary>
        /// Validation call back for frozen column count
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool ValidateFrozenColumnCount(object value)
        {
            int frozenCount = (int)value;
            return (frozenCount >= 0);
        }

        /// <summary>
        /// Dependency Property key for NonFrozenColumnsViewportHorizontalOffset Property
        /// </summary>
        private static readonly DependencyPropertyKey NonFrozenColumnsViewportHorizontalOffsetPropertyKey =
                DependencyProperty.RegisterReadOnly(
                        "NonFrozenColumnsViewportHorizontalOffset",
                        typeof(double),
                        typeof(DataGrid),
                        new FrameworkPropertyMetadata(0.0));

        /// <summary>
        /// Dependency property for NonFrozenColumnsViewportHorizontalOffset Property
        /// </summary>
        public static readonly DependencyProperty NonFrozenColumnsViewportHorizontalOffsetProperty = NonFrozenColumnsViewportHorizontalOffsetPropertyKey.DependencyProperty;

        /// <summary>
        /// Property which gets/sets the start x coordinate of non frozen columns in view port
        /// </summary>
        public double NonFrozenColumnsViewportHorizontalOffset
        {
            get
            {
                return (double)GetValue(NonFrozenColumnsViewportHorizontalOffsetProperty);
            }
            internal set
            {
                SetValue(NonFrozenColumnsViewportHorizontalOffsetPropertyKey, value);
            }
        }

        /// <summary>
        /// Override of OnApplyTemplate which clear the scroll host member
        /// </summary>
        public override void OnApplyTemplate()
        {
            _internalScrollHost = null;
            base.OnApplyTemplate();
        }

        /// <summary>
        /// Method which gets called when Horizontal scroll occurs on the scroll viewer of datagrid.
        /// Forwards the call to rows and header presenter.
        /// </summary>
        internal void OnHorizontalScroll()
        {
            ContainerTracking<DataGridRow> tracker = _rowTrackingRoot;
            while (tracker != null)
            {
                tracker.Container.OnHorizontalScroll();
                tracker = tracker.Next;
            }

            if (ColumnHeadersPresenter != null)
            {
                ColumnHeadersPresenter.OnHorizontalScroll();
            }
        }
        #endregion

        #region Column Reordering

        /// <summary>
        /// Dependency Property for CanUserReorderColumns Property
        /// </summary>
        public static readonly DependencyProperty CanUserReorderColumnsProperty =
            DependencyProperty.Register("CanUserReorderColumns", typeof(bool), typeof(DataGrid), new FrameworkPropertyMetadata(true));

        /// <summary>
        /// The property which determines if an end user can re-order columns or not.
        /// </summary>
        public bool CanUserReorderColumns
        {
            get { return (bool)GetValue(CanUserReorderColumnsProperty); }
            set { SetValue(CanUserReorderColumnsProperty, value); }
        }

        /// <summary>
        /// Dependency Property for DragIndicatorStyle property
        /// </summary>
        public static readonly DependencyProperty DragIndicatorStyleProperty =
            DependencyProperty.Register("DragIndicatorStyle", typeof(Style), typeof(DataGrid), new FrameworkPropertyMetadata(null, OnNotifyColumnPropertyChanged));

        /// <summary>
        /// The style property which would be applied on the column header drag indicator
        /// </summary>
        public Style DragIndicatorStyle
        {
            get { return (Style)GetValue(DragIndicatorStyleProperty); }
            set { SetValue(DragIndicatorStyleProperty, value); }
        }

        /// <summary>
        /// Dependency Property for DropLocationIndicatorStyle property
        /// </summary>
        public static readonly DependencyProperty DropLocationIndicatorStyleProperty =
            DependencyProperty.Register("DropLocationIndicatorStyle", typeof(Style), typeof(DataGrid), new FrameworkPropertyMetadata(null));

        /// <summary>
        /// The style property which would be applied on the column header drop location indicator.
        /// </summary>
        public Style DropLocationIndicatorStyle
        {
            get { return (Style)GetValue(DropLocationIndicatorStyleProperty); }
            set { SetValue(DropLocationIndicatorStyleProperty, value); }
        }

        public event EventHandler<DataGridColumnReorderingEventArgs> ColumnReordering;

        public event EventHandler<DragStartedEventArgs> ColumnHeaderDragStarted;

        public event EventHandler<DragDeltaEventArgs> ColumnHeaderDragDelta;

        public event EventHandler<DragCompletedEventArgs> ColumnHeaderDragCompleted;

        public event EventHandler<DataGridColumnEventArgs> ColumnReordered;

        internal protected virtual void OnColumnHeaderDragStarted(DragStartedEventArgs e)
        {
            if (ColumnHeaderDragStarted != null)
            {
                ColumnHeaderDragStarted(this, e);
            }
        }

        internal protected virtual void OnColumnReordering(DataGridColumnReorderingEventArgs e)
        {
            if (ColumnReordering != null)
            {
                ColumnReordering(this, e);
            }
        }

        internal protected virtual void OnColumnHeaderDragDelta(DragDeltaEventArgs e)
        {
            if (ColumnHeaderDragDelta != null)
            {
                ColumnHeaderDragDelta(this, e);
            }
        }

        internal protected virtual void OnColumnHeaderDragCompleted(DragCompletedEventArgs e)
        {
            if (ColumnHeaderDragCompleted != null)
            {
                ColumnHeaderDragCompleted(this, e);
            }
        }

        internal protected virtual void OnColumnReordered(DataGridColumnEventArgs e)
        {
            if (ColumnReordered != null)
            {
                ColumnReordered(this, e);
            }
        }

        #endregion

        #region Clipboard Copy

        /// <summary>
        ///     The DependencyProperty that represents the ClipboardCopyMode property.
        /// </summary>
        public static readonly DependencyProperty ClipboardCopyModeProperty =
            DependencyProperty.Register("ClipboardCopyMode", typeof(DataGridClipboardCopyMode), typeof(DataGrid), new FrameworkPropertyMetadata(DataGridClipboardCopyMode.ExcludeHeader, new PropertyChangedCallback(OnClipboardCopyModeChanged)));

        private static void OnClipboardCopyModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // The Copy command needs to have CanExecute run
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// The property which determines how DataGrid content is copied to the Clipboard.
        /// </summary>
        public DataGridClipboardCopyMode ClipboardCopyMode
        {
            get { return (DataGridClipboardCopyMode)GetValue(ClipboardCopyModeProperty); }
            set { SetValue(ClipboardCopyModeProperty, value); }
        }

        private static void OnCanExecuteCopy(object target, CanExecuteRoutedEventArgs args)
        {
            ((DataGrid)target).OnCanExecuteCopy(args);
        }

        /// <summary>
        /// This virtual method is called when ApplicationCommands.Copy command query its state.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnCanExecuteCopy(CanExecuteRoutedEventArgs args)
        {
            args.CanExecute = ClipboardCopyMode != DataGridClipboardCopyMode.None && _selectedCells.Count > 0;
            args.Handled = true;
        }

        private static void OnExecutedCopy(object target, ExecutedRoutedEventArgs args)
        {
            ((DataGrid)target).OnExecutedCopy(args);
        }

        /// <summary>
        /// This virtual method is called when ApplicationCommands.Copy command is executed.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnExecutedCopy(ExecutedRoutedEventArgs args)
        {
            if (ClipboardCopyMode == DataGridClipboardCopyMode.None)
            {
                throw new NotSupportedException(SR.Get(SRID.ClipboardCopyMode_Disabled));
            }
            args.Handled = true;

            // Supported default formats: Html, Text, UnicodeText and CSV
            Collection<string> formats = new Collection<string>(new string[] { DataFormats.Html, DataFormats.Text, DataFormats.UnicodeText, DataFormats.CommaSeparatedValue });
            Dictionary<string, StringBuilder> dataGridStringBuilders = new Dictionary<string, StringBuilder>(formats.Count);
            foreach (string format in formats)
            {
                dataGridStringBuilders[format] = new StringBuilder();
            }

            int minRowIndex;
            int maxRowIndex;
            int minColumnDisplayIndex;
            int maxColumnDisplayIndex;

            // Get the bounding box of the selected cells
            if (_selectedCells.GetSelectionRange(out minColumnDisplayIndex, out maxColumnDisplayIndex, out minRowIndex, out maxRowIndex))
            {
                // Add column headers if enabled
                if (ClipboardCopyMode == DataGridClipboardCopyMode.IncludeHeader)
                {
                    DataGridRowClipboardEventArgs preparingRowClipboardContentEventArgs = new DataGridRowClipboardEventArgs(null, minColumnDisplayIndex, maxColumnDisplayIndex, true /*IsColumnHeadersRow*/);
                    OnCopyingRowClipboardContent(preparingRowClipboardContentEventArgs);

                    foreach (string format in formats)
                    {
                        dataGridStringBuilders[format].Append(preparingRowClipboardContentEventArgs.FormatClipboardCellValues(format));
                    }
                }

                // Add each selected row
                for (int i = minRowIndex; i <= maxRowIndex; i++)
                {
                    object row = Items[i];
                    if (_selectedCells.Intersects(i)) // Row has a selecion
                    {
                        DataGridRowClipboardEventArgs preparingRowClipboardContentEventArgs = new DataGridRowClipboardEventArgs(row, minColumnDisplayIndex, maxColumnDisplayIndex, false /*IsColumnHeadersRow*/, i);
                        OnCopyingRowClipboardContent(preparingRowClipboardContentEventArgs);

                        foreach (string format in formats)
                        {
                            dataGridStringBuilders[format].Append(preparingRowClipboardContentEventArgs.FormatClipboardCellValues(format));
                        }
                    }
                }
            }

            ClipboardHelper.GetClipboardContentForHtml(dataGridStringBuilders[DataFormats.Html]);

            try
            {
                DataObject dataObject = new DataObject();
                foreach (string format in formats)
                {
                    dataObject.SetData(format, dataGridStringBuilders[format].ToString(), false /*autoConvert*/);
                }
                Clipboard.SetDataObject(dataObject);
            }
            catch (SecurityException)
            {
                // In partial trust we will have a security exception because clipboard operations require elevated permissions
                // Bug: Once the security team fix Clipboard.SetText - we can remove this catch
                // Temp: Use TextBox.Copy to have at least Text format in the clipboard
                TextBox textBox = new TextBox();
                textBox.Text = dataGridStringBuilders[DataFormats.Text].ToString();
                textBox.SelectAll();
                textBox.Copy();
            }
        }

        /// <summary>
        /// This method is called to prepare the clipboard content for each selected row.
        /// If ClipboardCopyMode is set to ClipboardCopyMode, then it is also called to prepare the column headers
        /// </summary>
        /// <param name="args">Contains the necessary information for generating the row clipboard content.</param>
        protected virtual void OnCopyingRowClipboardContent(DataGridRowClipboardEventArgs args)
        {
            if (args.IsColumnHeadersRow)
            {
                for (int i = args.StartColumnDisplayIndex; i <= args.EndColumnDisplayIndex; i++)
                {
                    DataGridColumn column = ColumnFromDisplayIndex(i);
                    args.ClipboardRowContent.Add(new DataGridClipboardCellContent(args.Item, column, column.Header));
                }
            }
            else
            {
                int rowIndex = args.RowIndexHint;
                if (rowIndex < 0)
                {
                    rowIndex = Items.IndexOf(args.Item);
                }
                if (_selectedCells.Intersects(rowIndex)) // If row has selection
                {
                    for (int i = args.StartColumnDisplayIndex; i <= args.EndColumnDisplayIndex; i++)
                    {
                        DataGridColumn column = ColumnFromDisplayIndex(i);
                        object cellValue = null;

                        // Get cell value only if the cell is selected - otherwise leave it null
                        if (_selectedCells.Contains(rowIndex, i))
                        {
                            cellValue = column.OnCopyingCellClipboardContent(args.Item);
                        }

                        args.ClipboardRowContent.Add(new DataGridClipboardCellContent(args.Item, column, cellValue));
                    }
                }
            }

            // Raise the event to give a chance to external listeners to modify row clipboard content (e.ClipboardRow)
            if (CopyingRowClipboardContent != null)
            {
                CopyingRowClipboardContent(this, args);
            }
        }

        /// <summary>
        /// This event is raised by OnCopyingRowClipboardContent method after the default row content is prepared.
        /// Event listeners can modify or add to the row clipboard content
        /// </summary>
        public event EventHandler<DataGridRowClipboardEventArgs> CopyingRowClipboardContent;
        #endregion

        #region Cells Panel Width

        /// <summary>
        /// Dependency Property for CellsPanelActualWidth property
        /// </summary>
        internal static readonly DependencyProperty CellsPanelActualWidthProperty =
            DependencyProperty.Register(
                        "CellsPanelActualWidth", 
                        typeof(double), 
                        typeof(DataGrid), 
                        new FrameworkPropertyMetadata(0.0, new PropertyChangedCallback(CellsPanelActualWidthChanged)));

        /// <summary>
        /// The property which represents the actual width of the cells panel,
        /// to be used by headers presenter
        /// </summary>
        internal double CellsPanelActualWidth
        {
            get
            {
                return (double)GetValue(CellsPanelActualWidthProperty);
            }
            set
            {
                SetValue(CellsPanelActualWidthProperty, value);
            }
        }

        /// <summary>
        /// Property changed callback for CellsPanelActualWidth property
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void CellsPanelActualWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            double oldValue = (double)e.OldValue;
            double newValue = (double)e.NewValue;
            if (!DoubleUtil.AreClose(oldValue, newValue))
            {
                ((DataGrid)d).NotifyPropertyChanged(d, e, NotificationTarget.ColumnHeadersPresenter);
            }
        }

        #endregion

        #region Helpers

        // TODO: Consider making this public.
        // Used as an alternate data item to CollectionView.NewItemPlaceholder so that
        // CellsPresenter's ItemContainerGenerator does not get confused.
        internal static object NewItemPlaceholder
        {
            get { return _newItemPlaceholder; }
        }

        #endregion

        #region Native

        // Used for AutoScrollTimeout
        [DllImport("User32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern int GetDoubleClickTime();

        #endregion

        #region Data

        private static ComponentResourceKey             _focusBorderBrushKey;           // Used in styles
        private static IValueConverter                  _headersVisibilityConverter;    // Used to convert DataGridHeadersVisibility to Visibility in styles

        private static object                           _newItemPlaceholder = new object(); // Used as an alternate data item to CollectionView.NewItemPlaceholder

        private DataGridColumnCollection                _columns;                       // Stores the columns
        private ContainerTracking<DataGridRow>          _rowTrackingRoot;               // Root of a linked list of active row containers
        private DataGridColumnHeadersPresenter          _columnHeadersPresenter;        // headers presenter for sending down notifications
        private DataGridCell                            _currentCellContainer;          // Reference to the cell container corresponding to CurrentCell (use CurrentCellContainer property instead)
        private DataGridCell                            _pendingCurrentCellContainer;   // Reference to the cell container that will become the current cell
        private SelectedCellsCollection                 _selectedCells;                 // Stores the selected cells
        private Nullable<DataGridCellInfo>              _selectionAnchor;               // For doing extended selection
        private bool                                    _isDraggingSelection;           // Whether a drag select is being performed
        private bool                                    _isRowDragging;                 // Whether a drag select is being done on rows
        private Panel                                   _internalItemsHost;             // Workaround for not having access to ItemsHost
        private ScrollViewer                            _internalScrollHost;            // Scroll viewer of the datagrid
        private DispatcherTimer                         _autoScrollTimer;               // Timer to tick auto-scroll
        private bool                                    _hasAutoScrolled;               // Whether an auto-scroll has occurred since starting the tick
        private VirtualizedCellInfoCollection           _pendingSelectedCells;          // Cells that were selected that haven't gone through SelectedCellsChanged
        private VirtualizedCellInfoCollection           _pendingUnselectedCells;        // Cells that were unselected that haven't gone through SelectedCellsChanged
        private bool                                    _deferAutoGeneration = false;   // The flag which determines whether the columns generation is deferred
        private bool                                    _measureNeverInvoked = true;    // Flag used to track if measure was invoked atleast once. Particularly used for AutoGeneration.
        private bool                                    _updatingSelectedCells = false; // Whether to defer notifying that SelectedCells changed.
        private Visibility                              _previousPlaceholderVisibility = Visibility.Visible;    // The visibility used before hiding the NewItemPlaceholder during an edit.
        private Visibility                              _placeholderVisibility         = Visibility.Visible;    // The visibility used for the Placeholder container.  It may not exist at all times, so it's stored on the DG.
        private Point                                   _dragPoint;                     // Used to detect if a drag actually occurred
        private List<int>                               _groupingSortDescriptionIndices = null; // List to hold the indices of SortDescriptions added for the sake of GroupDescriptions.
        private bool                                    _ignoreSortDescriptionsChange = false;  // Flag used to neglect the SortDescriptionCollection changes in the CollectionChanged listener.
        private bool                                    _sortingStarted = false;                // Flag used to track if Sorting ever started or not.

        #endregion
    }
}
