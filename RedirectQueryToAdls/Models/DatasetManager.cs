using System;
using AMO = Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.PowerBI.Api;
using PbiModels = Microsoft.PowerBI.Api.Models;
using System.IO;

namespace RedirectQueryToAdls.Models {
  class DatasetManager {

    // Using the Power BI Service API
    public static void ImportPBIX(Guid WorkspaceId, string PbixFilePath, string ImportName) {
      PowerBIClient pbiClient = TokenManager.GetPowerBiClient();
      FileStream stream = new FileStream(PbixFilePath, FileMode.Open, FileAccess.Read);
      var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName, PbiModels.ImportConflictHandlerMode.CreateOrOverwrite);
      Console.WriteLine("PBIX imported into workspace as " + ImportName);
    }


    // Using Tabular Object Model via the XMLA endpoint in Power BI Premium

    public static Server server = new Server();

    public static void ConnectToPowerBIAsUser() {
      string workspaceConnection = GlobalConstants.WorkspaceConnection;
      string accessToken = TokenManager.GetAccessToken();
      string connectStringUser = $"DataSource={workspaceConnection};Password={accessToken};";
      server.Connect(connectStringUser);
    }

    public static void DisplayDatabases() {
      foreach (Database database in server.Databases) {
        Console.WriteLine(database.Name);
        Console.WriteLine(database.CompatibilityLevel);
        Console.WriteLine(database.CompatibilityMode);
        Console.WriteLine(database.EstimatedSize);
        Console.WriteLine(database.ID);
        Console.WriteLine();
      }
    }

    public static void GetDatabaseInfo(string Name) {

      Database database = server.Databases.GetByName(Name);

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

      Database database = server.Databases.GetByName(DatabaseName);

      Table  table = database.Model.Tables.Find("Sales");

      Console.WriteLine("Name: " + table.Name);
      Console.WriteLine("ObjectType: " + table.ObjectType);
      Console.WriteLine("Partitions: " + table.Partitions.Count);
      Console.WriteLine();

      Partition partition = table.Partitions[0];
      var partitionSource = partition.Source as MPartitionSource;
      Console.WriteLine(partitionSource.Expression);

    }

    public static void UpdateTableQuery(string DatabaseName, string TableName) {
      Database database = server.Databases.GetByName(DatabaseName);
      Table table = database.Model.Tables.Find(TableName);
      Partition partition = table.Partitions[0];
      // get table partion as M partition
      var partitionSource = partition.Source as MPartitionSource;
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

      Console.WriteLine();
      Console.WriteLine();
      // refresh dataset using Excel file in ADLS
      Console.WriteLine("Query updated - running refresh operation");
      database.Model.RequestRefresh(RefreshType.DataOnly);
      database.Model.SaveChanges();
      Console.WriteLine("Dataset refresh complete");    

    }

    public static void RefreshDatabaseModel(string Name) {
      Database database = server.Databases.GetByName(Name);
      database.Model.RequestRefresh(RefreshType.DataOnly);
      database.Model.SaveChanges();
    }

    public static Database CreateDatabase(string DatabaseName) {

      string newDatabaseName = server.Databases.GetNewName(DatabaseName);

      var database = new Database() {
        Name = newDatabaseName,
        ID = newDatabaseName,
        CompatibilityLevel = 1520,
        StorageEngineUsed = Microsoft.AnalysisServices.StorageEngineUsed.TabularMetadata,
        Model = new Model() {
          Name = DatabaseName + "-Model",
          Description = "A Demo Tabular data model with 1520 compatibility level."
        }
      };

      server.Databases.Add(database);
      database.Update(Microsoft.AnalysisServices.UpdateOptions.ExpandFull);

      return database;
    }

    public static Database CopyDatabase(string sourceDatabaseName, string DatabaseName) {

      Database sourceDatabase = server.Databases.GetByName(sourceDatabaseName);

      string newDatabaseName = server.Databases.GetNewName(DatabaseName);
      Database targetDatabase = CreateDatabase(newDatabaseName);
      sourceDatabase.Model.CopyTo(targetDatabase.Model);
      targetDatabase.Model.SaveChanges();

      targetDatabase.Model.RequestRefresh(RefreshType.Full);
      targetDatabase.Model.SaveChanges();

      return targetDatabase;
    }

  }
}