//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MS.Internal;


namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     Internal class that holds the DataGrid's column collection.  Handles error-checking columns as they come in.
    /// </summary>
    internal class DataGridColumnCollection : ObservableCollection<DataGridColumn>
    {
        internal DataGridColumnCollection(DataGrid dataGridOwner)
        {
            Debug.Assert(dataGridOwner != null, "We should have a valid DataGrid");

            DisplayIndexMap = new List<int>(5);
            _dataGridOwner = dataGridOwner;
        }

        #region Protected Overrides

        protected override void InsertItem(int index, DataGridColumn item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item", SR.Get(SRID.DataGrid_NullColumn));
            }

            if (DisplayIndexMapInitialized)
            {
                ValidateDisplayIndex(item, item.DisplayIndex, true);
            }
            base.InsertItem(index, item);
            item.CoerceValue(DataGridColumn.IsFrozenProperty);
        }

        protected override void SetItem(int index, DataGridColumn item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item", SR.Get(SRID.DataGrid_NullColumn));
            }

            if (DisplayIndexMapInitialized)
            {
                ValidateDisplayIndex(item, item.DisplayIndex);
            }
            base.SetItem(index, item);
            item.CoerceValue(DataGridColumn.IsFrozenProperty);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (DisplayIndexMapInitialized)
                    {
                        UpdateDisplayIndexForNewColumns(e.NewItems, e.NewStartingIndex);
                    }
                    InvalidateStarComputationFlagForColumns(e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (DisplayIndexMapInitialized)
                    {
                        UpdateDisplayIndexForMovedColumn(e.OldStartingIndex, e.NewStartingIndex);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (DisplayIndexMapInitialized)
                    {
                        UpdateDisplayIndexForRemovedColumns(e.OldItems, e.OldStartingIndex);
                    }
                    InvalidateStarComputationFlagForColumns(e.OldItems);
                    ClearDisplayIndex(e.OldItems, e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (DisplayIndexMapInitialized)
                    {
                        UpdateDisplayIndexForReplacedColumn(e.OldItems, e.NewItems);
                    }
                    InvalidateStarComputationFlagForColumns(e.OldItems);
                    InvalidateStarComputationFlagForColumns(e.NewItems);
                    ClearDisplayIndex(e.OldItems, e.NewItems);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // We dont ClearDisplayIndex here because we no longer have access to the old items.
                    // Instead this is handled in ClearItems.
                    if (DisplayIndexMapInitialized)
                    {
                        DisplayIndexMap.Clear();
                        DataGridOwner.UpdateColumnsOnVirtualizedCellInfoCollections(NotifyCollectionChangedAction.Reset, -1, null, -1);
                    }
                    ComputeStarColumnWidths = true;
                    break;
            }

            base.OnCollectionChanged(e);
        }

        /// <summary>
        /// Clear's all the columns from this collection and resets DisplayIndex to its default value.
        /// </summary>
        protected override void ClearItems()
        {
            ClearDisplayIndex(this, null);
            base.ClearItems();
        }

        #endregion 

        #region Notification Propagation

        internal void NotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e, NotificationTarget target)
        {
            if (DataGridHelper.ShouldNotifyColumnCollection(target))
            {
                if (e.Property == DataGridColumn.DisplayIndexProperty)
                {
                    OnColumnDisplayIndexChanged((DataGridColumn)d, (int)e.OldValue, (int)e.NewValue);
                }
                else if (e.Property == DataGrid.FrozenColumnCountProperty)
                {
                    OnDataGridFrozenColumnCountChanged((DataGrid)d, (int)e.OldValue, (int)e.NewValue);
                }
                else if (e.Property == DataGridColumn.WidthProperty)
                {
                    OnColumnWidthChanged((DataGridColumn)d, (DataGridLength)e.OldValue, (DataGridLength)e.NewValue);
                }
                else if (e.Property == DataGridColumn.ActualWidthProperty)
                {
                    OnColumnActualWidthChanged((DataGridColumn)d, (double)e.OldValue, (double)e.NewValue);
                }
            }
            
            if (DataGridHelper.ShouldNotifyColumns(target))
            {
                int count = this.Count;
                for (int i = 0; i < count; i++)
                {
                    // Passing in NotificationTarget.Columns directly to ensure the notification doesn't
                    // bounce back to the collection.
                    this[i].NotifyPropertyChanged(d, e, NotificationTarget.Columns);
                }
            }
        }


        #endregion

        #region Display Index


        /// <summary>
        ///     Returns the DataGridColumn with the given DisplayIndex
        /// </summary>
        internal DataGridColumn ColumnFromDisplayIndex(int displayIndex)
        {
            Debug.Assert(displayIndex >= 0 && displayIndex < DisplayIndexMap.Count, "displayIndex should have already been validated");
            return this[DisplayIndexMap[displayIndex]];
        }

        /// <summary>
        ///     A map of display index (key) to index in the column collection (value).  Used to quickly find a column from its display index.
        /// </summary>
        internal List<int> DisplayIndexMap
        {
            get
            {
                if (!DisplayIndexMapInitialized)
                {
                    InitializeDisplayIndexMap();
                }
                return _displayIndexMap;
            }
            private set { _displayIndexMap = value; }
        }

        /// <summary>
        ///     Used to guard against re-entrancy when changing the DisplayIndex of a column.
        /// </summary>
        private bool IsUpdatingDisplayIndex
        {
            get { return _isUpdatingDisplayIndex; }
            set { _isUpdatingDisplayIndex = value; }
        }


        private int CoerceDefaultDisplayIndex(DataGridColumn column)
        {
            return CoerceDefaultDisplayIndex(column, IndexOf(column));
        }

        /// <summary>
        /// This takes a column and checks that if its DisplayIndex is the default value.  If so, it coerces
        /// the DisplayIndex to be its location in the columns collection.
        /// We can't do this in CoerceValue because the callback isn't called for default values.  Instead we call this
        /// whenever a column is added or replaced in the collection or when the DisplayIndex of an existing column has changed.
        /// </summary>
        /// <param name="column">The column</param>
        /// <param name="newDisplayIndex">The DisplayIndex the column should have</param>
        /// <returns>The DisplayIndex of the column</returns>
        private int CoerceDefaultDisplayIndex(DataGridColumn column, int newDisplayIndex)
        {
            if (DataGridHelper.IsDefaultValue(column, DataGridColumn.DisplayIndexProperty))
            {
                bool isUpdating = IsUpdatingDisplayIndex;
                try
                {
                    IsUpdatingDisplayIndex = true;
                    column.DisplayIndex = newDisplayIndex;
                }
                finally
                {
                    IsUpdatingDisplayIndex = isUpdating;
                }

                return newDisplayIndex;
            }

            return column.DisplayIndex;
        }

        /// <summary>
        ///     Called when a column's display index has changed.  
        /// <param name="oldDisplayIndex">the old display index of the column</param>
        /// <param name="newDisplayIndex">the new display index of the column</param>
        private void OnColumnDisplayIndexChanged(DataGridColumn column, int oldDisplayIndex, int newDisplayIndex)
        {
            //
            // Handle ClearValue.  -1 is the default value and really means 'DisplayIndex should be the index of the column in the column collection'.
            // We immediately replace the display index without notifying anyone.
            //
            if (oldDisplayIndex == -1 || _isClearingDisplayIndex)
            {
                // change from -1 to the new value; the OnColumnDisplayIndexChanged further down the stack (from old value to -1) will handle
                // notifying the user and updating columns.
                return;
            }

            // The DisplayIndex may have changed to the default value.  
            newDisplayIndex = CoerceDefaultDisplayIndex(column);

            if (newDisplayIndex == oldDisplayIndex)
            {
                return;
            }

            //
            // Our coerce value callback should have validated the DisplayIndex.  Fire the virtual.
            //

            Debug.Assert(newDisplayIndex >= 0 && newDisplayIndex < Count, "The new DisplayIndex should have already been validated");
            DataGridOwner.OnColumnDisplayIndexChanged(new DataGridColumnEventArgs(column));

            // Call our helper to walk through all other columns and adjust their display indices.
            UpdateDisplayIndexForChangedColumn(oldDisplayIndex, newDisplayIndex);
        }

        /// <summary>
        ///     Called when the DisplayIndex for a single column has changed.  The other columns may have conflicting display indices, so
        ///     we walk through them and adjust.  This method does nothing if we're already updating display index as part of a larger
        ///     operation (such as add or remove).  This is both for re-entrancy and to avoid modifying the display index map as we walk over
        ///     the columns.
        /// </summary>
        private void UpdateDisplayIndexForChangedColumn(int oldDisplayIndex, int newDisplayIndex)
        {
            //
            // The code below adjusts the DisplayIndex of other columns and shouldn't happen if this column's display index is changed
            // to account for the change in another.
            //

            if (IsUpdatingDisplayIndex)
            {
                // Avoid re-entrancy; setting DisplayIndex on columns causes their OnDisplayIndexChanged to fire.
                return;
            }

            try
            {
                IsUpdatingDisplayIndex = true;

                Debug.Assert(oldDisplayIndex != newDisplayIndex, "A column's display index must have changed for us to call OnColumnDisplayIndexChanged");
                Debug.Assert(oldDisplayIndex >= 0 && oldDisplayIndex < Count, "The old DisplayIndex should be valid");

                //
                // Update the display index of other columns.
                //
                if (newDisplayIndex < oldDisplayIndex)
                {
                    // DisplayIndex decreased. All columns with DisplayIndex >= newDisplayIndex and < oldDisplayIndex
                    // get their DisplayIndex incremented.

                    for (int i = newDisplayIndex; i < oldDisplayIndex; i++)
                    {
                        ColumnFromDisplayIndex(i).DisplayIndex++;
                    }
                }
                else
                {
                    // DisplayIndex increased. All columns with DisplayIndex <= newDisplayIndex and > oldDisplayIndex get their DisplayIndex decremented.

                    for (int i = oldDisplayIndex + 1; i <= newDisplayIndex; i++)
                    {
                        ColumnFromDisplayIndex(i).DisplayIndex--;
                    }
                }

                //
                // Update the display index mapping for all affected columns.
                //

                int columnIndex = DisplayIndexMap[oldDisplayIndex];
                DisplayIndexMap.RemoveAt(oldDisplayIndex);
                DisplayIndexMap.Insert(newDisplayIndex, columnIndex);

                debug_VerifyDisplayIndexMap();

                DataGridOwner.UpdateColumnsOnVirtualizedCellInfoCollections(NotifyCollectionChangedAction.Move, oldDisplayIndex, null, newDisplayIndex);
            }
            finally
            {
                IsUpdatingDisplayIndex = false;
            }
        }

        private void UpdateDisplayIndexForMovedColumn(int oldColumnIndex, int newColumnIndex)
        {
            int displayIndex = RemoveFromDisplayIndexMap(oldColumnIndex);
            InsertInDisplayIndexMap(displayIndex, newColumnIndex);
            DataGridOwner.UpdateColumnsOnVirtualizedCellInfoCollections(NotifyCollectionChangedAction.Move, oldColumnIndex, null, newColumnIndex);
        }

        /// <summary>
        ///     Sets the DisplayIndex on all newly inserted or added columns and updates the existing columns as necessary.  
        /// </summary>
        private void UpdateDisplayIndexForNewColumns(IList newColumns, int startingIndex)
        {
            DataGridColumn column;
            int newDisplayIndex, columnIndex;
            int newColumnCount = newColumns.Count;
            int columnCount = Count;

            Debug.Assert(newColumns.Count == 1,
                "This derives from ObservableCollection; it is impossible to remove multiple columns at once");
            Debug.Assert(IsUpdatingDisplayIndex == false, "We don't add new columns as part of a display index update operation");

            try
            {
                IsUpdatingDisplayIndex = true;

                //
                // Set the display index of the new columns and add them to the DisplayIndexMap
                //

                column = (DataGridColumn)newColumns[0];
                columnIndex = startingIndex;

                newDisplayIndex = CoerceDefaultDisplayIndex(column, columnIndex);

                // Inserting the column in the map means that all columns with display index >= the new column's display index
                // were given a higher display index.  This is perfect, except that the column indices have changed due to the insert
                // in the column collection.  We need to iterate over the column indices and increment them appropriately.  We also
                // need to give each changed column a new display index.

                InsertInDisplayIndexMap(newDisplayIndex, columnIndex);

                for (int i = 0; i < DisplayIndexMap.Count; i++)
                {
                    if (i > newDisplayIndex)
                    {
                        // All columns with DisplayIndex higher than the newly inserted columns
                        // need to have their DisplayIndex adiusted.
                        column = ColumnFromDisplayIndex(i);
                        column.DisplayIndex++;
                    }
                }

                debug_VerifyDisplayIndexMap();

                DataGridOwner.UpdateColumnsOnVirtualizedCellInfoCollections(NotifyCollectionChangedAction.Add, -1, null, newDisplayIndex);
            }
            finally
            {
                IsUpdatingDisplayIndex = false;
            }
        }

        // This method is called in first DataGrid measure call
        // It needs to populate DisplayIndexMap and validate the DisplayIndex of all columns
        internal void InitializeDisplayIndexMap()
        {
            if (_displayIndexMapInitialized)
                return;

            _displayIndexMapInitialized = true;

            Debug.Assert(DisplayIndexMap.Count == 0, "DisplayIndexMap should be empty until first measure call.");
            int columnCount = Count;
            Dictionary<int, int> assignedDisplayIndexMap = new Dictionary<int, int>(); // <DisplayIndex, ColumnIndex>

            // First loop:
            // 1. Validate all columns DisplayIndex
            // 2. Add columns with DisplayIndex!=default to the assignedDisplayIndexMap
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                DataGridColumn currentColumn = this[columnIndex];
                int currentColumnDisplayIndex = currentColumn.DisplayIndex;
                
                ValidateDisplayIndex(currentColumn, currentColumnDisplayIndex);
                if (currentColumnDisplayIndex >=0)
                {
                    if (assignedDisplayIndexMap.ContainsKey(currentColumnDisplayIndex))
                    {
                        throw new ArgumentException(SR.Get(SRID.DataGrid_DuplicateDisplayIndex));
                    }

                    assignedDisplayIndexMap.Add(currentColumnDisplayIndex, columnIndex);
                }
            }

            // Second loop:
            // Assign DisplayIndex to the columns with default values
            int nextAvailableColumnIndex = 0;
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                DataGridColumn currentColumn = this[columnIndex];
                int currentColumnDisplayIndex = currentColumn.DisplayIndex;
                
                bool hasDefaultDisplayIndex = DataGridHelper.IsDefaultValue(currentColumn, DataGridColumn.DisplayIndexProperty);
                if (hasDefaultDisplayIndex)
                {
                    while (assignedDisplayIndexMap.ContainsKey(nextAvailableColumnIndex))
                    {
                        nextAvailableColumnIndex++;
                    }

                    CoerceDefaultDisplayIndex(currentColumn, nextAvailableColumnIndex);
                    assignedDisplayIndexMap.Add(nextAvailableColumnIndex, columnIndex);
                    nextAvailableColumnIndex++;
                }
            }

            // Third loop:
            // Copy generated assignedDisplayIndexMap into DisplayIndexMap
            for (int displayIndex = 0; displayIndex < columnCount; displayIndex++)
            {
                Debug.Assert(assignedDisplayIndexMap.ContainsKey(displayIndex));
                DisplayIndexMap.Add(assignedDisplayIndexMap[displayIndex]);
            }
        }


        /// <summary>
        ///     Updates the display index for all columns affected by the removal of a set of columns.  
        /// </summary>
        private void UpdateDisplayIndexForRemovedColumns(IList oldColumns, int startingIndex)
        {
            DataGridColumn column;
            Debug.Assert(oldColumns.Count == 1, 
                "This derives from ObservableCollection; it is impossible to remove multiple columns at once");
            Debug.Assert(IsUpdatingDisplayIndex == false, "We don't remove columns as part of a display index update operation");

            try
            {
                IsUpdatingDisplayIndex = true;
                Debug.Assert(DisplayIndexMap.Count > Count, "Columns were just removed: the display index map shouldn't have yet been updated");

                int removedDisplayIndex = RemoveFromDisplayIndexMap(startingIndex);

                // Removing the column in the map means that all columns with display index >= the new column's display index
                // were given a lower display index.  This is perfect, except that the column indices have changed due to the insert
                // in the column collection.  We need to iterate over the column indices and decrement them appropriately.  We also
                // need to give each changed column a new display index.

                for (int i = 0; i < DisplayIndexMap.Count; i++)
                {
                    if (i >= removedDisplayIndex)
                    {
                        // All columns with DisplayIndex higher than the newly deleted columns need to have their DisplayIndex adiusted
                        // (we use >= because a column will have been decremented to have the same display index as the deleted column).
                        column = ColumnFromDisplayIndex(i);
                        column.DisplayIndex--;
                    }
                }

                debug_VerifyDisplayIndexMap();

                DataGridOwner.UpdateColumnsOnVirtualizedCellInfoCollections(NotifyCollectionChangedAction.Remove, removedDisplayIndex, (DataGridColumn)oldColumns[0], -1);
            }
            finally
            {
                IsUpdatingDisplayIndex = false;
            }
        }

        /// <summary>
        ///     Updates the display index for the column that was just replaced and adjusts the other columns if necessary
        /// </summary>
        private void UpdateDisplayIndexForReplacedColumn(IList oldColumns, IList newColumns)
        { 
            if (oldColumns!= null && oldColumns.Count > 0 && newColumns != null && newColumns.Count > 0)
            {
                Debug.Assert(oldColumns.Count == 1 && newColumns.Count == 1, "Multi replace isn't possible with ObservableCollection");
                DataGridColumn oldColumn = (DataGridColumn)oldColumns[0];
                DataGridColumn newColumn = (DataGridColumn)newColumns[0];

                if (oldColumn != null && newColumn != null)
                {
                    int newDisplayIndex = CoerceDefaultDisplayIndex(newColumn);

                    if (oldColumn.DisplayIndex != newDisplayIndex)
                    {
                        // Update the display index of other columns to adjust for that of the new one.
                        UpdateDisplayIndexForChangedColumn(oldColumn.DisplayIndex, newDisplayIndex);
                    }

                    DataGridOwner.UpdateColumnsOnVirtualizedCellInfoCollections(NotifyCollectionChangedAction.Replace, newDisplayIndex, oldColumn, newDisplayIndex);
                }
            }
        }

        /// <summary>
        /// Clears the DisplayIndexProperty on each of the columns.
        /// </summary>
        private void ClearDisplayIndex(IList oldColumns, IList newColumns)
        {
            if (oldColumns != null)
            {
                try
                {
                    _isClearingDisplayIndex = true;
                    var count = oldColumns.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var column = (DataGridColumn)oldColumns[i];
                        
                        // Only clear the old column's index if its not in newColumns
                        if (newColumns != null && newColumns.Contains(column))
                        {
                            continue;
                        }
                        
                        column.ClearValue(DataGridColumn.DisplayIndexProperty);
                    }
                }
                finally
                {
                    _isClearingDisplayIndex = false;
                }
            }
        }

        private bool IsDisplayIndexValid(DataGridColumn column, int displayIndex)
        {
            return IsDisplayIndexValid(column, displayIndex, false);
        }

        /// <summary>
        ///     Returns true if the display index is valid for the given column
        /// </summary>
        private bool IsDisplayIndexValid(DataGridColumn column, int displayIndex, bool isAdding)
        {
            // -1 is legal only as a default value
            if (displayIndex == -1 && DataGridHelper.IsDefaultValue(column, DataGridColumn.DisplayIndexProperty))
            {
                return true;
            }
             
            // If we're adding a column the count will soon be increased by one -- so a DisplayIndex == Count is ok.
            return displayIndex >= 0 && (isAdding ? displayIndex <= Count : displayIndex < Count);
        }

        /// <summary>
        ///     Inserts the given columnIndex in the DisplayIndexMap at the given display index.
        /// </summary>
        private void InsertInDisplayIndexMap(int newDisplayIndex, int columnIndex)
        {
            DisplayIndexMap.Insert(newDisplayIndex, columnIndex);

            for (int i = 0; i < DisplayIndexMap.Count; i++)
            {
                if (DisplayIndexMap[i] >= columnIndex && i != newDisplayIndex)
                {
                    // These are columns that are after the inserted item in the column collection; we have to adiust
                    // to account for the shifted column index.
                    DisplayIndexMap[i]++;
                }
            }
        }

        /// <summary>
        ///     Removes the given column index from the DisplayIndexMap
        /// </summary>
        private int RemoveFromDisplayIndexMap(int columnIndex)
        {
            int removedDisplayIndex = DisplayIndexMap.IndexOf(columnIndex);
            Debug.Assert(removedDisplayIndex >= 0);

            DisplayIndexMap.RemoveAt(removedDisplayIndex);

            for (int i = 0; i < DisplayIndexMap.Count; i++)
            {
                if (DisplayIndexMap[i] >= columnIndex)
                {
                    // These are columns that are after the removed item in the column collection; we have to adiust
                    // to account for the shifted column index.
                    DisplayIndexMap[i]--;
                }
            }

            return removedDisplayIndex;
        }

        /// <summary>
        ///     Throws an ArgumentOutOfRangeException if the given displayIndex is invalid for the given column.
        /// </summary>
        internal void ValidateDisplayIndex(DataGridColumn column, int displayIndex)
        {
            ValidateDisplayIndex(column, displayIndex, false);
        }

        /// <summary>
        ///     Throws an ArgumentOutOfRangeException if the given displayIndex is invalid for the given column.
        /// </summary>
        internal void ValidateDisplayIndex(DataGridColumn column, int displayIndex, bool isAdding)
        {
            if (!IsDisplayIndexValid(column, displayIndex, isAdding))
            {
                throw new ArgumentOutOfRangeException("displayIndex", displayIndex, SR.Get(SRID.DataGrid_ColumnDisplayIndexOutOfRange, column.Header));
            }
        }

        [Conditional("DEBUG")]
        private void debug_VerifyDisplayIndexMap()
        {
            Debug.Assert(Count == DisplayIndexMap.Count, "Display Index map is of the wrong size");
            for (int i = 0; i < DisplayIndexMap.Count; i++)
            {
                Debug.Assert(DisplayIndexMap[i] >= 0 && DisplayIndexMap[i] < Count, "DisplayIndex map entry doesn't point to a valid column");
                Debug.Assert(ColumnFromDisplayIndex(i).DisplayIndex == i, "DisplayIndex map doesn't match column indices");
            }
        }

        #endregion 

        #region Frozen Columns

        /// <summary>
        /// Method which sets / resets the IsFrozen property of columns based on DataGrid's FrozenColumnCount.
        /// It is possible that the FrozenColumnCount change could be a result of column count itself, in
        /// which case only the columns which are in the collection at the moment are to be considered.
        /// </summary>
        /// <param name="dataGrid"></param>
        /// <param name="oldFrozenCount"></param>
        /// <param name="newFrozenCount"></param>
        private void OnDataGridFrozenColumnCountChanged(DataGrid dataGrid, int oldFrozenCount, int newFrozenCount)
        {
            if (newFrozenCount > oldFrozenCount)
            {
                int columnCount = Math.Min(newFrozenCount, Count);
                for (int i = oldFrozenCount; i < columnCount; i++)
                {
                    ColumnFromDisplayIndex(i).IsFrozen = true;
                }
            }
            else
            {
                int columnCount = Math.Min(oldFrozenCount, Count);
                for (int i = newFrozenCount; i < columnCount; i++)
                {
                    ColumnFromDisplayIndex(i).IsFrozen = false;
                }
            }
        }

        #endregion

        #region Star Column Widths

        /// <summary>
        /// 
        /// </summary>
        internal bool ComputeStarColumnWidths
        {
            get
            {
                return _computeStarColumnWidths;
            }
            set
            {
                _computeStarColumnWidths = value;
            }
        }

        private void InvalidateStarComputationFlagForColumns(IList columns)
        {
            foreach (DataGridColumn column in columns)
            {
                if (column.Width.IsStar)
                {
                    ComputeStarColumnWidths = true;
                    break;
                }
            }
        }

        private void OnColumnWidthChanged(DataGridColumn column, DataGridLength oldWidth, DataGridLength newWidth)
        {
            if (oldWidth.IsStar || newWidth.IsStar)
            {
                ComputeStarColumnWidths = true;
            }
        }

        private void OnColumnActualWidthChanged(DataGridColumn column, double oldWidth, double newWidth)
        {
            if (!column.Width.IsStar && !DoubleUtil.AreClose(oldWidth, newWidth))
            {
                ComputeStarColumnWidths = true;
            }
        }

        #endregion

        #region Helpers

        private DataGrid DataGridOwner
        {
            get { return _dataGridOwner; }
        }

        // Used by DataGridColumnCollection to delay the validation of DisplayIndex
        // Validation should be delayed because we in the process of adding columns we may have DisplayIndex less that current columns number
        // After all columns are generated or added in xaml we can do the validation
        internal bool DisplayIndexMapInitialized { get { return _displayIndexMapInitialized; } }

        #endregion

        #region Data

        private DataGrid  _dataGridOwner;
        private bool      _isUpdatingDisplayIndex;     // true if we're in the middle of updating the display index of each column.
        private List<int> _displayIndexMap;            // maps a DisplayIndex to an index in the _columns collection.
        private bool      _displayIndexMapInitialized; // Flag is used to delay the validation of DisplayIndex until the first measure
        private bool      _computeStarColumnWidths = false;
        private bool      _isClearingDisplayIndex = false; // Flag indicating that we're currently clearing the display index.  We should not coerce default display index's during this time. 

        #endregion 
    }
}