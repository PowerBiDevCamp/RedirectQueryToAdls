using System;
using System.Collections.Generic;
using System.Text;

namespace RedirectQueryToAdls {
  class GlobalConstants {

    // metadata from public Azure AD application
    public const string ApplicationId = "";
    public const string RedirectUri = "http://localhost";

    // info for PBIX import operation
    public const string localPbixFilePath = @"C:\DevCamp\RedirectQueryToAdls\SalesDemo.pbix";
    public const string datasetName = "Sales Demo";
    public const string targetWorkspaceId = "";

    // info for query overwrite operation
    public const string tableName = "Sales"; // must match table name defined in PBIX file

    // connection string Tabular Object Model 
    public const string WorkspaceConnection = "powerbi://api.powerbi.com/v1.0/myorg/MY_WORKSPACE";

    // info required to u[pdate query to redirect to ADLS
    public const string adlsFilePath = "https://powerbidevcamp.blob.core.windows.net/exceldata/";
    public const string adlsBlobAccount = "https://powerbidevcamp.blob.core.windows.net/";
    public const string adlsBlobContainer = "exceldata/";
    public const string adlsFileName= "SalesDataProd2.xlsx";

    // key required to configure credentials
    public const string adlsStorageKey = "";

  }
}
