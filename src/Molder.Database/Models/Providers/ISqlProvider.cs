using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Molder.Database.Models.Providers
{
    public interface ISqlProvider
    {
        bool Create(string connectionString);
        Task<bool> CreateAsync(string connectionString);
        bool IsConnectAlive();
        void UsingTransaction(Action<SqlTransaction> onExecute, Action<Exception> onError, Action onSuccess = null);
        Task UsingTransactionAsync(Action<SqlTransaction> onExecute, Action<Exception> onError, Action onSuccess = null);
        SqlCommand SetupCommand(string query, int? timeout = null);
        bool Disconnect();
        Task<bool> DisconnectAsync();
    }
}
