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
await foreach (var idx in indexClient.QueryAsync<CarEventIndex>(filter: $"PartitionKey eq '{id}'")) {
idxList.Add(idx);
}
return Ok(new {
make = car.Value.Make,
model = car.Value.Model,
vrm = car.Value.VRM,
rowKey = car.Value.RowKey,
rallies = idxList
.OrderBy(e => e.EventDate)
.Select(e => new {
year = e.EventDate?.Year ?? 0,
eventRowKey = e.RowKey,
eventDate = e.EventDate
})
});
}
}
}