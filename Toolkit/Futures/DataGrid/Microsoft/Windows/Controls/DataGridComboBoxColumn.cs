//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     A column that displays a drop-down list while in edit mode.
    /// </summary>
    public class DataGridComboBoxColumn : DataGridBoundColumn
    {

        #region ComboBox Column Properties 

        /// <summary>
        ///     The ComboBox will attach to this ItemsSource.
        /// </summary>
        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        /// <summary>
        ///     The DependencyProperty for ItemsSource.
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(DataGridComboBoxColumn), new UIPropertyMetadata(null));

        /// <summary>
        /// Property which specifies the selection type to be used by the combo box
        /// </summary>
        public ComboBoxDataFieldTarget DataFieldTarget
        {
            get { return (ComboBoxDataFieldTarget)GetValue(DataFieldTargetProperty); }
            set { SetValue(DataFieldTargetProperty, value); }
        }

        /// <summary>
        /// Dependency property for DataFieldTarget property
        /// </summary>
        public static readonly DependencyProperty DataFieldTargetProperty =
            DependencyProperty.Register("DataFieldTarget", 
                                        typeof(ComboBoxDataFieldTarget), 
                                        typeof(DataGridComboBoxColumn),
                                        new FrameworkPropertyMetadata(ComboBoxDataFieldTarget.SelectedItem, new PropertyChangedCallback(DataGridColumn.NotifyPropertyChangeForRefreshContent)));

        #endregion

        #region Property Changed Handler

        protected internal override void RefreshCellContent(FrameworkElement element, string propertyName)
        {
            DataGridCell cell = element as DataGridCell;
            if (cell != null &&
                string.Compare(propertyName, "DataFieldTarget") == 0 &&
                cell.IsEditing)
            {
                cell.BuildVisualTree();
            }
            else
            {
                base.RefreshCellContent(element, propertyName);
            }
        }

        #endregion

        #region DataFieldTarget Helpers

        /// <summary>
        /// Helper method which returns the ComboBox's DependencyProperty
        /// based on DataFieldTarget
        /// </summary>
        /// <returns></returns>
        private DependencyProperty GetPropertyForDataFieldBinding()
        {
            switch (DataFieldTarget)
            {
                case ComboBoxDataFieldTarget.SelectedItem:
                    return ComboBox.SelectedItemProperty;
                case ComboBoxDataFieldTarget.SelectedValue:
                    return ComboBox.SelectedValueProperty;
                case ComboBoxDataFieldTarget.Text:
                    return ComboBox.TextProperty;
            }
            return null;
        }

        /// <summary>
        /// Helper method which returns selection value from
        /// combobox based on DataFieldTarget
        /// </summary>
        /// <param name="comboBox"></param>
        /// <returns></returns>
        private object GetComboBoxSelectionValue(ComboBox comboBox)
        {
            switch (DataFieldTarget)
            {
                case ComboBoxDataFieldTarget.SelectedItem:
                    return comboBox.SelectedItem;
                case ComboBoxDataFieldTarget.SelectedValue:
                    return comboBox.SelectedValue;
                case ComboBoxDataFieldTarget.Text:
                    return comboBox.Text;
            }
            return null;
        }

        #endregion

        #region Element Generation

        /// <summary>
        ///     Creates the visual tree for text based cells.
        /// </summary>
        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            TextBlock textBlock = new TextBlock();

            ApplyStyle(/* isEditing = */ false, /* defaultToElementStyle = */ false, textBlock);
            ApplyDataFieldBinding(textBlock, TextBlock.TextProperty);

            return textBlock;
        }

        /// <summary>
        ///     Creates the visual tree for text based cells.
        /// </summary>
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            ComboBox comboBox = new ComboBox();

            ApplyStyle(/* isEditing = */ true, /* defaultToElementStyle = */ false, comboBox);
            ApplyDataFieldBinding(comboBox, GetPropertyForDataFieldBinding());

            // If there is already a non-default value (provided in EditingElementStyle),
            // then don't apply the column's ItemsSource.
            if (DataGridHelper.IsDefaultValue(comboBox, ComboBox.ItemsSourceProperty))
            {
                if (_itemsSourceBinding == null)
                {
                    _itemsSourceBinding = new Binding("ItemsSource");
                    _itemsSourceBinding.Source = this;
                }
                comboBox.SetBinding(ComboBox.ItemsSourceProperty, _itemsSourceBinding);
            }

            return comboBox;
        }

        #endregion

        #region Editing

        /// <summary>
        ///     Called when a cell has just switched to edit mode.
        /// </summary>
        /// <param name="editingElement">A reference to element returned by GenerateEditingElement.</param>
        /// <param name="e">The event args of the input event that caused the cell to go into edit mode. May be null.</param>
        /// <returns>The unedited value of the cell.</returns>
        protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs e)
        {
            ComboBox comboBox = editingElement as ComboBox;
            if (comboBox != null)
            {
                comboBox.Focus();
                object originalValue = GetComboBoxSelectionValue(comboBox);
                
                if (IsComboBoxOpeningInputEvent(e))
                {
                    comboBox.IsDropDownOpen = true;
                }

                return originalValue;
            }

            return null;
        }

        /// <summary>
        ///     Called when a cell's value is to be committed, just before it exits edit mode.
        /// </summary>
        /// <param name="editingElement">A reference to element returned by GenerateEditingElement.</param>
        protected override void CommitCellEdit(FrameworkElement editingElement)
        {
            ComboBox comboBox = editingElement as ComboBox;
            if (comboBox != null)
            {
                UpdateSource(comboBox, GetPropertyForDataFieldBinding());
            }
        }

        internal override void OnInput(InputEventArgs e)
        {
            if (IsComboBoxOpeningInputEvent(e))
            {
                BeginEdit(e);
            }
        }

        private static bool IsComboBoxOpeningInputEvent(RoutedEventArgs e)
        {
            KeyEventArgs keyArgs = e as KeyEventArgs;
            if ((keyArgs != null) && ((keyArgs.KeyStates & KeyStates.Down) == KeyStates.Down))
            {
                bool isAltDown = (keyArgs.KeyboardDevice.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

                // We want to handle the ALT key. Get the real key if it is Key.System.
                Key key = keyArgs.Key;
                if (key == Key.System)
                {
                    key = keyArgs.SystemKey;
                }

                // F4 alone or ALT+Up or ALT+Down will open the drop-down
                return (((key == Key.F4) && !isAltDown) ||
                        (((key == Key.Up) || (key == Key.Down)) && isAltDown));
            }

            return false;
        }

        #endregion

        #region Data

        private Binding _itemsSourceBinding;

        #endregion
    }
}