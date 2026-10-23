using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Azure.Data.Tables; using Azure.Identity; using System.Linq;
namespace App_API.Controllers;
public class RecoveryController : ControllerBase
{
    [HttpGet("/recovery")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TableEntity>>> Get(int page = 1)
    {
        var client = new TableClient(new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!), "tblRescueRecoveryUnits", new DefaultAzureCredential());
        var list = new List<TableEntity>();
        await foreach (TableEntity r in client.QueryAsync<TableEntity>(filter: "PartitionKey eq 'recoveryUnit'")) { list.Add(r); }
        return list.OrderBy(x => x["Name"]?.ToString() ?? x.RowKey ?? x.PartitionKey).Skip((page - 1) * 20).Take(20).ToList();
    }
}
