using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Npgsql;
using OzonEdu.StockApi.Infrastructure.Configuration;
using OzonEdu.StockApi.Infrastructure.Repositories.Infrastructure.Interfaces;

namespace OzonEdu.StockApi.Infrastructure.Repositories.Infrastructure
{
    public class NpgsqlConnectionFactory : IDbConnectionFactory<NpgsqlConnection>
    {
        private readonly DatabaseConnectionOptions _options;

        public NpgsqlConnectionFactory(IOptions<DatabaseConnectionOptions> options)
        {
            _options = options.Value;
            // TODO: Возможно, тут было бы неплохо провалидировать коннекшн стрингу на пустоту.
        }

        public NpgsqlConnection Connection { get; private set; }

        public async Task<NpgsqlConnection> CreateConnection(CancellationToken token)
        {
            if (Connection != null)
            {
                return Connection;
            }

            Connection = new NpgsqlConnection(_options.ConnectionString);
            await Connection.OpenAsync(token);
            Connection.Disposed += (o, e) =>
            {
                Connection = null;
            };
            return Connection;
        }

        public void Dispose()
        {
            Connection?.Dispose();
        }
    }
}