//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     A base class for specifying column definitions for certain standard
    ///     types that do not allow arbitrary templates.
    /// </summary>
    public abstract class DataGridBoundColumn : DataGridColumn
    {
        #region Binding

        /// <summary>
        ///     The binding that will be applied to the generated element.
        /// </summary>
        public virtual BindingBase DataFieldBinding
        {
            get { return _dataFieldBinding; }
            set
            {
                if (_dataFieldBinding != value)
                {
                    BindingBase oldBinding = _dataFieldBinding;
                    _dataFieldBinding = value;
                    EnsureTwoWay(_dataFieldBinding);
                    OnDataFieldBindingChanged(oldBinding, _dataFieldBinding);
                }
            }
        }

        // TODO: Remove when binding groups are used (post Beta)
        private static void TEMP_UpdateSourceWorkaround(Binding b)
        {
            b.UpdateSourceTrigger = UpdateSourceTrigger.Explicit;
        }
        private static void TEMP_UpdateSourceWorkaround(MultiBinding b)
        {
            b.UpdateSourceTrigger = UpdateSourceTrigger.Explicit;
        }

        private static void EnsureTwoWay(BindingBase bindingBase)
        {
            // If it is a standard Binding, then set the mode to TwoWay
            Binding binding = bindingBase as Binding;
            if (binding != null)
            {
                if (binding.Mode != BindingMode.TwoWay)
                {
                    binding.Mode = BindingMode.TwoWay;
                    TEMP_UpdateSourceWorkaround(binding);
                }
                return;
            }

            // A multi-binding can be set to TwoWay as well
            MultiBinding multiBinding = bindingBase as MultiBinding;
            if (multiBinding != null)
            {
                if (multiBinding.Mode != BindingMode.TwoWay)
                {
                    multiBinding.Mode = BindingMode.TwoWay;
                    TEMP_UpdateSourceWorkaround(multiBinding);
                }
                return;
            }

            // A priority binding is a list of bindings, each should be set to TwoWay
            PriorityBinding priBinding = bindingBase as PriorityBinding;
            if (priBinding != null)
            {
                Collection<BindingBase> subBindings = priBinding.Bindings;
                int count = subBindings.Count;
                for (int i = 0; i < count; i++)
                {
                    EnsureTwoWay(subBindings[i]);
                }
            }
        }

        /// <summary>
        ///     Called when DataFieldBinding changes.
        /// </summary>
        /// <remarks>
        ///     Default implementation notifies the DataGrid and its subtree about the change.
        /// </remarks>
        /// <param name="oldBinding">The old binding.</param>
        /// <param name="newBinding">The new binding.</param>
        protected virtual void OnDataFieldBindingChanged(BindingBase oldBinding, BindingBase newBinding)
        {
            NotifyPropertyChanged("DataFieldBinding");
        }

        /// <summary>
        ///     Assigns the DataFieldBinding to the desired property on the target object.
        /// </summary>
        internal void ApplyDataFieldBinding(DependencyObject target, DependencyProperty property)
        {
            BindingBase binding = DataFieldBinding;
            if (binding != null)
            {
                BindingOperations.SetBinding(target, property, binding);
            }
            else
            {
                BindingOperations.ClearBinding(target, property);
            }
        }

        #endregion

        #region Styling

        /// <summary>
        ///     A style that is applied to the generated element when not editing.
        ///     The TargetType of the style depends on the derived column class.
        /// </summary>
        public Style ElementStyle
        {
            get { return (Style)GetValue(ElementStyleProperty); }
            set { SetValue(ElementStyleProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the ElementStyle property.
        /// </summary>
        public static readonly DependencyProperty ElementStyleProperty =
            DependencyProperty.Register("ElementStyle", 
                                        typeof(Style), 
                                        typeof(DataGridBoundColumn),
                                        new FrameworkPropertyMetadata(null, new PropertyChangedCallback(DataGridColumn.NotifyPropertyChangeForRefreshContent)));

        /// <summary>
        ///     A style that is applied to the generated element when editing.
        ///     The TargetType of the style depends on the derived column class.
        /// </summary>
        public Style EditingElementStyle
        {
            get { return (Style)GetValue(EditingElementStyleProperty); }
            set { SetValue(EditingElementStyleProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for the EditingElementStyle property.
        /// </summary>
        public static readonly DependencyProperty EditingElementStyleProperty =
            DependencyProperty.Register("EditingElementStyle", 
                                        typeof(Style), 
                                        typeof(DataGridBoundColumn),
                                        new FrameworkPropertyMetadata(null, new PropertyChangedCallback(DataGridColumn.NotifyPropertyChangeForRefreshContent)));

        /// <summary>
        ///     Assigns the ElementStyle to the desired property on the given element.
        /// </summary>
        internal void ApplyStyle(bool isEditing, bool defaultToElementStyle, FrameworkElement element)
        {
            Style style = PickStyle(isEditing, defaultToElementStyle);
            if (style != null)
            {
                element.Style = style;
            }
        }

        /// <summary>
        ///     Assigns the ElementStyle to the desired property on the given element.
        /// </summary>
        internal void ApplyStyle(bool isEditing, bool defaultToElementStyle, FrameworkContentElement element)
        {
            Style style = PickStyle(isEditing, defaultToElementStyle);
            if (style != null)
            {
                element.Style = style;
            }
        }

        private Style PickStyle(bool isEditing, bool defaultToElementStyle)
        {
            Style style = isEditing ? EditingElementStyle : ElementStyle;
            if (isEditing && defaultToElementStyle && (style == null))
            {
                style = ElementStyle;
            }

            return style;
        }

        #endregion

        #region Editing

        internal void UpdateSource(FrameworkElement element, DependencyProperty dp)
        {
            BindingExpression binding = DataGridBoundColumn.GetBindingExpression(element, dp);
            if (binding != null)
            {
                binding.UpdateSource();
            }
        }

        internal void UpdateTarget(FrameworkElement element, DependencyProperty dp)
        {
            BindingExpression binding = DataGridBoundColumn.GetBindingExpression(element, dp);
            if (binding != null)
            {
                binding.UpdateTarget();
            }
        }

        private static BindingExpression GetBindingExpression(FrameworkElement element, DependencyProperty dp)
        {
            if (element != null)
            {
                return element.GetBindingExpression(dp);
            }

            return null;
        }

        #endregion

        #region Clipboard Copy/Paste

        /// <summary>
        /// If base ClipboardContentBinding is not set we use DataFieldBinding.
        /// </summary>
        public override BindingBase ClipboardContentBinding
        {
            get
            {
                return base.ClipboardContentBinding ?? DataFieldBinding;
            }
            set
            {
                base.ClipboardContentBinding = value;
            }
        }

        #endregion

        #region Property Changed Handler

        /// <summary>
        /// Override which rebuilds the cell's visual tree for DataFieldBinding change
        /// </summary>
        /// <param name="element"></param>
        /// <param name="propertyName"></param>
        protected internal override void RefreshCellContent(FrameworkElement element, string propertyName)
        {
            DataGridCell cell = element as DataGridCell;
            if (cell != null)
            {
                bool isCellEditing = cell.IsEditing;
                if ((string.Compare(propertyName, "DataFieldBinding") == 0) ||
                    (string.Compare(propertyName, "ElementStyle") == 0 && !isCellEditing) ||
                    (string.Compare(propertyName, "EditingElementStyle") == 0 && isCellEditing))
                {
                    cell.BuildVisualTree();
                    return;
                }
            }
            base.RefreshCellContent(element, propertyName);
        }

        #endregion

        #region Data

        private BindingBase _dataFieldBinding;

        #endregion

    }
}