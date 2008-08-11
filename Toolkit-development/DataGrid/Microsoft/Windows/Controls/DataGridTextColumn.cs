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
using System.Windows.Media;
using System.Windows.Input;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    ///     A column that displays editable text.
    /// </summary>
    public class DataGridTextColumn : DataGridBoundColumn
    {
        static DataGridTextColumn()
        {
            ElementStyleProperty.OverrideMetadata(typeof(DataGridTextColumn), new FrameworkPropertyMetadata(DefaultElementStyle));
            EditingElementStyleProperty.OverrideMetadata(typeof(DataGridTextColumn), new FrameworkPropertyMetadata(DefaultEditingElementStyle));
        }

        #region Styles

        /// <summary>
        ///     The default value of the ElementStyle property.
        ///     This value can be used as the BasedOn for new styles.
        /// </summary>
        public static Style DefaultElementStyle
        {
            get
            {
                if (_defaultElementStyle == null)
                {
                    Style style = new Style(typeof(TextBlock));

                    // Use the same margin used on the TextBox to provide space for the caret
                    style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(2.0, 0.0, 2.0, 0.0)));

                    style.Seal();
                    _defaultElementStyle = style;
                }

                return _defaultElementStyle;
            }
        }

        /// <summary>
        ///     The default value of the EditingElementStyle property.
        ///     This value can be used as the BasedOn for new styles.
        /// </summary>
        public static Style DefaultEditingElementStyle
        {
            get
            {
                if (_defaultEditingElementStyle == null)
                {
                    Style style = new Style(typeof(TextBox));

                    style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0.0)));
                    style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(0.0)));

                    style.Seal();
                    _defaultEditingElementStyle = style;
                }

                return _defaultEditingElementStyle;
            }
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
            TextBox textBox = new TextBox();

            ApplyStyle(/* isEditing = */ true, /* defaultToElementStyle = */ false, textBox);
            ApplyDataFieldBinding(textBox, TextBox.TextProperty);

            return textBox;
        }

        #endregion

        #region Editing

        /// <summary>
        ///     Called when a cell has just switched to edit mode.
        /// </summary>
        /// <param name="editingElement">A reference to element returned by GenerateEditingElement.</param>
        /// <param name="editingEventArgs">The event args of the input event that caused the cell to go into edit mode. May be null.</param>
        /// <returns>The unedited value of the cell.</returns>
        protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
        {
            TextBox textBox = editingElement as TextBox;
            if (textBox != null)
            {
                textBox.Focus();

                string originalValue = textBox.Text;

                TextCompositionEventArgs textArgs = editingEventArgs as TextCompositionEventArgs;
                if (textArgs != null)
                {
                    // If text input started the edit, then replace the text with what was typed.
                    string inputText = textArgs.Text;
                    textBox.Text = inputText;
                    
                    // Place the caret after the end of the text.
                    textBox.Select(inputText.Length, 0);
                }
                else
                {
                    // If a mouse click started the edit, then place the caret under the mouse.
                    MouseButtonEventArgs mouseArgs = editingEventArgs as MouseButtonEventArgs;
                    if ((mouseArgs == null) || !PlaceCaretOnTextBox(textBox, Mouse.GetPosition(textBox)))
                    {
                        // If the mouse isn't over the textbox or something else started the edit, then select the text.
                        textBox.SelectAll();
                    }
                }

                return originalValue;
            }

            return null;
        }

        private bool PlaceCaretOnTextBox(TextBox textBox, Point position)
        {
            int characterIndex = textBox.GetCharacterIndexFromPoint(position, /* snapToText = */ false);
            if (characterIndex >= 0)
            {
                textBox.Select(characterIndex, 0);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Called when a cell's value is to be committed, just before it exits edit mode.
        /// </summary>
        /// <param name="editingElement">A reference to element returned by GenerateEditingElement.</param>
        protected override void CommitCellEdit(FrameworkElement editingElement)
        {
            TextBox textBox = editingElement as TextBox;
            if (textBox != null)
            {
                UpdateSource(textBox, TextBox.TextProperty);
            }
        }

        internal override void OnInput(InputEventArgs e)
        {
            // Text input will start an edit
            if (e is TextCompositionEventArgs)
            {
                BeginEdit(e);
            }
        }

        #endregion

        #region Data

        private static Style _defaultElementStyle;
        private static Style _defaultEditingElementStyle;
        
        #endregion
    }
}