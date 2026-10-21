using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Azure.Data.Tables; using Azure.Identity; using App_API.Models;
namespace App_API.Controllers {
public class StatsController : ControllerBase {
[HttpGet("/stats")]
    [AllowAnonymous] public async Task<IActionResult> Get() {
int C = 0, R = 0; var u = Environment.GetEnvironmentVariable("azureStorageUri"); var cred = new DefaultAzureCredential();
await foreach(var _ in new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblClubDetails).QueryAsync<Azure.Data.Tables.TableEntity>()) C++;
await foreach(var _ in new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblRallyDetails).QueryAsync<Azure.Data.Tables.TableEntity>()) R++;
return Ok(new { clubs = C, rallies = R }); } } }
