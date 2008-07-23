﻿//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;
using System.Windows;

namespace Microsoft.Windows.Controls
{
    internal static class ClipboardHelper
    {

        internal static void FormatCell(object cellValue, bool firstCell, bool lastCell, StringBuilder sb, string format)
        {
            bool csv = string.Equals(format, DataFormats.CommaSeparatedValue, StringComparison.OrdinalIgnoreCase);
            if (csv || string.Equals(format, DataFormats.Text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(format, DataFormats.UnicodeText, StringComparison.OrdinalIgnoreCase))
            {
                if (cellValue != null)
                {
                    bool escapeApplied = false;
                    int length = sb.Length;
                    FormatPlainText(cellValue.ToString(), csv, new StringWriter(sb, CultureInfo.CurrentCulture), ref escapeApplied);
                    if (escapeApplied)
                    {
                        sb.Insert(length, '"');
                    }
                }

                if (lastCell) // Last cell
                {
                    sb.Append('\r');
                    sb.Append('\n');
                }
                else
                {
                    sb.Append(csv ? ',' : '\t');
                }
            }
            else if (string.Equals(format, DataFormats.Html, StringComparison.OrdinalIgnoreCase))
            {
                if (firstCell) // First cell - append start of row
                {
                    sb.Append("<TR>");
                }
                sb.Append("<TD>"); // Start cell
                if (cellValue != null)
                {
                    FormatPlainTextAsHtml(cellValue.ToString(), new StringWriter(sb, CultureInfo.CurrentCulture));
                }
                else
                {
                    sb.Append("&nbsp;");
                }
                sb.Append("</TD>"); // End cell
                if (lastCell) // Last cell - append end of row
                {
                    sb.Append("</TR>");
                }
            }
        }
        
        private static void FormatPlainText(string s, bool csv, TextWriter output, ref bool escapeApplied)
        {
            if (s != null)
            {
                int length = s.Length;
                for (int i = 0; i < length; i++)
                {
                    char ch = s[i];
                    switch (ch)
                    {
                        case '\t':
                            if (!csv)
                            {
                                output.Write(' ');
                            }
                            else
                            {
                                output.Write('\t');
                            }
                            break;

                        case '"':
                            if (csv)
                            {
                                output.Write("\"\"");
                                escapeApplied = true;
                            }
                            else
                            {
                                output.Write('"');
                            }
                            break;

                        case ',':
                            if (csv)
                            {
                                escapeApplied = true;
                            }
                            output.Write(',');
                            break;

                        default:
                            output.Write(ch);
                            break;
                    }
                }
                if (escapeApplied)
                {
                    output.Write('"');
                }
            }
        }
        
        internal static void GetClipboardContentForHtml(StringBuilder sbContent)
        {
            sbContent.Insert(0, "<TABLE>");
            sbContent.Append("</TABLE>");

            // Marshal.SystemDefaultCharSize is 2 on WinXP Pro - so the offsets seem to be in character counts instead of bytes. 
            int bytecountEndOfFragment = 135 + sbContent.Length;
            int bytecountEndOfHtml = bytecountEndOfFragment + 36;
            string prefix = string.Format(CultureInfo.InvariantCulture, DATAGRIDVIEW_htmlPrefix, bytecountEndOfHtml.ToString("00000000", CultureInfo.InvariantCulture), bytecountEndOfFragment.ToString("00000000", CultureInfo.InvariantCulture)) + DATAGRIDVIEW_htmlStartFragment;
            sbContent.Insert(0, prefix);
            sbContent.Append(DATAGRIDVIEW_htmlEndFragment);
        }

        private const string DATAGRIDVIEW_htmlPrefix = "Version:1.0\r\nStartHTML:00000097\r\nEndHTML:{0}\r\nStartFragment:00000133\r\nEndFragment:{1}\r\n";
        private const string DATAGRIDVIEW_htmlStartFragment = "<HTML>\r\n<BODY>\r\n<!--StartFragment-->";
        private const string DATAGRIDVIEW_htmlEndFragment = "\r\n<!--EndFragment-->\r\n</BODY>\r\n</HTML>";

        // Code taken from ASP.NET file xsp\System\Web\httpserverutility.cs; same in DataGridViewCell.cs
        private static void FormatPlainTextAsHtml(string s, TextWriter output)
        {
            if (s == null)
            {
                return;
            }

            int cb = s.Length;
            char prevCh = '\0';

            for (int i = 0; i < cb; i++)
            {
                char ch = s[i];
                switch (ch)
                {
                    case '<':
                        output.Write("&lt;");
                        break;
                    case '>':
                        output.Write("&gt;");
                        break;
                    case '"':
                        output.Write("&quot;");
                        break;
                    case '&':
                        output.Write("&amp;");
                        break;
                    case ' ':
                        if (prevCh == ' ')
                        {
                            output.Write("&nbsp;");
                        }
                        else
                        {
                            output.Write(ch);
                        }
                        break;
                    case '\r':
                        // Ignore \r, only handle \n
                        break;
                    case '\n':
                        output.Write("<br>");
                        break;
                    // REVIEW: what to do with tabs?  See original code in xsp\System\Web\httpserverutility.cs
                    default:
                        // The seemingly arbitrary 160 comes from RFC
                        if (ch >= 160 && ch < 256)
                        {
                            output.Write("&#");
                            output.Write(((int)ch).ToString(NumberFormatInfo.InvariantInfo));
                            output.Write(';');
                        }
                        else
                        {
                            output.Write(ch);
                        }
                        break;
                }
                prevCh = ch;
            }
        }


    }
}
