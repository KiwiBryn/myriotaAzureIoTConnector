// Copyright (c) August 2024, devMobile Software, MIT License
//
namespace devMobile.IoT.myriotaAzureIoTConnector.myriota.UplinkWebhook
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            builder.Services.AddApplicationInsightsTelemetry(i => i.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights"));
            builder.Services.AddAzureClients(azureClient =>
            {
                azureClient.AddQueueServiceClient(builder.Configuration.GetConnectionString("UplinkQueueStorage"));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.MapControllers();

            app.Run();
        }
    }
}