//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MS.Internal;


namespace Microsoft.Windows.Controls
{

    /// <summary>
    ///     Panel that lays out both cells and column headers. This stacks cells in the horizontal direction and communicates with the 
    ///     relevant DataGridColumn to ensure all rows give cells in a given column the same size.
    ///     It is hardcoded against DataGridCell and DataGridColumnHeader.
    /// </summary>
    public class DataGridCellsPanel : Panel
    {
        static DataGridCellsPanel()
        {
            KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(DataGridCellsPanel), new FrameworkPropertyMetadata(KeyboardNavigationMode.Local));
        }

        #region Layout

        /// <summary>
        /// Measure
        /// </summary>
        /// <param name="constraint">Size constraint</param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size constraint)
        {           
            UIElementCollection children = InternalChildren;
            Debug.Assert(children.Count == Columns.Count, "We must have one child per column");

            Size stackDesiredSize = new Size();
            Size layoutSlotSize = new Size(Double.PositiveInfinity, constraint.Height);
            bool isColumnHeader = false;

            //
            //  Iterate through children
            //
            for (int i = 0, count = children.Count; i < count; ++i)
            {
                // Get next child in the order of DisplayIndex
                UIElement child = GetChildAtDisplayIndex(children, i);  
                IProvideDataGridColumn cell = child as IProvideDataGridColumn;

                // Use the first child to determine if this row contains column headers.
                if (i == 0)
                {
                    isColumnHeader = (child is DataGridColumnHeader);
                }

                // Allow the column to affect the constraint.
                if (cell != null)
                {
                    Debug.Assert(cell.Column == (ParentDataGrid != null ? Columns[ParentDataGrid.DisplayIndexMap[i]] : Columns[i]), "each cell should match its column");
                    layoutSlotSize.Width = cell.Column.GetConstraintWidth(isColumnHeader);
                }

                // Measure the child.
                child.Measure(layoutSlotSize);
                Size childDesiredSize = child.DesiredSize;

                // Accumulate child size.
                if (cell != null)
                {
                    // Allow the column to process the desired size and return the amount the panel
                    // should use in its desired size.
                    stackDesiredSize.Width += cell.Column.UpdateDesiredWidth(isColumnHeader, childDesiredSize.Width);
                }
                else
                {
                    stackDesiredSize.Width += childDesiredSize.Width;
                }

                // Ensure that the panel's desired height is the max of all the child heights
                stackDesiredSize.Height = Math.Max(stackDesiredSize.Height, childDesiredSize.Height);
            }

            return stackDesiredSize;
        }

        /// <summary>
        /// Arrange
        /// </summary>
        /// <param name="arrangeSize">Arrange size</param>
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            UIElementCollection children = this.Children;
            Rect rcChild = new Rect(arrangeSize);
            double childSize = 0.0;
            double allocatedSpace = 0.0;
            bool isColumnHeader = false;

            /*
             * Determine how much space is available to columns that use "*" widths
             */
            for (int i = 0, count = children.Count; i < count; ++i)
            {
                // Iterate over the children in order of DisplayIndex
                UIElement child = GetChildAtDisplayIndex(children, i);
                IProvideDataGridColumn cell = child as IProvideDataGridColumn;

                // Use the first child to determine if this row contains column headers.
                if (i == 0)
                {
                    isColumnHeader = (child is DataGridColumnHeader);
                }

                if (cell != null)
                {
                    DataGridLength width = cell.Column.Width;
                    if (!width.IsStar)
                    {
                        // Add up already allocated space
                        allocatedSpace += cell.Column.ActualWidth;
                    }
                }
                else
                {
                    // Add up already allocated space
                    allocatedSpace += child.DesiredSize.Width;
                }
            }

            if (ParentDataGrid != null)
            {
                //Set the CellsPanelActualWidth property of the datagrid and invalidate star width computation if needed
                if (!isColumnHeader)
                {
                    if (!DoubleUtil.AreClose(ParentDataGrid.CellsPanelActualWidth, arrangeSize.Width))
                    {
                        ParentDataGrid.CellsPanelActualWidth = arrangeSize.Width;
                        DataGridHelper.InvalidateStarWidthComputation(ParentDataGrid);
                    }
                }

                //Do the star width computation if needed
                if (DataGridHelper.StarWidthComputationNeeded(ParentDataGrid))
                {
                    // Subtract the already allocated space from the available space
                    double availableStarSpace = Math.Max(0.0, arrangeSize.Width - allocatedSpace);
                    if (Double.IsInfinity(availableStarSpace) || Double.IsNaN(availableStarSpace))
                    {
                        availableStarSpace = 0.0;
                    }
                    DataGridHelper.ComputeStarColumnWidths(ParentDataGrid, availableStarSpace);
                }
            }

            UIElement newClippedChild = null;
            UIElement oldClippedChild = null;
            int frozenColumnCount = 0;
            double cellsPanelOffset               = 0.0;  //indicates the offset of cells panel from the start of viewport 
            double nextFrozenCellStart            = 0.0;  //indicates the start position for next frozen cell
            double nextNonFrozenCellStart         = 0.0;  //indicates the start position for next non-frozen cell
            double viewportStartX                 = 0.0;  //indicates the start of viewport with respect to coordinate system of cell panel
            double dataGridHorizontalScrollStartX = 0.0;  //indicates the start position of the horizontal scroll bar.

            /*
             * determine the horizontal offset, cells panel offset and other coordinates used for arrange of children
             */
            if (ParentDataGrid != null)
            {
                double horizontalOffset = 0.0;
                Point originPoint = new Point(0, 0);
                IScrollInfo scrollInfo = ParentDataGrid.InternalItemsHost as IScrollInfo;
                if (scrollInfo != null)
                {
                    horizontalOffset = scrollInfo.HorizontalOffset;
                }

                if (isColumnHeader || children.Count == 0)
                {
                    if (ParentDataGrid.InternalScrollHost != null && ParentPresenter != null)
                    {
                        //Determine the start position of the presenter wrt scrollviewer and start position of cells panel wrt to presenter
                        //The combination of these two along with horizontal offset will give cellsPanelOffset and dataGridHorizontalScrollStartX
                        double presenterStartX = ParentPresenter.TransformToAncestor(ParentDataGrid.InternalScrollHost).Transform(originPoint).X;
                        dataGridHorizontalScrollStartX = ParentDataGrid.InternalScrollHost.ContentHorizontalOffset + TransformToAncestor(ParentPresenter).Transform(originPoint).X;
                        cellsPanelOffset = dataGridHorizontalScrollStartX + presenterStartX;
                    }
                }
                else
                {
                    //Determine the start of cells panel wrt presenter and start of cells panel wrt to scroll viewer.
                    //The combination of these two along with horizontal offset will give the cellsPanelOffset and dataGridHorizontalScrollStartX
                    double cellsPanelPresenterOffset = 0.0;
                    if (ParentPresenter != null)
                    {
                        cellsPanelPresenterOffset = TransformToAncestor(ParentPresenter).Transform(originPoint).X;
                        Visual dataGridRow = ParentPresenter.TemplatedParent as Visual;
                        if (dataGridRow != null)
                        {
                            // cellsPanelOffset is the offset of DataGridCellsPresenter wrt DataGridRow
                            cellsPanelOffset = TransformToAncestor(dataGridRow).Transform(originPoint).X;
                        }
                    }
                    dataGridHorizontalScrollStartX = cellsPanelPresenterOffset;
                }

                nextFrozenCellStart = horizontalOffset;
                nextNonFrozenCellStart -= cellsPanelOffset;
                viewportStartX = horizontalOffset - cellsPanelOffset;
                frozenColumnCount = ParentDataGrid.FrozenColumnCount;
            }

            /*
             * Arrange the children
             */
            for (int i = 0, count = children.Count; i < count; ++i)
            {
                UIElement child = GetChildAtDisplayIndex(children, i);
                IProvideDataGridColumn cell = child as IProvideDataGridColumn;

                //Determine if this child was clipped in last arrange for the sake of frozen columns
                if (child == _clippedChildForFrozenBehaviour)
                {
                    oldClippedChild = child;
                    _clippedChildForFrozenBehaviour = null;
                }

                //Width determinition of the child to be arranged
                if (cell != null)
                {
                    childSize = cell.Column.ActualWidth;
                }
                else
                {
                    // Allocate the already allocated space
                    childSize = child.DesiredSize.Width;
                }
                rcChild.Width = childSize;

                //Determinition of start point for children to arrange. Lets say the there are 5 columns of which 2 are frozen.
                //If the datagrid is scrolled horizontally. Following is the snapshot of arrange
                /*
                        *                                                                                                    *
                        *| <Cell3> | <Unarranged space> | <RowHeader> | <Cell1> | <Cell2> | <Right Clip of Cell4> | <Cell5> |*
                        *                               |                        <Visible region>                           |*
                 */
                if (i < frozenColumnCount)
                {
                    //For all the frozen children start from the horizontal offset
                    //and arrange increamentally
                    rcChild.X = nextFrozenCellStart;
                    nextFrozenCellStart += childSize;
                    dataGridHorizontalScrollStartX += childSize;
                }
                else
                {
                    //For arranging non frozen children arrange which ever can be arranged
                    //from the start to horizontal offset. This would fill out the space left by
                    //frozen children. The next one child will be arranged and clipped accordingly past frozen 
                    //children. The remaining children will arranged in the remaining space.
                    if (DoubleUtil.LessThanOrClose(nextNonFrozenCellStart, viewportStartX))
                    {
                        if (DoubleUtil.LessThanOrClose(nextNonFrozenCellStart + childSize, viewportStartX))
                        {
                            rcChild.X = nextNonFrozenCellStart;
                            nextNonFrozenCellStart += childSize;
                        }
                        else
                        {
                            double cellChoppedWidth = viewportStartX - nextNonFrozenCellStart;
                            if (DoubleUtil.AreClose(cellChoppedWidth, 0.0))
                            {
                                rcChild.X = nextFrozenCellStart;
                                nextNonFrozenCellStart = nextFrozenCellStart + childSize;
                            }
                            else
                            {
                                rcChild.X = nextFrozenCellStart - cellChoppedWidth;
                                double clipWidth = childSize - cellChoppedWidth;
                                newClippedChild = child;
                                _childClipForFrozenBehavior.Rect = new Rect(cellChoppedWidth, 0, clipWidth, rcChild.Height);
                                nextNonFrozenCellStart = nextFrozenCellStart + clipWidth;
                            }
                        }
                    }
                    else
                    {
                        rcChild.X = nextNonFrozenCellStart;
                        nextNonFrozenCellStart += childSize;
                    }

                }

                child.Arrange(rcChild);
            }

            //Update the NonFrozenColumnsViewportHorizontalOffset property of datagrid
            if (ParentDataGrid != null)
            {
                ParentDataGrid.NonFrozenColumnsViewportHorizontalOffset = dataGridHorizontalScrollStartX;
            }

            //Remove the clip on previous clipped child
            if (oldClippedChild != null)
            {
                oldClippedChild.CoerceValue(ClipProperty);
            }

            //Add the clip on new child to be clipped for the sake of frozen columns.
            _clippedChildForFrozenBehaviour = newClippedChild;
            if (newClippedChild != null)
            {
                newClippedChild.CoerceValue(ClipProperty);
            }

            return arrangeSize;
        }

        #endregion


        #region Frozen Columns

        /// <summary>
        /// Method which returns the clip for the child which overlaps with frozen column
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        internal Geometry GetFrozenClipForChild(UIElement child)
        {
            if (child == _clippedChildForFrozenBehaviour)
            {
                return _childClipForFrozenBehavior;
            }
            return null;
        }

        #endregion


        #region Helpers

        /// <summary>
        ///     Returns the child of this panel at the given DisplayIndex.  
        /// </summary>
        /// <returns></returns>
        private UIElement GetChildAtDisplayIndex(UIElementCollection children, int index)
        {
            DataGrid parentDataGrid = ParentDataGrid;
            Debug.Assert(parentDataGrid == null || parentDataGrid.DisplayIndexMap.Count == children.Count, "DisplayIndexMap should have exactly one entry per column");

            if (parentDataGrid != null)
            {
                // treat index as a DisplayIndex
                index = parentDataGrid.ColumnIndexFromDisplayIndex(index);
            }

            return (UIElement)children[index];     
        }

        /// <summary>
        ///     Returns the columns on the parent DataGrid.
        /// </summary>
        private ObservableCollection<DataGridColumn> Columns
        {
            get
            {
                DataGrid parentDataGrid = ParentDataGrid;
                if (parentDataGrid != null)
                {
                    return parentDataGrid.Columns;
                }

                return null;
            }
        }


        /// <summary>
        ///     The row that this panel presents belongs to the DataGrid returned from this property.
        /// </summary>
        private DataGrid ParentDataGrid
        {
            get
            {
                if (_parentDataGrid == null)
                {
                    DataGridCellsPresenter presenter = ParentPresenter as DataGridCellsPresenter;

                    if (presenter != null)
                    {
                        DataGridRow row = presenter.DataGridRowOwner;

                        if (row != null)
                        {
                            _parentDataGrid = row.DataGridOwner;
                        }
                    }
                    else
                    {
                        DataGridColumnHeadersPresenter headersPresenter = ParentPresenter as DataGridColumnHeadersPresenter;

                        if (headersPresenter != null)
                        {
                            _parentDataGrid = headersPresenter.ParentDataGrid;
                        }
                    }
                }

                return _parentDataGrid;
            }
        }


        private ItemsControl ParentPresenter
        {
            get        
            {
                FrameworkElement itemsPresenter = TemplatedParent as FrameworkElement;
                if (itemsPresenter != null)
                {
                    return itemsPresenter.TemplatedParent as ItemsControl;
                }

                return null;
            }
        }


        #endregion 


        #region Data

        DataGrid _parentDataGrid;

        UIElement _clippedChildForFrozenBehaviour = null;
        RectangleGeometry _childClipForFrozenBehavior = new RectangleGeometry();

        #endregion 
    }
}