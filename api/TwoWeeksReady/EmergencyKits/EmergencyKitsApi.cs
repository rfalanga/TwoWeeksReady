using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AzureFunctions.OidcAuthentication;

namespace TwoWeeksReady.EmergencyKits
{
    public class EmergencyKitsApi
    {
        private readonly IApiAuthentication _apiAuthentication;

        public EmergencyKitsApi(IApiAuthentication apiAuthentication)
        {
            _apiAuthentication = apiAuthentication;
        }

        [FunctionName("emergencykits")]
        public async Task<IActionResult> GetList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
            HttpRequest req,
            [CosmosDB( databaseName: "2wr", collectionName: "emergencykits", ConnectionStringSetting = "CosmosDBConnection")]
            DocumentClient client,
            ILogger log)
        {
          
            log.LogInformation($"Getting list of emergency kits");      
            var authorizationResult = await _apiAuthentication.AuthenticateAsync(req.Headers);
            if (authorizationResult.Failed)
            {
                log.LogWarning(authorizationResult.FailureReason);
                return new UnauthorizedResult();
            }      
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("2wr", "emergencykits");
            var query = client.CreateDocumentQuery<EmergencyKit>(collectionUri, new FeedOptions{EnableCrossPartitionQuery = true}) // REVIEW: Why is this required when my where clause includes the partition id?
                                .Where(e => e.UserId == authorizationResult.User.Identity.Name)
                                .AsDocumentQuery();

            var emergencyKits = new List<EmergencyKit>();
            while (query.HasMoreResults)
            {
                var result = await query.ExecuteNextAsync<EmergencyKit>();
                emergencyKits.AddRange(result);                
            }

            return new OkObjectResult(emergencyKits);
        }

        [FunctionName("emergencykits-add")]
        public async Task<IActionResult> Add(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]
            HttpRequest req,
            [CosmosDB( databaseName: "2wr", collectionName: "emergencykits", ConnectionStringSetting = "CosmosDBConnection")]
            DocumentClient client,
            ILogger log)
        {
          
            log.LogInformation($"Getting list of emergency kits");      
            var authorizationResult = await _apiAuthentication.AuthenticateAsync(req.Headers);
            if (authorizationResult.Failed)
            {
                log.LogWarning(authorizationResult.FailureReason);
                return new UnauthorizedResult();
            }

            var content = await new StreamReader(req.Body).ReadToEndAsync();
            var newEmergencyKit = JsonConvert.DeserializeObject<EmergencyKit>(content);
            newEmergencyKit.Id = Guid.NewGuid().ToString();
            newEmergencyKit.UserId = authorizationResult.User.Identity.Name;

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri("2wr", "emergencykits");
            Document document = await client.CreateDocumentAsync(collectionUri, newEmergencyKit);
      
           
            return new OkObjectResult(newEmergencyKit);
        }
    }
}
