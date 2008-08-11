//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Resources;
using System.Globalization;

namespace Microsoft.Windows.Controls
{

    // A wrapper around string identifiers.
    internal struct SRID
    {

           private string _string;
           public string String { get { return _string; } }
           private SRID (string s) { _string = s; }

           public static SRID DataGrid_SelectAllCommandText { get { return new SRID("DataGrid_SelectAllCommandText"); } }
           public static SRID DataGrid_SelectAllKey { get { return new SRID("DataGrid_SelectAllKey"); } }
           public static SRID DataGrid_SelectAllKeyDisplayString { get { return new SRID("DataGrid_SelectAllKeyDisplayString"); } }
           public static SRID DataGrid_BeginEditCommandText { get { return new SRID("DataGrid_BeginEditCommandText"); } }
           public static SRID DataGrid_CommitEditCommandText { get { return new SRID("DataGrid_CommitEditCommandText"); } }
           public static SRID DataGrid_CancelEditCommandText { get { return new SRID("DataGrid_CancelEditCommandText"); } }
           public static SRID DataGrid_DeleteCommandText { get { return new SRID("DataGrid_DeleteCommandText"); } }
           public static SRID DataGrid_ColumnDisplayIndexOutOfRange { get { return new SRID("DataGrid_ColumnDisplayIndexOutOfRange"); } }
           public static SRID DataGrid_DisplayIndexOutOfRange { get { return new SRID("DataGrid_DisplayIndexOutOfRange"); } }
           public static SRID DataGrid_DuplicateDisplayIndex { get { return new SRID("DataGrid_DuplicateDisplayIndex"); } }
           public static SRID DataGrid_NewColumnInvalidDisplayIndex { get { return new SRID("DataGrid_NewColumnInvalidDisplayIndex"); } }
           public static SRID DataGrid_NullColumn { get { return new SRID("DataGrid_NullColumn"); } }
           public static SRID DataGrid_ReadonlyCellsItemsSource { get { return new SRID("DataGrid_ReadonlyCellsItemsSource"); } }
           public static SRID DataGrid_InvalidSortDescription { get { return new SRID("DataGrid_InvalidSortDescription"); } }
           public static SRID DataGrid_ProbableInvalidSortDescription { get { return new SRID("DataGrid_ProbableInvalidSortDescription"); } }
           public static SRID DataGrid_CannotSelectCell { get { return new SRID("DataGrid_CannotSelectCell"); } }
           public static SRID DataGridRow_CannotSelectRowWhenCells { get { return new SRID("DataGridRow_CannotSelectRowWhenCells"); } }
           public static SRID SelectedCellsCollection_InvalidItem { get { return new SRID("SelectedCellsCollection_InvalidItem"); } }
           public static SRID SelectedCellsCollection_DuplicateItem { get { return new SRID("SelectedCellsCollection_DuplicateItem"); } }
           public static SRID VirtualizedCellInfoCollection_IsReadOnly { get { return new SRID("VirtualizedCellInfoCollection_IsReadOnly"); } }
           public static SRID VirtualizedCellInfoCollection_DoesNotSupportIndexChanges { get { return new SRID("VirtualizedCellInfoCollection_DoesNotSupportIndexChanges"); } }
           public static SRID ClipboardCopyMode_Disabled { get { return new SRID("ClipboardCopyMode_Disabled"); } }

    }
}
