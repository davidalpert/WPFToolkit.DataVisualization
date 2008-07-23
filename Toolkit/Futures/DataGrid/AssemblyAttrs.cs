//---------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
// This file specifies various assembly level attributes.
//
//---------------------------------------------------------------------------

using System.Security;
using System.Windows;
using System.Windows.Markup;

// Needed to turn on checking of security critical call chains
[assembly:SecurityCritical]

// Needed to enable xbap scenarios
[assembly:AllowPartiallyTrustedCallers]

[assembly:ThemeInfo(
    // Specifies the location of theme specific resources
    ResourceDictionaryLocation.SourceAssembly,
    // Specifies the location of non-theme specific resources:
    ResourceDictionaryLocation.SourceAssembly)]

[assembly:XmlnsDefinition("http://schemas.microsoft.com/wpf/2008/toolkit", "Microsoft.Windows.Controls")]
