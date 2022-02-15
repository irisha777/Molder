using Molder.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Data.Common;

namespace Molder.Database.Models.Providers
{
    [ExcludeFromCodeCoverage]
    public class SqlProvider : ISqlProvider
    {
        private Lazy<SqlConnection> _connection = new(() => null);

        public SqlConnection Connection
        {
            get => _connection.Value;
            set
            {
                _connection = new Lazy<SqlConnection>(() => value);
            }
        }

        public bool Create(string connectionString)
        {
            try
            {
                if (Connection is null)
                {
                    Connection = Open(connectionString);
                    return true;
                }

                if (Connection.ConnectionString.Equals(connectionString))
                {
                    Log.Logger().LogWarning($"Connection with parameters: {Helpers.Message.CreateMessage(connectionString)} is already create");
                    return false;
                }

                Log.Logger().LogWarning($"Connection parameters are different: {Helpers.Message.CreateMessage(connectionString)}");
                return false;
            }
            catch (SqlException ex)
            {
                Log.Logger().LogError($"Connection with parameters: {Helpers.Message.CreateMessage(connectionString)} failed.{Environment.NewLine} {ex.Message}");
                throw new ConnectSqlException($"Connection with parameters: {Helpers.Message.CreateMessage(connectionString)} failed.{Environment.NewLine} {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Log.Logger().LogError($"Connection string is empty: {Helpers.Message.CreateMessage(connectionString)} {ex.Message}");
                throw new InvalidOperationException($"Connection string is empty: {Helpers.Message.CreateMessage(connectionString)} {ex.Message}");
            }
        }

        public async Task<bool> CreateAsync(string connectionString)
        {
            try
            {
                if (Connection is null)
                {
                    Connection = await OpenAsync(connectionString);
                    return true;
                }

                if (Connection.ConnectionString.Equals(connectionString))
                {
                    Log.Logger().LogWarning($"Connection with parameters: {Helpers.Message.CreateMessage(connectionString)} is already create");
                    return false;
                }

                Log.Logger().LogWarning($"Connection parameters are different: {Helpers.Message.CreateMessage(connectionString)}");
                return false;
            }
            catch (SqlException ex)
            {
                Log.Logger().LogError($"Connection with parameters: {Helpers.Message.CreateMessage(connectionString)} failed.{Environment.NewLine} {ex.Message}");
                throw new ConnectSqlException($"Connection with parameters: {Helpers.Message.CreateMessage(connectionString)} failed.{Environment.NewLine} {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Log.Logger().LogError($"Connection string is empty: {Helpers.Message.CreateMessage(connectionString)} {ex.Message}");
                throw new InvalidOperationException($"Connection string is empty: {Helpers.Message.CreateMessage(connectionString)} {ex.Message}");
            }
        }

        public void UsingTransaction(Action<SqlTransaction> onExecute, Action<Exception> onError, Action onSuccess = null!)
        {
            var transaction = Connection.BeginTransaction(IsolationLevel.ReadUncommitted);

            try
            {
                onExecute(transaction);
                transaction.Commit();
            }
            catch (Exception ex)
            {
                Log.Logger().LogError($"Transaction failed: {transaction} {Environment.NewLine} {ex.Message}");
                transaction.Rollback();
                onError(ex.GetBaseException());
            }
            finally
            {
                transaction.Dispose();
            }
        }
        public async Task UsingTransactionAsync(Action<SqlTransaction> onExecute, Action<Exception> onError, Action onSuccess = null!)
        {
            var transaction = await Connection.BeginTransactionAsync(IsolationLevel.ReadUncommitted);

            try
            {
                onExecute((SqlTransaction)transaction);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Log.Logger().LogError($"Transaction failed: {transaction} {Environment.NewLine} {ex.Message}");
                await transaction.RollbackAsync();
                onError(ex.GetBaseException());
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }

        public SqlCommand SetupCommand(string query, int? timeout = null)
        {
            var command = Connection.CreateCommand();
            command.CommandTimeout = Math.Min(300, Math.Max(0, timeout ?? 0));
            command.CommandType = CommandType.Text;
            command.CommandText = query;

            return command;
        }

        public bool IsConnectAlive()
        {
            return Connection is not null && Connection.State.HasFlag(ConnectionState.Open);
        }

        public bool Disconnect()
        {
            if (Connection == null)
            {
                return true;
            }

            try
            {
                Connection.Dispose();
                Log.Logger().LogInformation($"Connection is closed and disposed.");
                Connection = null;
                return true;
            }
            catch (Exception ex)
            {
                Log.Logger().LogError($"Connection not disposed {ex.Message}");
                Connection = null;
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            if (Connection == null)
            {
                return true;
            }

            try
            {
                await Connection.DisposeAsync();
                Log.Logger().LogInformation($"Connection is closed and disposed.");
                Connection = null;
                return true;
            }
            catch (Exception ex)
            {
                Log.Logger().LogError($"Connection not disposed {ex.Message}");
                Connection = null;
                return false;
            }
        }

        protected SqlConnection Open(string connectionString)
        {
            var connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        protected async Task<SqlConnection> OpenAsync(string connectionString)
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}
