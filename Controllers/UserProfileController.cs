using System.Security.Claims;
using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;

namespace App_API.Controllers
{
    [Authorize]
    [Route("api/userprofile")]
    [ApiController]
    public class UserProfileController : ControllerBase
    {
        private static string ParseFlagCode(string country, string region)
        {
            if (string.IsNullOrEmpty(country))
                return string.Empty;
            if (country.Equals("GB", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(region))
                    return "gb";
                switch (region.ToLower())
                {
                    case "wales": return "gb-wls";
                    case "scotland": return "gb-sct";
                    case "england": return "gb-eng";
                    case "northern ireland":
                    case "ni": return "gb-nir";
                    default: return "gb";
                }
            }
            return country.ToLower();
        }

        private string GetUserOid()
        {
            // Entra ID uses "oid" claim for object identifier
            return User.FindFirst("oid")?.Value
                ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? string.Empty;
        }

        private async Task<TableClient> GetPeopleClientAsync()
        {
            var uri = Environment.GetEnvironmentVariable("azureStorageUri")!;
            var cred = new DefaultAzureCredential();
            return new TableServiceClient(new Uri(uri), cred).GetTableClient(TableNames.tblPeople);
        }

        /// <summary>
        /// Get the authenticated user's profile. Creates a profile if one doesn't exist.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var userOid = GetUserOid();
            if (string.IsNullOrEmpty(userOid))
                return Unauthorized("Unable to determine user identity from token.");

            var client = await GetPeopleClientAsync();

            // Query for existing profile by EntraOid
            TableEntity? existingEntity = null;
            await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"EntraOid eq '{userOid}'", maxPerPage: 1))
            {
                existingEntity = entity;
                break;
            }

            if (existingEntity == null)
            {
                // Create a minimal profile for the user
                var newEntity = new TableEntity("personOverview", Guid.NewGuid().ToString("D"))
                {
                    { "FirstName", string.Empty },
                    { "Surname", string.Empty },
                    { "Country", string.Empty },
                    { "Region", string.Empty },
                    { "EntraOid", userOid },
                    { "CompetitorLicense", string.Empty },
                    { "MarshalLicense", string.Empty },
                    { "FlagCode", string.Empty }
                };
                await client.AddEntityAsync(newEntity);

                return Ok(new
                {
                    personRowKey = newEntity.RowKey,
                    firstName = string.Empty,
                    surname = string.Empty,
                    country = string.Empty,
                    region = string.Empty,
                    entraOid = userOid,
                    competitorLicense = string.Empty,
                    marshalLicense = string.Empty,
                    flagUrl = string.Empty
                });
            }

            var firstName = existingEntity.GetString("FirstName") ?? string.Empty;
            var surname = existingEntity.GetString("Surname") ?? string.Empty;
            var country = existingEntity.GetString("Country") ?? string.Empty;
            var region = existingEntity.GetString("Region") ?? string.Empty;
            var entraOid = existingEntity.GetString("EntraOid") ?? string.Empty;
            var competitorLicense = existingEntity.GetString("CompetitorLicense") ?? string.Empty;
            var marshalLicense = existingEntity.GetString("MarshalLicense") ?? string.Empty;
            var flagCode = ParseFlagCode(country, region);
            var flagUrl = string.IsNullOrEmpty(flagCode) ? string.Empty : $"/images/flags/4x3/{flagCode}.svg";

            return Ok(new
            {
                personRowKey = existingEntity.RowKey,
                firstName,
                surname,
                country,
                region,
                entraOid,
                competitorLicense,
                marshalLicense,
                flagUrl
            });
        }

        /// <summary>
        /// Update the authenticated user's profile. Only allows updating own profile.
        /// </summary>
        [HttpPatch]
        public async Task<ActionResult> Patch([FromBody] UserProfileUpdateRequest request)
        {
            var userOid = GetUserOid();
            if (string.IsNullOrEmpty(userOid))
                return Unauthorized("Unable to determine user identity from token.");

            var client = await GetPeopleClientAsync();

            // Find the user's profile by EntraOid
            TableEntity? existingEntity = null;
            await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"EntraOid eq '{userOid}'", maxPerPage: 1))
            {
                existingEntity = entity;
                break;
            }

            if (existingEntity == null)
                return NotFound("Profile not found.");

            // Update allowed fields
            if (!string.IsNullOrEmpty(request.FirstName))
                existingEntity["FirstName"] = request.FirstName;
            if (!string.IsNullOrEmpty(request.Surname))
                existingEntity["Surname"] = request.Surname;
            if (request.Country != null)
                existingEntity["Country"] = request.Country;
            if (request.Region != null)
                existingEntity["Region"] = request.Region;

            // Recalculate FlagCode
            var country = existingEntity.GetString("Country") ?? string.Empty;
            var region = existingEntity.GetString("Region") ?? string.Empty;
            var flagCode = ParseFlagCode(country, region);
            existingEntity["FlagCode"] = flagCode;

            await client.UpdateEntityAsync(existingEntity, existingEntity.ETag, TableUpdateMode.Replace);

            var flagUrl = string.IsNullOrEmpty(flagCode) ? string.Empty : $"/images/flags/4x3/{flagCode}.svg";

            return Ok(new
            {
                personRowKey = existingEntity.RowKey,
                firstName = existingEntity.GetString("FirstName") ?? string.Empty,
                surname = existingEntity.GetString("Surname") ?? string.Empty,
                country = existingEntity.GetString("Country") ?? string.Empty,
                region = existingEntity.GetString("Region") ?? string.Empty,
                entraOid = existingEntity.GetString("EntraOid") ?? string.Empty,
                competitorLicense = existingEntity.GetString("CompetitorLicense") ?? string.Empty,
                marshalLicense = existingEntity.GetString("MarshalLicense") ?? string.Empty,
                flagUrl
            });
        }
    }

    public class UserProfileUpdateRequest
    {
        public string? FirstName { get; set; }
        public string? Surname { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
    }
}