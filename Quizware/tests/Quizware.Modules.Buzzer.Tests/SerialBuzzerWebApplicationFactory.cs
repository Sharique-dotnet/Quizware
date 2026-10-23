using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quizware.Api.IntegrationTests;
using Quizware.Modules.Buzzer.Serial;

namespace Quizware.Modules.Buzzer.Tests;

/// <summary>The API with <c>Buzzer:Provider = Serial</c> chosen by
/// configuration, exactly as on a venue machine — only the COM port itself
/// is replaced by <see cref="FakeSerialPort"/>.</summary>
public sealed class SerialBuzzerWebApplicationFactory : CustomWebApplicationFactory
{
    public FakeSerialPort Port { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Buzzer:Provider", "Serial");
        builder.UseSetting("Buzzer:DeviceCount", "3");
        builder.UseSetting("Buzzer:Serial:BusDelayMs", "0");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISerialPortConnection>();
            services.AddSingleton<ISerialPortConnection>(Port);
        });
    }
}
