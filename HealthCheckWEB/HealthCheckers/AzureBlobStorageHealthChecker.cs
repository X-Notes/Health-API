using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckWEB.HealthCheckers
{
    public class AzureBlobStorageHealthChecker : IHealthCheck
    {
        private readonly BlobServiceClient blobServiceClient;

        public AzureBlobStorageHealthChecker(BlobServiceClient blobServiceClient)
        {
            this.blobServiceClient = blobServiceClient;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var statistic = await blobServiceClient.GetPropertiesAsync(cancellationToken);
                return HealthCheckResult.Healthy("Blob storage healthy");
            }
            catch (Exception e)
            {
                return HealthCheckResult.Unhealthy("Azure Storage does not work");
            }
        }
    }
}
