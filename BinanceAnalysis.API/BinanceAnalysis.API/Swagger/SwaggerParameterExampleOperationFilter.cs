namespace BinanceAnalysis.API.Swagger
{
    using System.Text.Json;
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public class SwaggerParameterExampleOperationFilter : IOperationFilter
    {
        public void Apply(
            OpenApiOperation operation,
            OperationFilterContext context)
        {
            if (operation.Parameters is null)
            {
                return;
            }

            var startDateParameter = operation.Parameters
                .FirstOrDefault(x => x.Name == "s");

            if (startDateParameter is OpenApiParameter parameter)
            {
                parameter.Example = JsonSerializer.SerializeToNode(
                    "2026-11-01T14:30:00Z");
            }
        }
    }
}