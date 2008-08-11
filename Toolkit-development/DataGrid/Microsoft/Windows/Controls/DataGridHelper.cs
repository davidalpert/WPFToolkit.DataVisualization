//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MS.Internal;
using System.Collections.Generic;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     Helper code for DataGrid.
    /// </summary>
    internal static class DataGridHelper
    {
        #region GridLines

        //
        // Common code for drawing GridLines.  Shared by DataGridDetailsPresenter, DataGridCellsPresenter, and Cell
        //

        /// <summary>
        ///     Returns a size based on the given one with the given double subtracted out from the Width or Height.
        ///     Used to adjust for the thickness of grid lines.
        /// </summary>
        public static Size SubtractFromSize(Size size, double thickness, bool height)
        {
            if (height)
            {
                return new Size(size.Width, Math.Max(0.0, size.Height - thickness));
            }
            else
            {
                return new Size(Math.Max(0.0, size.Width - thickness), size.Height);
            }
        }

        /// <summary>
        ///     Test if either the vertical or horizontal gridlines are visible.
        /// </summary>
        public static bool IsGridLineVisible(DataGrid dataGrid, bool isHorizontal)
        {
            if (dataGrid != null)
            {
                DataGridGridLinesVisibility visibility = dataGrid.GridLinesVisibility;

                switch (visibility)
                {
                    case DataGridGridLinesVisibility.All:
                        return true;
                    case DataGridGridLinesVisibility.Horizontal:
                        return isHorizontal;
                    case DataGridGridLinesVisibility.None:
                        return false;
                    case DataGridGridLinesVisibility.Vertical:
                        return !isHorizontal;
                }
            }

            return false;
        }

        #endregion 

        #region Notification Propagation


        public static bool ShouldNotifyCells(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.Cells);
        }

        public static bool ShouldNotifyCellsPresenter(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.CellsPresenter);
        }

        public static bool ShouldNotifyColumns(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.Columns);
        }

        public static bool ShouldNotifyColumnHeaders(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.ColumnHeaders);
        }

        public static bool ShouldNotifyColumnHeadersPresenter(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.ColumnHeadersPresenter);
        }

        public static bool ShouldNotifyColumnCollection(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.ColumnCollection);
        }

        public static bool ShouldNotifyDataGrid(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.DataGrid);
        }

        // TODO: we have no properties that notify the DetailsPresenter right now.
#if NotifyDetailsPresenter
        public static bool ShouldNotifyDetailsPresenter(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.DetailsPresenter);
        }
#endif

        public static bool ShouldRefreshCellContent(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.RefreshCellContent);
        }

        public static bool ShouldNotifyRowHeaders(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.RowHeaders);
        }

        public static bool ShouldNotifyRows(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.Rows);
        }

        public static bool ShouldNotifyRowSubtree(NotificationTarget target)
        {
            return TestTarget(target, NotificationTarget.Rows |
                                      NotificationTarget.RowHeaders |
                                      NotificationTarget.CellsPresenter |
                                      NotificationTarget.Cells |
                                      NotificationTarget.RefreshCellContent |
                                      NotificationTarget.DetailsPresenter);
        }

        private static bool TestTarget(NotificationTarget target, NotificationTarget value)
        {
            return (target & value) != 0; 
        }

        #endregion 

        #region Tree Helpers


        /// <summary>
        /// Walks up the templated parent tree looking for a parent type.
        /// </summary>
        public static T FindParent<T>(FrameworkElement element) where T : FrameworkElement
        {
            FrameworkElement parent = element.TemplatedParent as FrameworkElement;

            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }

                parent = parent.TemplatedParent as FrameworkElement;
            }

            return null;
        }

        public static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }

                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }

            return null;
        }

        #endregion

        #region Cells Panel Invalidation

        /// <summary>
        ///     Invalidates a cell's panel if its column's width changes sufficiently.
        /// </summary>
        /// <param name="provideColumn">The cell or header.</param>
        public static void OnColumnWidthChanged(IProvideDataGridColumn cell, bool detectActualWidthChanges)
        {
            Debug.Assert((cell is DataGridCell) || (cell is DataGridColumnHeader),
                "provideColumn should be one of the cell or header containers.");

            UIElement element = (UIElement)cell;
            DataGridColumn column = cell.Column;

            if ((column != null) && 
                !(detectActualWidthChanges && DoubleUtil.AreClose(column.ActualWidth, element.RenderSize.Width)))
            {
                // The width changed enough to invaliate the parent DataGridCellsPanel.
                DataGridCellsPanel panel = VisualTreeHelper.GetParent(element) as DataGridCellsPanel;
                if (panel != null)
                {
                    // The parent panel (and not the cell) is invalidated since 
                    // it is the one that is communicating with the 
                    // column regarding width data.
                    panel.InvalidateMeasure();
                    panel.InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Helper method which invalidates the arrange of cells panel for a given cell
        /// </summary>
        /// <param name="cell"></param>
        public static void InvalidateCellsPanelArrange(IProvideDataGridColumn cell)
        {
            DataGridCellsPanel panel = GetParentPanelForCell(cell);
            if (panel != null)
            {
                panel.InvalidateArrange();
            }
        }

        /// <summary>
        /// Helper method which returns the clip for the cell based on whether it overlaps with frozen columns or not
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        public static Geometry GetFrozenClipForCell(IProvideDataGridColumn cell)
        {
            DataGridCellsPanel panel = GetParentPanelForCell(cell);
            if (panel != null)
            {
                return panel.GetFrozenClipForChild((UIElement)cell);
            }
            return null;
        }

        /// <summary>
        /// Helper method which returns the parent DataGridCellsPanel for a cell
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        public static DataGridCellsPanel GetParentPanelForCell(IProvideDataGridColumn cell)
        {
            Debug.Assert((cell is DataGridCell) || (cell is DataGridColumnHeader),
                "provideColumn should be one of the cell or header containers.");

            UIElement element = (UIElement)cell;
            return (VisualTreeHelper.GetParent(element) as DataGridCellsPanel);
        }

        #endregion

        #region Property Helpers

        public static bool IsDefaultValue(DependencyObject d, DependencyProperty dp)
        {
            return DependencyPropertyHelper.GetValueSource(d, dp).BaseValueSource == BaseValueSource.Default;
        }

        /// <summary>
        ///     Computes the value of a given property based on the DataGrid property transfer rules.
        /// </summary>
        /// <remarks>
        ///     This is intended to be called from within the coercion of the baseProperty.
        /// </remarks>
        /// <param name="baseObject">The target object which recieves the transferred property</param>
        /// <param name="baseValue">The baseValue that was passed into the coercion delegate</param>
        /// <param name="baseProperty">The property that is being coerced</param>
        /// <param name="parentObject">The object that contains the parentProperty</param>
        /// <param name="parentProperty">A property who's value should be transfered (via coercion) to the baseObject if it has a higher precedence.</param>
        /// <param name="grandParentObject">Same as parentObject but evaluated at a lower presedece for a given BaseValueSource</param>
        /// <param name="grandParentProperty">Same as parentProperty but evaluated at a lower presedece for a given BaseValueSource</param>
        /// <returns></returns>
        public static object GetCoercedTransferPropertyValue(DependencyObject baseObject, object baseValue, DependencyProperty baseProperty,
                                                             DependencyObject parentObject, DependencyProperty parentProperty,
                                                             DependencyObject grandParentObject, DependencyProperty grandParentProperty)
        {
            //
            // Transfer Property Coercion rules:
            //
            // Determine if this is a 'Transfer Property Coercion'.  If so:
            //   We can safely get the BaseValueSource because the property change originated from another
            //   property, and thus this BaseValueSource wont be stale.
            //   Pick a value to use based on who has the greatest BaseValueSource
            // If not a 'Transfer Property Coercion', simply return baseValue.  This will cause a property change if the value changes, which
            // will trigger a 'Transfer Property Coercion', and we will no longer have a stale BaseValueSource
            //
            var coercedValue = baseValue;

            if (IsPropertyTransferEnabled(baseObject, baseProperty))
            {
                var propertySource = DependencyPropertyHelper.GetValueSource(baseObject, baseProperty);
                var maxBaseValueSource = propertySource.BaseValueSource;

                if (parentObject != null)
                {
                    var parentPropertySource = DependencyPropertyHelper.GetValueSource(parentObject, parentProperty);

                    if (parentPropertySource.BaseValueSource > maxBaseValueSource)
                    {
                        coercedValue = parentObject.GetValue(parentProperty);
                        maxBaseValueSource = parentPropertySource.BaseValueSource;
                    }
                }

                if (grandParentObject != null)
                {
                    var grandParentPropertySource = DependencyPropertyHelper.GetValueSource(grandParentObject, grandParentProperty);

                    if (grandParentPropertySource.BaseValueSource > maxBaseValueSource)
                    {
                        coercedValue = grandParentObject.GetValue(grandParentProperty);
                        maxBaseValueSource = grandParentPropertySource.BaseValueSource;
                    }
                }
            }

            return coercedValue;
        }

        /// <summary>
        ///     Causes the given DependencyProperty to be coerced in transfer mode.
        /// </summary>
        /// <remarks>
        ///     This should be called from within the target object's NotifyPropertyChanged.  It MUST be called in
        ///     response to a change in the target property.
        /// </remarks>
        /// <param name="d">The DependencyObject which contains the property that needs to be transfered.</param>
        /// <param name="p">The DependencyProperty that is the target of the property transfer.</param>
        public static void TransferProperty(DependencyObject d, DependencyProperty p)
        {
            var transferEnabledMap = GetPropertyTransferEnabledMapForObject(d, p);
            transferEnabledMap[p] = true;
            d.CoerceValue(p);
            transferEnabledMap[p] = false;
        }

        private static Dictionary<DependencyProperty, bool> GetPropertyTransferEnabledMapForObject(DependencyObject d, DependencyProperty p)
        {
            var propertyTransferEnabledForObject = _propertyTransferEnabledMap[d] as Dictionary<DependencyProperty, bool>;

            if (propertyTransferEnabledForObject == null)
            {
                propertyTransferEnabledForObject = new Dictionary<DependencyProperty, bool>();
                _propertyTransferEnabledMap.SetWeak(d, propertyTransferEnabledForObject);
            }

            return propertyTransferEnabledForObject;
        }

        private static bool IsPropertyTransferEnabled(DependencyObject d, DependencyProperty p)
        {
            var propertyTransferEnabledForObject = _propertyTransferEnabledMap[d] as Dictionary<DependencyProperty, bool>;

            if (propertyTransferEnabledForObject != null)
            {
                bool isPropertyTransferEnabled;
                if (propertyTransferEnabledForObject.TryGetValue(p, out isPropertyTransferEnabled))
                {
                    return isPropertyTransferEnabled;
                }
            }

            return false;
        }
        
        /// <summary>
        ///     Tracks which properties are currently being transfered.  This information is needed when GetPropertyTransferEnabledMapForObject
        ///     is called inside of Coersion.
        /// </summary>
        private static WeakHashtable _propertyTransferEnabledMap = new WeakHashtable();

        #endregion

        #region Input Gestures

        // Taken from KeyGesture.CreateFromResourceStrings
        internal static KeyGesture CreateFromResourceStrings(string keyGestureToken, string keyDisplayString)
        {
            // combine the gesture and the display string, producing a string
            // that the type converter will recognize
            if (!String.IsNullOrEmpty(keyDisplayString))
            {
                keyGestureToken += DISPLAYSTRING_SEPARATOR + keyDisplayString;
            }

            return _keyGestureConverter.ConvertFromInvariantString(keyGestureToken) as KeyGesture;
        }

        private const char DISPLAYSTRING_SEPARATOR = ',';
        private static TypeConverter _keyGestureConverter = new KeyGestureConverter();

        #endregion

        #region Theme

        /// <summary>
        ///     Will return the string version of the current theme name.
        ///     Will apply a resource reference to the element passed in.
        /// </summary>
        public static string GetTheme(FrameworkElement element)
        {
            object o = element.ReadLocalValue(ThemeProperty);
            if (o == DependencyProperty.UnsetValue)
            {
                element.SetResourceReference(ThemeProperty, _themeKey);
            }

            return (string)element.GetValue(ThemeProperty);
        }

        /// <summary>
        ///     Private property used to determine the theme name.
        /// </summary>
        private static readonly DependencyProperty ThemeProperty =
            DependencyProperty.RegisterAttached("Theme", typeof(string), typeof(DataGridHelper), new FrameworkPropertyMetadata(String.Empty));

        /// <summary>
        ///     The resource key used to fetch the theme name.
        /// </summary>
        private static ComponentResourceKey _themeKey = new ComponentResourceKey(typeof(DataGrid), "Theme");

        /// <summary>
        ///     Sets up a property change handler for the private theme property.
        ///     Use this to receive a theme change notification.
        ///     Requires calling GetTheme on an element of the given type at some point.
        /// </summary>
        public static void HookThemeChange(Type type, PropertyChangedCallback propertyChangedCallback)
        {
            ThemeProperty.OverrideMetadata(type, new FrameworkPropertyMetadata(String.Empty, propertyChangedCallback));
        }

        #endregion

        #region Star Width Helper

        /// <summary>
        /// Helper method which determines if star width computation is needed or not
        /// </summary>
        /// <param name="dataGrid"></param>
        /// <returns></returns>
        public static bool StarWidthComputationNeeded(DataGrid dataGrid)
        {
            Debug.Assert(dataGrid != null, "DataGrid is null");
            return ((DataGridColumnCollection)(dataGrid.Columns)).ComputeStarColumnWidths;
        }

        /// <summary>
        /// Helper method which invalidates the star width computation flag
        /// </summary>
        /// <param name="dataGrid"></param>
        public static void InvalidateStarWidthComputation(DataGrid dataGrid)
        {
            Debug.Assert(dataGrid != null, "DataGrid is null");
            ((DataGridColumnCollection)(dataGrid.Columns)).ComputeStarColumnWidths = true;
        }

        /// <summary>
        /// Helper method which computes the widths of all the star columns for a given datagrid
        /// </summary>
        /// <param name="dataGrid"></param>
        /// <param name="availableStarSpace"></param>
        public static void ComputeStarColumnWidths(DataGrid dataGrid, double availableStarSpace)
        {
            Debug.Assert(dataGrid != null, "DataGrid is null");
            Debug.Assert(!Double.IsNaN(availableStarSpace) && !Double.IsNegativeInfinity(availableStarSpace) && !Double.IsPositiveInfinity(availableStarSpace),
                "availableStarSpace is not valid");

            List<DataGridColumn> unResolvedColumns = new List<DataGridColumn>();
            List<DataGridColumn> partialResolvedColumns = new List<DataGridColumn>();
            double totalFactors = 0.0;

            //accumulate all the star columns into unResolvedColumns in the beginning
            foreach(DataGridColumn column in dataGrid.Columns)
            {
                DataGridLength width = column.Width;
                if(width.IsStar)
                {
                    unResolvedColumns.Add(column);
                    totalFactors += width.Value;
                }
            }

            while (unResolvedColumns.Count > 0)
            {
                //find all the columns whose star share is less than thier min width and move such columns
                //into partialResolvedColumns giving them atleast the minwidth and there by reducing the availableSpace and totalFactors
                for (int i = 0, count = unResolvedColumns.Count; i < count; i++)
                {
                    DataGridColumn column = unResolvedColumns[i];
                    DataGridLength width = column.Width;

                    double columnMinWidth = column.MinWidth;
                    double starColumnWidth = availableStarSpace * width.Value / totalFactors;

                    if (DoubleUtil.GreaterThan(columnMinWidth, starColumnWidth))
                    {
                        availableStarSpace = Math.Max(0.0, availableStarSpace - columnMinWidth);
                        totalFactors -= width.Value;
                        unResolvedColumns.RemoveAt(i);
                        i--;
                        count--;
                        partialResolvedColumns.Add(column);
                    }
                }

                //With the remaining space determine in any columns star share is more than maxwidth.
                //If such columns are found give them their max width and remove them from unResolvedColumns
                //there by reducing the availablespace and totalfactors. If such column is found, the remaining columns are to be recomputed
                bool iterationRequired = false;
                for (int i = 0, count = unResolvedColumns.Count; i < count; i++)
                {
                    DataGridColumn column = unResolvedColumns[i];
                    DataGridLength width = column.Width;

                    double columnMaxWidth = column.MaxWidth;
                    double starColumnWidth = availableStarSpace * width.Value / totalFactors;

                    if (DoubleUtil.LessThan(columnMaxWidth, starColumnWidth))
                    {
                        iterationRequired = true;
                        unResolvedColumns.RemoveAt(i);
                        availableStarSpace -= columnMaxWidth;
                        totalFactors -= width.Value;
                        column.UpdateActualWidth(columnMaxWidth);
                        break;
                    }
                }

                //If it was determined by the previous step that another iteration is needed
                //then move all the partialResolvedColumns back to unResolvedColumns and there by
                //restoring availablespace and totalfactors.
                //If another iteration is not needed then allocate min widths to all columns in 
                //partial resolved columns and star share to all unresolved columns there by
                //ending the loop
                if (iterationRequired)
                {
                    for (int i = 0, count = partialResolvedColumns.Count; i < count; i++)
                    {
                        DataGridColumn column = partialResolvedColumns[i];

                        unResolvedColumns.Add(column);
                        availableStarSpace += column.MinWidth;
                        totalFactors += column.Width.Value;
                    }
                    partialResolvedColumns.Clear();
                }
                else
                {
                    for (int i = 0, count = partialResolvedColumns.Count; i < count; i++)
                    {
                        DataGridColumn column = partialResolvedColumns[i];
                        column.UpdateActualWidth(column.MinWidth);
                    }
                    partialResolvedColumns.Clear();
                    for (int i = 0, count = unResolvedColumns.Count; i < count; i++)
                    {
                        DataGridColumn column = unResolvedColumns[i];
                        double starColumnWidth = availableStarSpace * column.Width.Value / totalFactors;
                        column.UpdateActualWidth(starColumnWidth);
                    }
                    unResolvedColumns.Clear();
                }
            }

            ((DataGridColumnCollection)(dataGrid.Columns)).ComputeStarColumnWidths = false;
        }

        #endregion
    }
}