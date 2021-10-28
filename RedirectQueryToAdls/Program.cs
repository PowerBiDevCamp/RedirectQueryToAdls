using Microsoft.PowerBI.Api.Models;
using RedirectQueryToAdls.Models;
using System;

namespace RedirectQueryToAdls {
  class Program {
    static void Main(string[] args) {
      Console.WriteLine();

      string localPbixFilePath = GlobalConstants.localPbixFilePath;
      string datasetName = GlobalConstants.datasetName;
      Guid targetWorkspaceId = new Guid(GlobalConstants.targetWorkspaceId);

      // import PBIX file from report builders PC
      DatasetManager.ImportPBIX(targetWorkspaceId, localPbixFilePath, datasetName);

      // overwrite M code behind query to redirect datasource to ADLS
      DatasetManager.ConnectToPowerBIAsUser();
      string tableName = GlobalConstants.tableName;
      DatasetManager.UpdateTableQuery(datasetName, tableName);

      Dataset dataset = DatasetManager.GetDataset(targetWorkspaceId, datasetName);

      DatasetManager.PatchAdlsCredentials(targetWorkspaceId, dataset.Id);

      DatasetManager.RefreshDataset(datasetName);

    }
  }
}
