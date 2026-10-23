using Microsoft.Extensions.Options;
using Quizware.BuzzerAgent;
using Quizware.Modules.Buzzer.Abstractions;
using Quizware.Modules.Buzzer.Agent;
using Quizware.Modules.Buzzer.Serial;

// The Quizware buzzer agent (P14-05). Run it on the PC with the RS-485 cable,
// as a console app or a Windows service (sc.exe create ... binPath= ...).
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "Quizware Buzzer Agent");

builder.Services.Configure<AgentHostOptions>(builder.Configuration.GetSection(AgentHostOptions.SectionName));
builder.Services.Configure<BuzzerOptions>(builder.Configuration.GetSection(BuzzerOptions.SectionName));

builder.Services.AddSingleton<ISerialPortConnection>(sp =>
    new SystemSerialPortConnection(sp.GetRequiredService<IOptions<BuzzerOptions>>().Value.Serial));
builder.Services.AddSingleton<SerialDeviceBus>();
builder.Services.AddHttpClient<IBuzzerAgentApi, HttpBuzzerAgentApi>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<AgentHostOptions>>().Value;
    http.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMs);
    http.DefaultRequestHeaders.Add(HttpBuzzerAgentApi.AgentKeyHeader, options.ApiKey);
});
builder.Services.AddSingleton<BuzzerAgentRunner>();
builder.Services.AddHostedService<AgentWorker>();

builder.Build().Run();
