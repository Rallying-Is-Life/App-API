using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using App_API.Models;
using System.Collections.Generic;

namespace App_API.Controllers
{
    public class StatsController : ControllerBase
    {
        [HttpGet("/stats")]
        [AllowAnonymous]
        public async Task<IActionResult> Get()
        {
            int C = 0, R = 0, S = 0;
            var u = Environment.GetEnvironmentVariable("azureStorageUri");
            var cred = new DefaultAzureCredential();
            var svc = new TableServiceClient(new Uri(u), cred);

            await foreach (var _ in svc.GetTableClient(TableNames.tblClubDetails).QueryAsync<TableEntity>()) C++;
            await foreach (var _ in svc.GetTableClient(TableNames.tblRallyDetails).QueryAsync<TableEntity>()) R++;
            await foreach (var _ in svc.GetTableClient(TableNames.tblRallyStages).QueryAsync<TableEntity>()) S++;

            int cars = 0;
            await foreach (var _ in svc.GetTableClient(TableNames.tblCars).QueryAsync<TableEntity>()) cars++;

            int rescue = 0, recovery = 0;
            await foreach (var _ in svc.GetTableClient("tblRescueRecoveryUnits").QueryAsync<TableEntity>(filter: "PartitionKey eq 'rescueUnit'")) rescue++;
            await foreach (var _ in svc.GetTableClient("tblRescueRecoveryUnits").QueryAsync<TableEntity>(filter: "PartitionKey eq 'recoveryUnit'")) recovery++;

            var driverPersonKeys = new HashSet<string>();
            var coDriverPersonKeys = new HashSet<string>();

            await foreach (var e in svc.GetTableClient(TableNames.tblPersonEventIndex).QueryAsync<TableEntity>(filter: "Role eq 'Driver'"))
            {
                driverPersonKeys.Add(e.PartitionKey);
            }
            await foreach (var e in svc.GetTableClient(TableNames.tblPersonEventIndex).QueryAsync<TableEntity>(filter: "Role eq 'CoDriver'"))
            {
                coDriverPersonKeys.Add(e.PartitionKey);
            }

            return Ok(new
            {
                clubs = C,
                rallies = R,
                stages = S,
                cars = cars,
                rescueUnits = rescue,
                recoveryUnits = recovery,
                drivers = driverPersonKeys.Count,
                coDrivers = coDriverPersonKeys.Count
            });
        }
    }
}
