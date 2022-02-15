using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Molder.Controllers;
using Molder.Database.Controllers;
using Molder.Database.Models;
using Molder.Helpers;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;
using Molder.Database.Infrastructures;
using System.Data;
using Microsoft.Extensions.Logging;
using Molder.Database.Extensions;
using Molder.Database.Models.Parameters;
using Molder.Extensions;
using System.Data.SqlClient;
using Molder.Infrastructures;
using System.Threading.Tasks;

namespace Molder.Database.Steps
{
    /// <summary>
    /// Шаги для работы с SqlServer.
    /// </summary>
    [Binding]
    public class SqlServerSteps
    {
        private readonly DatabaseController databaseController;
        private readonly VariableController variableController;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerSteps"/> class.
        /// Привязка шагов работы с базами данных к работе с переменным через контекст.
        /// </summary>
        /// <param name="databaseController">Контекст для работы с базой данных.</param>
        /// <param name="variableController">Контекст для работы с переменными.</param>
        public SqlServerSteps(DatabaseController databaseController, VariableController variableController)
        {
            this.databaseController = databaseController;
            this.variableController = variableController;
        }

        #region Transformations
        /// <summary>
        /// Трансформация параметров подключения к SqlServer в DbConnectionParams.
        /// </summary>
        /// <param name="table">Параметры подключения.</param>
        /// <returns>Параметры подключения к SqlServer.</returns>
        [StepArgumentTransformation]
        public SqlConnectionStringBuilder GetDataBaseParametersFromTableSqlServer(Table table)
        {
            return table.ReplaceWith(variableController).CreateInstance<SqlConnectionStringBuilder>();
        }

        /// <summary>
        /// Преобразование строки запроса в отформатированную с заменами.
        /// </summary>
        [StepArgumentTransformation()]
        public QueryParam TransformationQuery(string query)
        {
            return new QueryParam { Query = variableController.ReplaceVariables(query) };
        }

        /// <summary>
        /// Преобразование Таблицы с параметрами в Список словарей для создания запроса
        /// </summary>        
        [StepArgumentTransformation]
        public IEnumerable<Dictionary<string, object>> TransformationTableToString(Table table)
        {
            return table.ToDictionary();
        }
        #endregion
        #region Connections
        /// <summary>
        /// Подключение к SQLServer.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="sqlConnectionString">Параметры подключения.</param>
        [Given(@"я подключаюсь к БД MS SQL Server с названием ""(.+)"":")]
        public void ConnectToDB_SqlServer(string connectionName, SqlConnectionStringBuilder sqlConnectionString)
        {
            databaseController.Connections.Should().NotContainKey(connectionName, $"Connection \"{connectionName}\" already exists");
            var connection = new SqlServerClient();
            connection.Create(sqlConnectionString).Should().BeTrue();
            connection.IsConnectAlive().Should().BeTrue();
            databaseController.Connections.TryAdd(connectionName, (connection, TypeOfAccess.Local, sqlConnectionString.ConnectTimeout));
        }

        /// <summary>
        /// Асинхронное подключение к SQLServer.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="sqlConnectionString">Параметры подключения.</param>
        [Given(@"я асинхронно подключаюсь к БД MS SQL Server с названием ""(.+)"":")]
        public async Task ConnectToDB_SqlServerAsync(string connectionName, SqlConnectionStringBuilder sqlConnectionString)
        {
            databaseController.Connections.Should().NotContainKey(connectionName, $"Connection \"{connectionName}\" already exists");
            var connection = new SqlServerClient();
            var isConnectionCreated = await connection.CreateAsync(sqlConnectionString);
            isConnectionCreated.Should().BeTrue();
            connection.IsConnectAlive().Should().BeTrue();
            databaseController.Connections.TryAdd(connectionName, (connection, TypeOfAccess.Local, sqlConnectionString.ConnectTimeout));
        }
        #endregion
        #region Execute Query Type
        /// <summary>
        /// Выполнение ExecuteQuery или ExecuteNonQuery БЕЗ сохранения результата
        /// </summary>        
        [StepDefinition(@"я выполняю ""(.+)"" запрос в БД ""(.+)"" и не сохраняю результат:")]
        public void ExecuteQueryType(QueryType queryType, string connectionName, QueryParam query)
        {
            var (_, count) = ExecuteAnyRequest(queryType, connectionName, query);
            Log.Logger().LogInformation($"Request {query} returned {count} row(s)");
        }

        /// <summary>
        /// Выполнение ExecuteQueryAsync или ExecuteNonQueryAsync БЕЗ сохранения результата
        /// </summary>        
        [StepDefinition(@"я асинхронно выполняю ""(.+)"" запрос в БД ""(.+)"" и не сохраняю результат:")]
        public async Task ExecuteQueryTypeAsync(QueryType queryType, string connectionName, QueryParam query)
        {
            var (_, count) = await ExecuteAnyRequestAsync(queryType, connectionName, query);
            Log.Logger().LogInformation($"Request {query} returned {count} row(s)");
        }

        /// <summary>
        /// Выполнение ExecuteQuery или ExecuteNonQuery C сохранением результата
        /// </summary>        
        [StepDefinition(@"я выполняю ""(.+)"" запрос в БД ""(.+)"" и сохраняю результат в переменную ""(.+)"":")]
        public void ExecuteQueryTypeWithVarName(QueryType queryType, string connectionName, string varName, QueryParam query)
        {
            var (outRecords, count) = ExecuteAnyRequest(queryType, connectionName, query);
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");

            this.variableController.SetVariable(varName, typeof(DataTable), outRecords);
            Log.Logger().LogInformation($"Request {query} returned {count} row(s)");
        }

        /// <summary>
        /// Выполнение ExecuteQuery или ExecuteNonQuery
        /// </summary>      
        private (object outRecords, int queryCount) ExecuteAnyRequest(QueryType queryType, string connectionName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);
            var (connection, _, timeout) = this.databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            Log.Logger().LogInformation($"{queryType} query:" + Environment.NewLine + $"{query}");

            switch (queryType)
            {
                case (QueryType.SELECT):
                    Log.Logger().LogInformation($"Choose {queryType} query. Query type is ExecuteQuery");
                    var (outRecords, queryCount) = connection.ExecuteQuery(query.Query, timeout);

                    Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
                    return (outRecords, queryCount);
                default:
                    Log.Logger().LogInformation($"Choose {queryType} query. Query type is ExecuteNonQuery");
                    var nonQueryCount = connection.ExecuteNonQuery(query.Query, timeout);
                    return (null, nonQueryCount);
            }
        }

        /// <summary>
        /// Выполнение ExecuteQueryAsync или ExecuteNonQueryAsync
        /// </summary>      
        private async Task<(object outRecords, int queryCount)> ExecuteAnyRequestAsync(QueryType queryType, string connectionName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);
            var (connection, _, timeout) = this.databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            Log.Logger().LogInformation($"{queryType} query:" + Environment.NewLine + $"{query}");

            switch (queryType)
            {
                case (QueryType.SELECT):
                    Log.Logger().LogInformation($"Choose {queryType} query. Query type is ExecuteQuery");
                    var (outRecords, queryCount) = await connection.ExecuteQueryAsync(query.Query, timeout);

                    Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
                    return (outRecords, queryCount);
                default:
                    Log.Logger().LogInformation($"Choose {queryType} query. Query type is ExecuteNonQuery");
                    var nonQueryCount = await connection.ExecuteNonQueryAsync(query.Query, timeout);
                    return (null, nonQueryCount);
            }
        }
        #endregion
        #region Execute Query/NonQuery/Scalar
        /// <summary>
        /// Выполнение ExecuteQuery
        /// </summary>        
        [StepDefinition(@"я выполняю запрос в БД ""(.+)"" с сохранением результата в переменную ""(.+)"":")]
        [StepDefinition(@"я выбираю несколько записей из БД ""(.+)"" и сохраняю их в переменную ""(.+)"":")]
        public void ExecuteQuery(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var (outRecords, count) = connection.ExecuteQuery(query.Query, timeout);
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
            Log.Logger().LogInformation($"Request {query} returned {count} row(s)");

            variableController.SetVariable(varName, typeof(DataTable), outRecords);
        }

        /// <summary>
        /// Выполнение ExecuteQueryAsync
        /// </summary>        
        [StepDefinition(@"я выполняю асинхронный запрос в БД ""(.+)"" с сохранением результата в переменную ""(.+)"":")]
        [StepDefinition(@"я асинхронно выбираю несколько записей из БД ""(.+)"" и сохраняю их в переменную ""(.+)"":")]
        public async Task ExecuteQueryAsync(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var (outRecords, count) = await  connection.ExecuteQueryAsync(query.Query, timeout);
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
            Log.Logger().LogInformation($"Request {query} returned {count} row(s)");

            variableController.SetVariable(varName, typeof(DataTable), outRecords);
        }

        /// <summary>
        /// Выполнение ExecuteNonQuery
        /// </summary>        
        [StepDefinition(@"я выполняю запрос в БД ""(.+)"" с сохранением количества обработанных записей в переменную ""(.+)"":")]
        public void ExecuteNonQuery(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.Should().ContainKey(connectionName, $"Connection: \"{connectionName}\" does not exist");

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var count = connection.ExecuteNonQuery(query.Query, timeout);
            Log.Logger().LogInformation($"Request {query} returned {count} row(s)");

            variableController.SetVariable(varName, typeof(int), count);
        }

        /// <summary>
        /// Выполнение ExecuteNonQueryAsync
        /// </summary>        
        [StepDefinition(@"я выполняю асинхронный запрос в БД ""(.+)"" с сохранением количества обработанных записей в переменную ""(.+)"":")]
        public async Task ExecuteNonQueryAsync(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.Should().ContainKey(connectionName, $"Connection: \"{connectionName}\" does not exist");

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var count = await connection.ExecuteNonQueryAsync(query.Query, timeout);
            Log.Logger().LogInformation($"Request {query} returned {count} row(s)");

            variableController.SetVariable(varName, typeof(int), count);
        }
        #endregion

        /// <summary>
        /// Шаг выборки строки (json/xml) из базы данных и сохранения в переменную.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="varName">Идентификатор переменной.</param>
        /// <param name="query">Запрос.</param>
        [StepDefinition(@"я выбираю единственную запись в виде строки из БД ""(.+)"" и сохраняю её в переменную ""(.+)"":")]
        public void SelectStringFromDbSetVariable(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var str = connection.ExecuteStringQuery(query.Query, timeout);
            
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(str != null ? $"is not empty" : $"is empty")}");
            variableController.SetVariable(varName, typeof(string), str);
        }

        /// <summary>
        /// Шаг асинхронной выборки строки (json/xml) из базы данных и сохранения в переменную.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="varName">Идентификатор переменной.</param>
        /// <param name="query">Запрос.</param>
        [StepDefinition(@"я асинхронно выбираю единственную запись в виде строки из БД ""(.+)"" и сохраняю её в переменную ""(.+)"":")]
        public async Task SelectStringFromDbSetVariableAsync(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var str = await connection.ExecuteStringQueryAsync(query.Query, timeout);

            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(str != null ? $"is not empty" : $"is empty")}");
            variableController.SetVariable(varName, typeof(string), str);
        }

        /// <summary>
        /// Шаг выборки записи из базы данных и сохранения в переменную.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="varName">Идентификатор переменной.</param>
        /// <param name="query">Запрос.</param>
        [StepDefinition(@"я выбираю единственную запись из БД ""(.+)"" и сохраняю её в переменную ""(.+)"":")]
        public void SelectSingleRowFromDbSetVariable(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var (outRecords, count) = connection.ExecuteQuery(query.Query, timeout);
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
            count.Should().Be(1, "Запрос вернул не одну запись");

            variableController.SetVariable(varName, typeof(DataRow), ((DataTable)outRecords!).Rows[0]);
        }

        /// <summary>
        /// Шаг асинхронной выборки записи из базы данных и сохранения в переменную.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="varName">Идентификатор переменной.</param>
        /// <param name="query">Запрос.</param>
        [StepDefinition(@"я асинхронно выбираю единственную запись из БД ""(.+)"" и сохраняю её в переменную ""(.+)"":")]
        public async Task SelectSingleRowFromDbSetVariableAsync(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.InputValidation(connectionName, query.Query);

            var (connection, _, timeout) = databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;
            var (outRecords, count) = await connection.ExecuteQueryAsync(query.Query, timeout);
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
            count.Should().Be(1, "Запрос вернул не одну запись");

            variableController.SetVariable(varName, typeof(DataRow), ((DataTable)outRecords!).Rows[0]);
        }

        /// <summary>
        /// Шаг выполнения Insert в БД с указанными значениями
        /// </summary>        
        [StepDefinition(@"я добавляю записи в таблицу ""(.+)"" в БД ""(.+)"":")]
        public void ExecuteInsertQueryFromTable(string tableName, string connectionName, IEnumerable<Dictionary<string, object>> insertQuery)
        {
            databaseController.Connections.Should().ContainKey(connectionName, $"Connection: \"{connectionName}\" does not exist");
            var (connection, _, timeout) = this.databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;

            connection.IsConnectAlive().Should().BeTrue();
            var query = insertQuery.ToSqlQuery(tableName);
            var count = connection.ExecuteNonQuery(query, timeout);

            count.Should().NotBe(0, $"INSERT {query} failed. Check table names or values");
            Log.Logger().LogInformation($"INSERT completed {Environment.NewLine} {query}. {Environment.NewLine} Changed {count} row(s).");
        }

        /// <summary>
        /// Шаг асинхронного выполнения Insert в БД с указанными значениями
        /// </summary>        
        [StepDefinition(@"я асинхронно добавляю записи в таблицу ""(.+)"" в БД ""(.+)"":")]
        public async Task ExecuteInsertQueryFromTableAsync(string tableName, string connectionName, IEnumerable<Dictionary<string, object>> insertQuery)
        {
            databaseController.Connections.Should().ContainKey(connectionName, $"Connection: \"{connectionName}\" does not exist");
            var (connection, _, timeout) = this.databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;

            connection.IsConnectAlive().Should().BeTrue();
            var query = insertQuery.ToSqlQuery(tableName);
            var count = await connection.ExecuteNonQueryAsync(query, timeout);

            count.Should().NotBe(0, $"INSERT {query} failed. Check table names or values");
            Log.Logger().LogInformation($"INSERT completed {Environment.NewLine} {query}. {Environment.NewLine} Changed {count} row(s).");
        }

        /// <summary>
        /// Шаг выборки единственной ячейки из базы данных и сохранения в переменную.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="varName">Идентификатор переменной.</param>
        /// <param name="query">Запрос.</param>
        [Scope(Tag = "DBAccess")]
        [StepDefinition(@"я сохраняю значение единственной ячейки из выборки из БД ""(.+)"" в переменную ""(.+)"":")]
        public void SelectScalarFromDbSetVariable(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.Should().ContainKey(connectionName, $"Connection: \"{connectionName}\" does not exist");
            var (connection, _, timeout) = this.databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;

            var (outRecords, count) = connection.ExecuteQuery(query.Query, timeout);
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
            count.Should().Be(1, "Запрос вернул не одну запись");

            variableController.SetVariable(varName, ((DataTable)outRecords!).Columns[0].DataType, ((DataTable)outRecords).Rows[0][0]);
        }

        /// <summary>
        /// Шаг асинхронной выборки единственной ячейки из базы данных и сохранения в переменную.
        /// </summary>
        /// <param name="connectionName">Название подключения.</param>
        /// <param name="varName">Идентификатор переменной.</param>
        /// <param name="query">Запрос.</param>
        [Scope(Tag = "DBAccess")]
        [StepDefinition(@"я сохраняю значение единственной ячейки из выборки из БД ""(.+)"" в переменную ""(.+)"":")]
        public async Task SelectScalarFromDbSetVariableAsync(string connectionName, string varName, QueryParam query)
        {
            databaseController.Connections.Should().ContainKey(connectionName, $"Connection: \"{connectionName}\" does not exist");
            var (connection, _, timeout) = this.databaseController.Connections.SingleOrDefault(_ => _.Key == connectionName).Value;

            var (outRecords, count) = await connection.ExecuteQueryAsync(query.Query, timeout);
            Log.Logger().LogInformation($"Request returned: {Environment.NewLine} {(outRecords != null ? ((DataTable)outRecords).CreateMessage() : $"is empty")}");
            count.Should().Be(1, "Запрос вернул не одну запись");

            variableController.SetVariable(varName, ((DataTable)outRecords!).Columns[0].DataType, ((DataTable)outRecords).Rows[0][0]);
        }
    }
}