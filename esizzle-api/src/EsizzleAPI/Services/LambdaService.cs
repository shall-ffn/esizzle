using Amazon.Lambda;
using Amazon.Lambda.Model;
using EsizzleAPI.Controllers;
using System.Text.Json;

namespace EsizzleAPI.Services
{
    /// <summary>
    /// Service for invoking AWS Lambda functions
    /// Integrates with AWS Lambda service for PDF processing and other serverless operations
    /// </summary>
    public class LambdaService : ILambdaService
    {
        private readonly ILogger<LambdaService> _logger;
        private readonly IConfiguration _configuration;
        private readonly AmazonLambdaClient _lambdaClient;
        private readonly bool _isLocalDevelopment;

        public LambdaService(ILogger<LambdaService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Check if we're in local development mode
            _isLocalDevelopment = _configuration.GetValue<bool>("Lambda:UseLocalMode", false);
            
            if (!_isLocalDevelopment)
            {
                // Initialize AWS Lambda client for production
                _lambdaClient = new AmazonLambdaClient();
                _logger.LogInformation("LambdaService initialized for AWS Lambda integration");
            }
            else
            {
                _logger.LogInformation("LambdaService initialized in LOCAL DEVELOPMENT mode");
            }
        }

        public async Task InvokeAsync(string functionName, object payload)
        {
            if (_isLocalDevelopment)
            {
                await InvokeLocalAsync(functionName, payload);
                return;
            }

            try
            {
                _logger.LogInformation("Invoking AWS Lambda function '{FunctionName}'", functionName);
                
                // Serialize payload to JSON
                var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // Create Lambda invocation request
                var request = new InvokeRequest
                {
                    FunctionName = functionName,
                    InvocationType = InvocationType.Event, // Asynchronous invocation
                    Payload = jsonPayload
                };

                // Invoke Lambda function
                var response = await _lambdaClient.InvokeAsync(request);

                // Check for Lambda execution errors
                if (response.StatusCode != 202) // 202 = Accepted for async invocation
                {
                    _logger.LogError("Lambda invocation failed with status code: {StatusCode}", response.StatusCode);
                    throw new InvalidOperationException($"Lambda invocation failed with status code: {response.StatusCode}");
                }

                if (!string.IsNullOrEmpty(response.FunctionError))
                {
                    _logger.LogError("Lambda function error: {Error}", response.FunctionError);
                    throw new InvalidOperationException($"Lambda function error: {response.FunctionError}");
                }

                _logger.LogInformation("Successfully invoked Lambda function '{FunctionName}' (RequestId: {RequestId})", 
                    functionName, response.ResponseMetadata?.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invoke Lambda function '{FunctionName}': {Error}", 
                    functionName, ex.Message);
                throw new InvalidOperationException($"Failed to invoke Lambda function '{functionName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Local development mode - can simulate Lambda invocation or call local debug script
        /// </summary>
        private async Task InvokeLocalAsync(string functionName, object payload)
        {
            _logger.LogInformation("LOCAL MODE: Simulating Lambda function '{FunctionName}' invocation", functionName);
            
            // In local development, you can:
            // 1. Just log the invocation (current behavior)
            // 2. Call the local debug script
            // 3. Make HTTP calls to a local Lambda simulator
            
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            
            _logger.LogInformation("LOCAL MODE: Would invoke '{FunctionName}' with payload:\n{Payload}", 
                functionName, jsonPayload);

            // Optional: Try to invoke local debug script if configured
            var localLambdaUrl = _configuration.GetValue<string>("Lambda:LocalUrl");
            if (!string.IsNullOrEmpty(localLambdaUrl))
            {
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(5); // Lambda can take time
                    
                    var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync($"{localLambdaUrl}/{functionName}", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("LOCAL MODE: Successfully called local Lambda simulator");
                    }
                    else
                    {
                        _logger.LogWarning("LOCAL MODE: Local Lambda simulator returned {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LOCAL MODE: Failed to call local Lambda simulator, continuing with simulation");
                }
            }

            // Simulate async processing delay
            await Task.Delay(100);
        }

        public void Dispose()
        {
            _lambdaClient?.Dispose();
        }
    }
}
