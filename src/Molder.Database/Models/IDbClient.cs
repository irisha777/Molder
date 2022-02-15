using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Molder.Database.Models
{
    public interface IDbClient : IDisposable
    {
        IDbConnection Get();

        bool Create(DbConnectionStringBuilder parameters);
        Task<bool> CreateAsync(DbConnectionStringBuilder parameters);
        bool IsConnectAlive();
        (object, int) ExecuteQuery(string query, int? timeout = null);
        Task<(object, int)> ExecuteQueryAsync(string query, int? timeout = null);
        string ExecuteStringQuery(string query, int? timeout = null);
        Task<string> ExecuteStringQueryAsync(string query, int? timeout = null);
        int ExecuteNonQuery(string query, int? timeout = null);
        Task<int> ExecuteNonQueryAsync(string query, int? timeout = null);
        object ExecuteScalar(string query, int? timeout = null);
        Task<object> ExecuteScalarAsync(string query, int? timeout = null);
    }
}
