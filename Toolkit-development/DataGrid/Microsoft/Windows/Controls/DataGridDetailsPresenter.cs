//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Microsoft.Windows.Controls
{
    // TODO: Hook up the content.
    // TODO: OnApplyTemplate isn't getting called yet.  Once we hook up the content ensure this happens in the common case
    //       or find another way to hook up the details presenter with the Row.

    public class DataGridDetailsPresenter : ContentPresenter
    {
        static DataGridDetailsPresenter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DataGridDetailsPresenter), new FrameworkPropertyMetadata(typeof(DataGridDetailsPresenter)));
        }

        #region Row Communication

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // TODO: we have no properties that send down notifications to the DetailsPresenter yet
#if NotifyDetailsPresenter
            //  Give the Row a pointer to the RowHeader so that it can propagate down change notifications
            DataGridRow parent = ParentRow;

            if (parent != null)
            {
                parent.DetailsPresenter = this;

            }
#endif
        }

        /// <summary>
        ///     Update all properties that get a value from the DataGrid
        /// </summary>
        /// <remarks>
        ///     See comment on DataGridRow.OnDataGridChanged
        /// </remarks>
        internal void SyncProperties()
        {
            // TODO: we have no properties that send down notifications to the DetailsPresenter yet
        }

        #endregion

#if NotifyDetailsPresenter
        #region Notification Propagation

        internal void NotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        #endregion 
#endif

        #region GridLines

        //
        // Different parts of the DataGrid draw different pieces of the GridLines.
        // Rows draw a single horizontal line on the bottom.  The DataGridDetailsPresenter is the element that handles it.
        //

        /// <summary>
        ///     Measure.  This is overridden so that the row can extend its size to account for a grid line on the bottom.
        /// </summary>
        /// <param name="availableSize"></param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            //
            // Make space for the GridLine on the bottom.
            // Remove space from the constraint (since it implicitly includes the GridLine's thickness), 
            // call the base implementation, and add the thickness back for the returned size.
            //

            DataGrid dataGrid = DataGridOwner;

            if (ParentRow.DetailsPresenterDrawsGridLines && 
                DataGridHelper.IsGridLineVisible(dataGrid, /*isHorizontal = */ true))
            {
                double thickness = dataGrid.HorizontalGridLineThickness;
                Size desiredSize = base.MeasureOverride(DataGridHelper.SubtractFromSize(availableSize, thickness, /*height = */ true));
                desiredSize.Height += thickness;
                return desiredSize;
            }
            else
            {
                return base.MeasureOverride(availableSize);
            }
        }

        /// <summary>
        ///     Arrange.  This is overriden so that the row can position its content to account for a grid line on the bottom.
        /// </summary>
        /// <param name="finalSize">Arrange size</param>
        protected override Size ArrangeOverride(Size finalSize)
        {
            //
            // We don't need to adjust the Arrange position of the content.  By default it is arranged at 0,0 and we're
            // adding a line to the bottom.  All we have to do is compress and extend the size, just like Measure.
            //

            DataGrid dataGrid = DataGridOwner;

            if (ParentRow.DetailsPresenterDrawsGridLines &&
                DataGridHelper.IsGridLineVisible(dataGrid, /*isHorizontal = */ true))
            {
                double thickness = dataGrid.HorizontalGridLineThickness;
                Size returnSize = base.ArrangeOverride(DataGridHelper.SubtractFromSize(finalSize, thickness, /*height = */ true));
                returnSize.Height += thickness;
                return returnSize;
            }
            else
            {
                return base.ArrangeOverride(finalSize);
            }
        }

        /// <summary>
        ///     OnRender.  Overriden to draw a horizontal line underneath the content.
        /// </summary>
        /// <param name="drawingContext"></param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            DataGrid dataGrid = DataGridOwner;

            base.OnRender(drawingContext);

            if (ParentRow.DetailsPresenterDrawsGridLines &&
                DataGridHelper.IsGridLineVisible(dataGrid, /*isHorizontal = */ true))
            {
                double thickness = dataGrid.HorizontalGridLineThickness;
                Rect rect = new Rect(new Size(RenderSize.Width, thickness));
                rect.Y = RenderSize.Height - thickness;

                drawingContext.DrawRectangle(dataGrid.HorizontalGridLinesBrush, null, rect);
            }
        }

        #endregion

        #region Helpers

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

        private DataGridRow ParentRow
        {
            get
            {
                return DataGridHelper.FindParent<DataGridRow>(this);
            }
        }

        #endregion
    }
}