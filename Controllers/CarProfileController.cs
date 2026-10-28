using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;

namespace App_API.Controllers {
public class CarProfileController : ControllerBase {
[HttpGet("/profiles/car")]
[AllowAnonymous]
public async Task<ActionResult> Get(string id) {
var carClient = new TableClient(new Uri(Environment.GetEnvironmentVariable("azureStorageUri")), TableNames.tblCars, new DefaultAzureCredential());
var car = await carClient.GetEntityAsync<CarTableEntity>("car", id);
if (car.Value == null) return NotFound();

var indexClient = new TableClient(new Uri(Environment.GetEnvironmentVariable("azureStorageUri")), TableNames.tblCarEventIndex, new DefaultAzureCredential());
var idxList = new List<CarEventIndex>();
await foreach (var idx in indexClient.QueryAsync<CarEventIndex>(filter: $"PartitionKey eq '{id}'")) { idxList.Add(idx); }

var eventClient = new TableClient(new Uri(Environment.GetEnvironmentVariable("azureStorageUri")), TableNames.tblEventEntries, new DefaultAzureCredential());
var rallies = new List<object>();
foreach (var e in idxList.OrderBy(x => x.EventDate)) {
    string rallyRowKey = e.RowKey; // default fallback
    try {
        var entryQuery = eventClient.Query<TableEntity>(filter: $"RowKey eq '{e.EventRowKey}'");
        foreach (var ent in entryQuery) { rallyRowKey = ent.PartitionKey ?? e.RowKey; break; }
    } catch {}
    rallies.Add(new {
        year = e.EventYear > 0 ? e.EventYear : (e.EventDate?.Year ?? 0),
        rallyName = (!string.IsNullOrEmpty(e.EventName) ? e.EventName : (e.RowKey ?? "—")),
        rallyRowKey = rallyRowKey
    });
}
return Ok(new {
    make = car.Value.Make,
    model = car.Value.Model,
    vrm = car.Value.VRM,
    rowKey = car.Value.RowKey,
    rallies = rallies
});
}
}
}