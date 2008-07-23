//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Windows.Controls
{
    /// <summary>
    /// This structure encapsulate the cell information necessary when clipboard content is prepared
    /// </summary>
    public struct DataGridClipboardCellContent
    {
        /// <summary>
        /// Creates a new DataGridClipboardCellValue structure containing information about DataGrid cell
        /// </summary>
        /// <param name="row">DataGrid row item containing the cell</param>
        /// <param name="column">DataGridColumn containing the cell</param>
        /// <param name="value">DataGrid cell value</param>
        public DataGridClipboardCellContent(object item, DataGridColumn column, object content)
        {
            _item = item;
            _column = column;
            _content = content;
        }

        /// <summary>
        /// DataGrid row item containing the cell
        /// </summary>
        public object Item
        {
            get { return _item; }
        }

        /// <summary>
        /// DataGridColumn containing the cell
        /// </summary>
        public DataGridColumn Column
        {
            get { return _column; }
        }

        /// <summary>
        /// Cell content
        /// </summary>
        public object Content
        {
            get { return _content; }
        }

        private object _item;
        private DataGridColumn _column;
        private object _content;
    }
}
