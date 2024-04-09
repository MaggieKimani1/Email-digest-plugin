using Microsoft.Extensions.Configuration;

namespace sample_plugins
{
    static class ConfigurationProvider
    {
        public static IConfigurationRoot GetConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build();
            return configuration;
        }
    }
}
