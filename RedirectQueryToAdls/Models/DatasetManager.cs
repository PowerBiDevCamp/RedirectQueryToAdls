using System;
using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;
using Microsoft.PowerBI.Api;
using PbiModels = Microsoft.PowerBI.Api.Models;
using System.IO;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;

namespace RedirectQueryToAdls.Models {
  class DatasetManager {

    // Using the Power BI Service API
    public static void ImportPBIX(Guid WorkspaceId, string PbixFilePath, string ImportName) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient();
      FileStream stream = new FileStream(PbixFilePath, FileMode.Open, FileAccess.Read);
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName, PbiModels.ImportConflictHandlerMode.CreateOrOverwrite);
      Console.WriteLine("PBIX imported into workspace as " + ImportName);
    }

    public static Dataset GetDataset(Guid WorkspaceId, string DatasetName) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient();
      var datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
      foreach (var dataset in datasets) {
        if (dataset.Name.Equals(DatasetName)) {
          return dataset;
        }
      }
      return null;
    }

    public static void PatchAdlsCredentials(Guid WorkspaceId, string DatasetId) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient();
      pbiClient.Datasets.TakeOverInGroup(WorkspaceId, DatasetId);
      var datasources = (pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId)).Value;
      foreach (var datasource in datasources) {
        if (datasource.DatasourceType.ToLower() == "azureblobs") {
          // get the datasourceId and the gatewayId
          var datasourceId = datasource.DatasourceId;
          var gatewayId = datasource.GatewayId;
          // Create UpdateDatasourceRequest to update access key
          UpdateDatasourceRequest req = new UpdateDatasourceRequest {
            CredentialDetails = new CredentialDetails(
              new KeyCredentials(GlobalConstants.adlsStorageKey),
              PrivacyLevel.None,
              EncryptedConnection.NotEncrypted)
          };
          // Execute Patch command to update credentials
          pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);
        }
      };
    }

    // Using Tabular Object Model via the XMLA endpoint in Power BI Premium
    public static TOM.Server server = new TOM.Server();

    public static void ConnectToPowerBIAsUser() {
      string workspaceConnection = GlobalConstants.WorkspaceConnection;
      string accessToken = TokenManager.GetAccessToken();
      string connectStringUser = $"DataSource={workspaceConnection};Password={accessToken};";
      server.Connect(connectStringUser);
    }

    public static void DisplayDatabases() {
      foreach (TOM.Database database in server.Databases) {
        Console.WriteLine(database.Name);
        Console.WriteLine(database.CompatibilityLevel);
        Console.WriteLine(database.CompatibilityMode);
        Console.WriteLine(database.EstimatedSize);
        Console.WriteLine(database.ID);
        Console.WriteLine();
      }
    }

    public static void GetDatabaseInfo(string Name) {
      TOM.Database database = server.Databases.GetByName(Name);
      Console.WriteLine("Name: " + database.Name);
      Console.WriteLine("ID: " + database.ID);
      Console.WriteLine("ModelType: " + database.ModelType);
      Console.WriteLine("CompatibilityLevel: " + database.CompatibilityLevel);
      Console.WriteLine("LastUpdated: " + database.LastUpdate);
      Console.WriteLine("EstimatedSize: " + database.EstimatedSize);
      Console.WriteLine("CompatibilityMode: " + database.CompatibilityMode);
      Console.WriteLine("LastProcessed: " + database.LastProcessed);
      Console.WriteLine("LastSchemaUpdate: " + database.LastSchemaUpdate);
    }

    public static void GetTable(string DatabaseName, string TableName) {
      TOM.Database database = server.Databases.GetByName(DatabaseName);
      TOM.Table  table = database.Model.Tables.Find("Sales");
      Console.WriteLine("Name: " + table.Name);
      Console.WriteLine("ObjectType: " + table.ObjectType);
      Console.WriteLine("Partitions: " + table.Partitions.Count);
      Console.WriteLine();
      TOM.Partition partition = table.Partitions[0];
      var partitionSource = partition.Source as TOM.MPartitionSource;
      Console.WriteLine(partitionSource.Expression);
    }

    public static void UpdateTableQuery(string DatabaseName, string TableName) {
      TOM.Database database = server.Databases.GetByName(DatabaseName);
      TOM.Table table = database.Model.Tables.Find(TableName);
      TOM.Partition partition = table.Partitions[0];

      // get table partion as M partition
      var partitionSource = partition.Source as TOM.MPartitionSource;

      // get text for query
      string queryTemplate = Properties.Resources.SalesQuery_m;
      string query = queryTemplate.Replace("@adlsStorageAccountUrl", GlobalConstants.adlsBlobAccount);
      query = query.Replace("@adlsContainerPath", GlobalConstants.adlsBlobAccount + GlobalConstants.adlsBlobContainer);
      query = query.Replace("@adlsFileName", GlobalConstants.adlsFileName);

      // update query text
      Console.WriteLine("Updating query with the following M code");
      Console.WriteLine();
      Console.WriteLine(query);
      partitionSource.Expression = query;
      database.Model.SaveChanges();
    }

    public static void RefreshDataset(string Name) {
      TOM.Database database = server.Databases.GetByName(Name);
      database.Model.RequestRefresh(TOM.RefreshType.DataOnly);
      database.Model.SaveChanges();
    }

    public static TOM.Database CreateDatabase(string DatabaseName) {

      string newDatabaseName = server.Databases.GetNewName(DatabaseName);

      var database = new TOM.Database() {
        Name = newDatabaseName,
        ID = newDatabaseName,
        CompatibilityLevel = 1520,
        StorageEngineUsed = Microsoft.AnalysisServices.StorageEngineUsed.TabularMetadata,
        Model = new TOM.Model() {
          Name = DatabaseName + "-Model",
          Description = "A Demo Tabular data model with 1520 compatibility level."
        }
      };

      server.Databases.Add(database);
      database.Update(Microsoft.AnalysisServices.UpdateOptions.ExpandFull);
      return database;
    }

    public static TOM.Database CopyDatabase(string sourceDatabaseName, string DatabaseName) {
      TOM.Database sourceDatabase = server.Databases.GetByName(sourceDatabaseName);
      string newDatabaseName = server.Databases.GetNewName(DatabaseName);
      TOM.Database targetDatabase = CreateDatabase(newDatabaseName);
      sourceDatabase.Model.CopyTo(targetDatabase.Model);
      targetDatabase.Model.SaveChanges();
      targetDatabase.Model.RequestRefresh(TOM.RefreshType.Full);
      targetDatabase.Model.SaveChanges();
      return targetDatabase;
    }
    
  }
}