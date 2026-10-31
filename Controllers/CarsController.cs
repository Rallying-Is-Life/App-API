using App_API.Models;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace App_API.Controllers {
public class CarsController : ControllerBase {
[HttpGet("/cars")]
[AllowAnonymous]
public async Task<ActionResult<List<CarTableEntity>>> Get([FromQuery] int page=1, [FromQuery] int size=20, [FromQuery] string? q=null, [FromQuery] bool countOnly=false) {
var client = new TableClient(new Uri(Environment.GetEnvironmentVariable("azureStorageUri")), TableNames.tblCars, new DefaultAzureCredential());
var list = new List<CarTableEntity>();
await foreach (CarTableEntity c in client.QueryAsync<CarTableEntity>()) { list.Add(c); }
if (!string.IsNullOrWhiteSpace(q)) {
var qq = q.ToLower();
list = list.Where(c => (c.Make ?? "").ToLower().Contains(qq) || (c.Model ?? "").ToLower().Contains(qq) || (c.VRM ?? "").ToLower().Contains(qq)).ToList();
}
list = list.OrderBy(c => c.Make + " " + c.Model + " " + c.VRM).ToList();
if (countOnly) return Ok(new { count = list.Count });
return list.Skip((page-1)*size).Take(size).ToList();
}
}
}
