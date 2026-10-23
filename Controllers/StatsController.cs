using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Azure.Data.Tables; using Azure.Identity; using App_API.Models;
namespace App_API.Controllers {
public class StatsController : ControllerBase {
[HttpGet("/stats")]
    [AllowAnonymous] public async Task<IActionResult> Get() {
int C = 0, R = 0, S = 0; var u = Environment.GetEnvironmentVariable("azureStorageUri"); var cred = new DefaultAzureCredential();
await foreach(var _ in new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblClubDetails).QueryAsync<Azure.Data.Tables.TableEntity>()) C++;
await foreach(var _ in new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblRallyDetails).QueryAsync<Azure.Data.Tables.TableEntity>()) R++;
await foreach(var _ in new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblRallyStages).QueryAsync<Azure.Data.Tables.TableEntity>()) S++;
int rescue = 0, recovery = 0;
await foreach(var _ in new TableServiceClient(new Uri(u), cred).GetTableClient("tblRescueRecoveryUnits").QueryAsync<Azure.Data.Tables.TableEntity>(filter: "PartitionKey eq 'rescueUnit'")) rescue++;
await foreach(var _ in new TableServiceClient(new Uri(u), cred).GetTableClient("tblRescueRecoveryUnits").QueryAsync<Azure.Data.Tables.TableEntity>(filter: "PartitionKey eq 'recoveryUnit'")) recovery++;
return Ok(new { clubs = C, rallies = R, stages = S, rescueUnits = rescue, recoveryUnits = recovery }); } } }
